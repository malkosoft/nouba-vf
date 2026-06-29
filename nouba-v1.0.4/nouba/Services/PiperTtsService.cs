using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Nouba.Infrastructure;

namespace Nouba.Services;

/// <summary>
/// Service TTS Piper natif offline. Aucune configuration visible dans l'admin :
/// les fichiers sont cherchés automatiquement dans wwwroot/tts/piper/.
/// Piper écrit toujours un vrai fichier WAV temporaire, ensuite lu comme audio/wav.
/// </summary>
public sealed class PiperTtsService
{
    private const string PiperBinaryWindows = "piper.exe";
    private const string PiperBinaryUnix    = "piper";

    private const string ModelFileFr = "fr_FR-upmc-medium.onnx";
    private const string ModelFileEn = "en_US-lessac-medium.onnx";

    // ── Voix masculine / féminine FR & EN (6 voix au total avec l'arabe) ──
    // Le modèle français fr_FR-upmc-medium contient DEUX locuteurs dans un
    // seul fichier : jessica (féminin, speaker 0) et pierre (masculin,
    // speaker 1). On sélectionne via l'argument Piper --speaker.
    private const int FrSpeakerFemale = 0; // jessica
    private const int FrSpeakerMale   = 1; // pierre

    // L'anglais lessac n'a qu'une voix (féminine). Pour le masculin anglais,
    // on cherche un modèle séparé parmi ces candidats (le 1er présent gagne).
    private static readonly string[] EnglishMaleCandidates =
    {
        "en_US-ryan-medium.onnx",
        "en_US-ryan-high.onnx",
        "en_US-ryan-low.onnx",
        "en_US-joe-medium.onnx",
        "en_GB-alan-medium.onnx",
        "en_GB-northern_english_male-medium.onnx",
        "en_US-male-medium.onnx"
    };
    // Candidats féminins anglais (lessac d'abord, déjà fourni).
    private static readonly string[] EnglishFemaleCandidates =
    {
        "en_US-lessac-medium.onnx",
        "en_US-lessac-high.onnx",
        "en_US-amy-medium.onnx",
        "en_US-hfc_female-medium.onnx",
        "en_GB-jenny_dioco-medium.onnx"
    };

    private static readonly string[] ArabicMaleCandidates =
    {
        "ar_SA-miro-medium.onnx",
        "ar_JO-kareem-medium.onnx",
        "ar_JO-kareem-low.onnx",
        "ar_SA-miro-high.onnx",
        "ar_JO-SA_miro-high.onnx",
        "ar_SA-miro-low.onnx",
        "arabic-male-model.onnx",
        "ar_male-medium.onnx"
    };

    // Support voix arabe féminine : ne bloque jamais si le fichier n'existe pas.
    // Il suffit de déposer un couple .onnx + .onnx.json avec l'un de ces noms.
    private static readonly string[] ArabicFemaleCandidates =
    {
        "ar_SA-dii-medium.onnx",
        "arabic-emirati-female-model.onnx",
        "voix-arabe-feminine.onnx",
        "ar_SA-dii-low.onnx",
        "ar_SA-miro-medium.onnx",
        "ar_SA-miro-low.onnx",
        "ar_AR-female-medium.onnx",
        "ar_female-medium.onnx"
    };

    // Le 1er appel charge le modèle ONNX en mémoire (~2-3 s à froid sur un PC
    // modeste). L'ancien 8 s pouvait être dépassé sur la toute première
    // synthèse arabe, ce qui faisait échouer 1 annonce sur 2 au démarrage.
    private const int FixedTimeoutMs = 15000;
    private const int ProbeTimeoutMs = 12000;

    private readonly ILogger<PiperTtsService> _logger;
    private readonly string _piperDir;
    private readonly string _cacheDir;
    // On autorise jusqu'à 2 synthèses Piper en parallèle : sur PC multi-cœur
    // cela évite qu'une annonce FR bloque une annonce AR demandée juste après
    // (cause classique du « une fois sur deux »). Piper est un process isolé,
    // il n'y a pas d'état partagé à protéger entre deux synthèses.
    private readonly SemaphoreSlim _runLock = new(2, 2);
    // Le probe (vérification « ce modèle se charge-t-il ? ») a son PROPRE verrou
    // pour ne JAMAIS bloquer une vraie synthèse en cours.
    private readonly SemaphoreSlim _probeLock = new(1, 1);
    private readonly object _probeCacheLock = new();
    private readonly Dictionary<string, bool> _synthesisProbeCache = new(StringComparer.OrdinalIgnoreCase);
    private int _warmedUp;

    public PiperTtsService(ILogger<PiperTtsService> logger, IWebHostEnvironment env, AppStoragePaths storagePaths)
    {
        _logger = logger;
        var webRoot = !string.IsNullOrEmpty(env.WebRootPath)
            ? env.WebRootPath
            : Path.Combine(env.ContentRootPath, "wwwroot");

        // Les modèles + piper.exe sont lus depuis wwwroot (lecture seule OK).
        _piperDir = Path.Combine(webRoot, "tts", "piper");

        // CORRECTIF CRITIQUE : le cache TTS (où Piper ÉCRIT le WAV généré) doit
        // être dans un dossier accessible en ÉCRITURE. En installation
        // « Program Files », wwwroot est en lecture seule pour un utilisateur
        // standard → Piper ne pouvait pas écrire son fichier de sortie et le
        // logiciel basculait sur la voix du navigateur. On écrit donc le cache
        // dans le DataRoot (ex. C:\ProgramData\Nouba), toujours accessible.
        var cacheDir = Path.Combine(storagePaths.DataRoot, "tts-cache");
        try { Directory.CreateDirectory(cacheDir); }
        catch
        {
            cacheDir = Path.Combine(Path.GetTempPath(), "nouba-tts-cache");
            try { Directory.CreateDirectory(cacheDir); } catch { }
        }
        _cacheDir = cacheDir;
    }

    /// <summary>
    /// Pré-chauffe Piper au démarrage : on lance une synthèse silencieuse pour
    /// chaque langue/voix disponible afin que le modèle ONNX soit déjà chargé
    /// en mémoire quand le premier vrai ticket est appelé. Sans ça, la toute
    /// première annonce (souvent l'arabe) dépassait le timeout côté navigateur
    /// et basculait en voix de secours — d'où l'impression « une fois sur deux ».
    /// Idempotent et silencieux : n'échoue jamais le démarrage de l'app.
    /// </summary>
    public async Task WarmUpAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _warmedUp, 1) == 1) return;
        if (!File.Exists(GetBinaryPath())) return;

        // Texte court par langue, juste de quoi forcer le chargement du modèle.
        // On pré-chauffe les 6 voix (chaque langue × chaque genre) pour que la
        // toute première annonce soit instantanée quel que soit le réglage.
        var jobs = new (string lang, string? gender)[]
        {
            ("fr", "female"),
            ("fr", "male"),
            ("en", "female"),
            ("en", "male"),
            ("ar", "female"),
            ("ar", "male")
        };

        foreach (var (lang, gender) in jobs)
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                if (!IsLanguageAvailable(lang, gender)) continue;
                var probe = GetProbeText(lang);
                _ = await SynthesizeAsync(probe, lang, gender, ct);
                _logger.LogInformation("Piper TTS : modèle pré-chargé pour {Lang}/{Gender}.", lang, gender ?? "auto");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Piper TTS : pré-chauffe {Lang}/{Gender} ignorée.", lang, gender ?? "auto");
            }
        }
    }

    public bool IsAvailable()
    {
        if (!File.Exists(GetBinaryPath())) return false;
        return IsLanguageAvailable("fr") || IsLanguageAvailable("en") || IsLanguageAvailable("ar");
    }

    public bool IsLanguageAvailable(string lang, string? gender = null)
    {
        if (!File.Exists(GetBinaryPath())) return false;
        var safe = NormalizeLang(lang);
        if (safe == "ar")
        {
            var requested = (gender ?? string.Empty).Trim().ToLowerInvariant();
            var female = GetArabicFemaleModelPath();
            var male = GetMaleArabicModelPath();
            return requested switch
            {
                "female" => IsSynthesizableModel(female, "ar"),
                "male" => IsSynthesizableModel(male, "ar"),
                _ => IsSynthesizableModel(male, "ar") || IsSynthesizableModel(female, "ar")
            };
        }

        return IsSynthesizableModel(GetModelPath(safe, gender), safe);
    }

    public object GetStatus()
    {
        var binaryPath = GetBinaryPath();
        var binaryOk = File.Exists(binaryPath);
        var frFemale = ResolveVoice("fr", "female");
        var frMale = ResolveVoice("fr", "male");
        var enFemale = ResolveVoice("en", "female");
        var enMale = ResolveVoice("en", "male");
        var arMalePath = GetMaleArabicModelPath();
        var arFemalePath = GetArabicFemaleModelPath();
        var frFemaleReady = binaryOk && IsSynthesizableModel(frFemale.Path, "fr");
        var frMaleReady = binaryOk && frMale.Gender == "male" && IsSynthesizableModel(frMale.Path, "fr");
        var enFemaleReady = binaryOk && IsSynthesizableModel(enFemale.Path, "en");
        var enMaleReady = binaryOk && enMale.Gender == "male" && IsSynthesizableModel(enMale.Path, "en");
        var arMaleFileOk = IsValidModel(arMalePath);
        var arFemaleFileOk = IsValidModel(arFemalePath);
        var arMaleReady = binaryOk && IsSynthesizableModel(arMalePath, "ar");
        var arFemaleReady = binaryOk && IsSynthesizableModel(arFemalePath, "ar");
        return new
        {
            available = binaryOk && (frFemaleReady || frMaleReady || enFemaleReady || enMaleReady || arMaleReady || arFemaleReady),
            binary = new { path = binaryPath, exists = binaryOk },
            fr = new {
                ready = frFemaleReady || frMaleReady,
                female = frFemaleReady,
                male = frMaleReady,
                gender = string.Join("+", new[] { frFemaleReady ? "female" : "", frMaleReady ? "male" : "" }.Where(x => !string.IsNullOrEmpty(x))),
                path = frFemale.Path,
                onnxExists = File.Exists(frFemale.Path ?? ""),
                jsonExists = File.Exists((frFemale.Path ?? "") + ".json"),
                maleProblem = (frFemaleReady && !frMaleReady) ? "Modèle FR mono-locuteur : voix masculine indisponible (utilisez fr_FR-upmc-medium qui contient les 2 voix)." : ""
            },
            en = new {
                ready = enFemaleReady || enMaleReady,
                female = enFemaleReady,
                male = enMaleReady,
                gender = string.Join("+", new[] { enFemaleReady ? "female" : "", enMaleReady ? "male" : "" }.Where(x => !string.IsNullOrEmpty(x))),
                femalePath = enFemale.Path,
                malePath = enMale.Gender == "male" ? enMale.Path : null,
                maleProblem = (enFemaleReady && !enMaleReady) ? "Voix masculine anglaise absente : ajoutez en_US-ryan-medium.onnx (+ .json)." : ""
            },
            ar = new
            {
                ready = arMaleReady || arFemaleReady,
                male = arMaleReady,
                female = arFemaleReady,
                gender = string.Join("+", new[] { arFemaleReady ? "female" : "", arMaleReady ? "male" : "" }.Where(x => !string.IsNullOrEmpty(x))),
                malePath = arMalePath,
                maleFileOk = arMaleFileOk,
                maleOnnxExists = File.Exists(arMalePath),
                maleJsonExists = File.Exists(arMalePath + ".json"),
                maleProblem = arMaleFileOk && !arMaleReady ? "Le fichier existe mais Piper ne le charge pas." : "",
                femalePath = arFemalePath,
                femaleFileOk = arFemaleFileOk,
                femaleOnnxExists = File.Exists(arFemalePath),
                femaleJsonExists = File.Exists(arFemalePath + ".json"),
                femaleProblem = arFemaleFileOk && !arFemaleReady ? "Le fichier existe mais Piper ne le charge pas." : ""
            }
        };
    }

    public string GetModelGender(string lang, string? requestedGender = null)
    {
        // Renvoie le genre réellement servi (utile à l'UI / diagnostic).
        return ResolveVoice(lang, requestedGender).Gender;
    }

    public Task<byte[]?> SynthesizeAsync(string text, string lang, CancellationToken ct = default)
        => SynthesizeAsync(text, lang, null, (double?)null, ct);

    public Task<byte[]?> SynthesizeAsync(string text, string lang, string? requestedGender, CancellationToken ct = default)
        => SynthesizeAsync(text, lang, requestedGender, (double?)null, ct);

    /// <summary>
    /// <paramref name="lengthScaleOverride"/> : si fourni et compris entre 0.5
    /// et 2.0, force le débit Piper (--length-scale) au lieu de la valeur par
    /// défaut spécifique au genre (voir ResolveLengthScale). Utilisé par la page
    /// de réglage /tts-tuning.html pour ajuster la vitesse à l'oreille sans
    /// recompiler. La clé de cache inclut déjà le length-scale : pas de collision.
    /// </summary>
    public async Task<byte[]?> SynthesizeAsync(string text, string lang, string? requestedGender, double? lengthScaleOverride, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        if (text.Length > 500) text = text[..500];

        var binaryPath = GetBinaryPath();
        if (!File.Exists(binaryPath))
        {
            _logger.LogWarning("Piper TTS : binaire introuvable à {Path}.", binaryPath);
            return null;
        }

        var voice = ResolveVoice(lang, requestedGender);
        var modelPath = voice.Path;
        if (!IsValidModel(modelPath))
        {
            _logger.LogWarning("Piper TTS : modèle absent ou invalide pour lang={Lang} gender={Gender} (path attendu : {Path}).", lang, requestedGender, modelPath);
            return null;
        }

        // La clé de cache DOIT inclure le genre/locuteur, sinon la voix
        // masculine et la voix féminine d'une même langue (ex. FR jessica vs
        // pierre, même fichier) partageraient le même WAV → mauvaise voix servie.
        // v2.7.46 — Le length-scale arabe est désormais spécifique au genre
        // (voir ResolveLengthScale ci-dessous) : le masculin et le féminin
        // n'ont pas le même débit naturel, leur appliquer le même facteur de
        // ralentissement uniforme convenait à l'un et rendait l'autre trop
        // lent/peu naturel.
        var lengthScale = (lengthScaleOverride is double ov && ov >= 0.5 && ov <= 2.0)
            ? ov.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture)
            : ResolveLengthScale(NormalizeLang(lang), voice.Gender);
        var voiceTag = GetModelFileName(modelPath) + "|g=" + voice.Gender + "|s=" + (voice.Speaker?.ToString() ?? "-") + "|ls=" + lengthScale;
        var cacheKey = ComputeCacheKey(text, NormalizeLang(lang), voiceTag);
        var cachePath = Path.Combine(_cacheDir, cacheKey + ".wav");
        if (File.Exists(cachePath))
        {
            try { return await File.ReadAllBytesAsync(cachePath, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Cache TTS illisible."); }
        }

        await _runLock.WaitAsync(ct);
        var tmpWavPath = Path.Combine(_cacheDir, "_tmp_" + Guid.NewGuid().ToString("N") + ".wav");
        var tmpMoved = false;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = binaryPath,
                WorkingDirectory = _piperDir,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8
            };
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(modelPath!);
            if (voice.Speaker.HasValue)
            {
                psi.ArgumentList.Add("--speaker");
                psi.ArgumentList.Add(voice.Speaker.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            // Arabe : on ralentit le débit du modèle FÉMININ (length-scale
            // 1.15) car les modèles arabes libres parlent vite, ce qui nuit
            // à l'intelligibilité. Le MASCULIN (kareem-medium) a déjà un
            // débit naturellement plus lent et moins articulé : lui imposer
            // le même ralentissement le rendait pénible à écouter. Voir
            // ResolveLengthScale. FR/EN gardent leur débit naturel (déjà bons).
            if (lengthScale != "1.0")
            {
                psi.ArgumentList.Add("--length-scale");
                psi.ArgumentList.Add(lengthScale);
            }
            psi.ArgumentList.Add("--output_file");
            psi.ArgumentList.Add(tmpWavPath);

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            await proc.StandardInput.WriteAsync(text);
            proc.StandardInput.Close();

            var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(FixedTimeoutMs);
            try { await proc.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                try { proc.Kill(true); } catch { }
                _logger.LogWarning("Piper timeout.");
                return null;
            }

            try { _ = stdoutTask.GetAwaiter().GetResult(); } catch { }

            if (proc.ExitCode != 0)
            {
                string err;
                try { err = await stderrTask; } catch { err = string.Empty; }
                _logger.LogWarning("Piper exit {Code}: {Err}", proc.ExitCode, err.Length > 300 ? err[..300] : err);
                return null;
            }

            if (!File.Exists(tmpWavPath)) return null;
            var wav = await File.ReadAllBytesAsync(tmpWavPath, ct);
            if (!IsValidWav(wav))
            {
                _logger.LogWarning("Piper a produit un fichier non WAV/corrompu.");
                return null;
            }

            try
            {
                File.Move(tmpWavPath, cachePath, true);
                tmpMoved = true;
                CleanupCacheIfNeeded();
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Cache TTS non écrit."); }
            return wav;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur synthèse Piper.");
            return null;
        }
        finally
        {
            try { if (!tmpMoved && File.Exists(tmpWavPath)) File.Delete(tmpWavPath); } catch { }
            _runLock.Release();
        }
    }

    private string GetBinaryPath()
    {
        var fileName = OperatingSystem.IsWindows() ? PiperBinaryWindows : PiperBinaryUnix;
        return Path.Combine(_piperDir, fileName);
    }

    private string? GetModelPath(string lang, string? requestedGender)
        => ResolveVoice(lang, requestedGender).Path;

    /// <summary>
    /// v2.7.46 — Facteur de ralentissement Piper (--length-scale), spécifique
    /// au genre pour l'arabe. Le modèle féminin (arabic-emirati-female) parle
    /// vite par défaut → on le ralentit (1.15) pour rester intelligible. Le
    /// modèle masculin (ar_JO-kareem-medium) a au contraire un débit déjà lent
    /// et peu articulé nativement : lui appliquer le même ralentissement
    /// rendait l'écoute pénible. On le laisse donc à son débit natif (1.0).
    /// Valeur à ajuster par essais successifs si besoin — impossible pour moi
    /// de juger à l'oreille depuis cet environnement.
    /// </summary>
    private static string ResolveLengthScale(string normalizedLang, string gender)
    {
        if (normalizedLang != "ar") return "1.0";
        return gender == "male" ? "1.0" : "1.15";
    }

    /// <summary>
    /// Résout le modèle, le locuteur (--speaker) et le genre réellement servi
    /// pour une langue + un genre demandé. Couvre les 6 voix :
    /// FR f/m (1 fichier, 2 locuteurs), EN f/m (2 fichiers), AR f/m (2 fichiers).
    /// </summary>
    private VoiceResolution ResolveVoice(string lang, string? requestedGender)
    {
        var safe = NormalizeLang(lang);
        var g = (requestedGender ?? "").Trim().ToLowerInvariant();
        if (g != "male" && g != "female") g = "";

        if (safe == "fr")
        {
            // Un seul fichier, deux locuteurs. Le masculin n'est dispo que si
            // le modèle est bien multi-locuteurs (sécurité si un mono est posé).
            var path = Path.Combine(_piperDir, ModelFileFr);
            if (!IsValidModel(path)) return VoiceResolution.None;
            var male = g == "male" && ModelHasMultipleSpeakers(path);
            return new VoiceResolution(path, male ? FrSpeakerMale : FrSpeakerFemale, male ? "male" : "female");
        }

        if (safe == "en")
        {
            var femalePath = FindFirstValidModel(EnglishFemaleCandidates);
            var malePath = FindFirstValidModel(EnglishMaleCandidates);
            if (g == "male")
            {
                if (malePath != null) return new VoiceResolution(malePath, SpeakerFor(malePath, true), "male");
                if (femalePath != null) return new VoiceResolution(femalePath, null, "female"); // repli
            }
            else
            {
                if (femalePath != null) return new VoiceResolution(femalePath, null, "female");
                if (malePath != null) return new VoiceResolution(malePath, SpeakerFor(malePath, true), "male"); // repli
            }
            return VoiceResolution.None;
        }

        if (safe == "ar")
        {
            var female = GetArabicFemaleModelPath();
            var male = GetMaleArabicModelPath();
            var femaleReady = IsSynthesizableModel(female, "ar");
            var maleReady = IsSynthesizableModel(male, "ar");
            if (g == "female")
            {
                if (femaleReady) return new VoiceResolution(female, null, "female");
                if (maleReady) return new VoiceResolution(male, null, "male");
            }
            else if (g == "male")
            {
                if (maleReady) return new VoiceResolution(male, null, "male");
                if (femaleReady) return new VoiceResolution(female, null, "female");
            }
            else
            {
                if (maleReady) return new VoiceResolution(male, null, "male");
                if (femaleReady) return new VoiceResolution(female, null, "female");
            }
            return VoiceResolution.None;
        }

        return VoiceResolution.None;
    }

    /// <summary>Locuteur masculin si le modèle a plusieurs voix, sinon défaut.</summary>
    private int? SpeakerFor(string modelPath, bool male)
        => ModelHasMultipleSpeakers(modelPath) ? (male ? 1 : 0) : (int?)null;

    /// <summary>Lit num_speakers dans le .onnx.json (mis en cache).</summary>
    private readonly Dictionary<string, bool> _multiSpeakerCache = new(StringComparer.OrdinalIgnoreCase);
    private bool ModelHasMultipleSpeakers(string? modelPath)
    {
        if (string.IsNullOrEmpty(modelPath)) return false;
        lock (_multiSpeakerCache)
        {
            if (_multiSpeakerCache.TryGetValue(modelPath, out var cached)) return cached;
        }
        var result = false;
        try
        {
            var jsonPath = modelPath + ".json";
            if (File.Exists(jsonPath))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(jsonPath));
                if (doc.RootElement.TryGetProperty("num_speakers", out var ns)
                    && ns.TryGetInt32(out var n))
                {
                    result = n > 1;
                }
            }
        }
        catch { result = false; }
        lock (_multiSpeakerCache)
        {
            _multiSpeakerCache[modelPath] = result;
        }
        return result;
    }

    private readonly record struct VoiceResolution(string? Path, int? Speaker, string Gender)
    {
        public static VoiceResolution None => new(null, null, "female");
    }

    private string? GetMaleArabicModelPath()
        => FindFirstValidModel(ArabicMaleCandidates) ?? DiscoverArabicModel(preferFemale: false);

    private string? GetArabicFemaleModelPath()
        => FindFirstValidModel(ArabicFemaleCandidates) ?? DiscoverArabicModel(preferFemale: true);

    private string? FindFirstValidModel(IEnumerable<string> candidates)
    {
        foreach (var name in candidates)
        {
            var path = Path.Combine(_piperDir, name);
            if (IsValidModel(path)) return path;
        }
        return null;
    }

    private string? DiscoverArabicModel(bool preferFemale)
    {
        if (!Directory.Exists(_piperDir)) return null;

        var files = Directory.EnumerateFiles(_piperDir, "*.onnx", SearchOption.TopDirectoryOnly)
            .Where(path => IsArabicModelName(Path.GetFileName(path)) && IsValidModel(path))
            .ToList();

        if (preferFemale)
        {
            return files
                .Where(path => IsFemaleArabicName(Path.GetFileName(path)))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        return files
            .Where(path => !IsFemaleArabicName(Path.GetFileName(path)))
            .OrderByDescending(path => IsMaleArabicName(Path.GetFileName(path)) ? 1 : 0)
            .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool IsArabicModelName(string? fileName)
    {
        var name = (fileName ?? string.Empty).ToLowerInvariant();
        return name.StartsWith("ar_")
               || name.StartsWith("ar-")
               || name.Contains("arabic")
               || name.Contains("arabe");
    }

    private static bool IsFemaleArabicName(string? fileName)
    {
        var name = (fileName ?? string.Empty).ToLowerInvariant();
        return name.Contains("female")
               || name.Contains("feminine")
               || name.Contains("feminin")
               || name.Contains("femme");
    }

    private static bool IsMaleArabicName(string? fileName)
    {
        var name = (fileName ?? string.Empty).ToLowerInvariant();
        return name.Contains("male")
               || name.Contains("masculine")
               || name.Contains("masculin")
               || name.Contains("kareem")
               || name.Contains("miro");
    }

    private static bool IsValidModel(string? modelPath)
        => !string.IsNullOrWhiteSpace(modelPath)
           && File.Exists(modelPath)
           && File.Exists(modelPath + ".json");

    private bool IsSynthesizableModel(string? modelPath, string lang)
    {
        if (!IsValidModel(modelPath) || !File.Exists(GetBinaryPath())) return false;

        var cacheKey = BuildProbeCacheKey(modelPath!);
        lock (_probeCacheLock)
        {
            if (_synthesisProbeCache.TryGetValue(cacheKey, out var cached)) return cached;
        }

        var ok = ProbeModel(modelPath!, GetProbeText(lang));
        lock (_probeCacheLock)
        {
            _synthesisProbeCache[cacheKey] = ok;
        }
        return ok;
    }

    private bool ProbeModel(string modelPath, string text)
    {
        var lockTaken = false;
        var tmpWavPath = Path.Combine(_cacheDir, "_probe_" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            lockTaken = _probeLock.Wait(ProbeTimeoutMs);
            if (!lockTaken)
            {
                _logger.LogWarning("Piper TTS : probe impossible, moteur occupé trop longtemps pour {Model}.", Path.GetFileName(modelPath));
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = GetBinaryPath(),
                WorkingDirectory = _piperDir,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8
            };
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(modelPath);
            psi.ArgumentList.Add("--output_file");
            psi.ArgumentList.Add(tmpWavPath);

            using var proc = Process.Start(psi);
            if (proc is null) return false;

            proc.StandardInput.Write(text);
            proc.StandardInput.Close();

            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit(ProbeTimeoutMs))
            {
                try { proc.Kill(true); } catch { }
                _logger.LogWarning("Piper TTS : probe timeout pour {Model}.", Path.GetFileName(modelPath));
                return false;
            }

            if (proc.ExitCode != 0)
            {
                string err;
                try { err = stderrTask.GetAwaiter().GetResult(); } catch { err = string.Empty; }
                _logger.LogWarning("Piper TTS : modèle non synthétisable {Model}, exit={Code}, err={Err}.",
                    Path.GetFileName(modelPath),
                    proc.ExitCode,
                    err.Length > 220 ? err[..220] : err);
                return false;
            }

            if (!File.Exists(tmpWavPath)) return false;
            var wav = File.ReadAllBytes(tmpWavPath);
            return IsValidWav(wav);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Piper TTS : probe échoué pour {Model}.", Path.GetFileName(modelPath));
            return false;
        }
        finally
        {
            try { if (File.Exists(tmpWavPath)) File.Delete(tmpWavPath); } catch { }
            if (lockTaken) _probeLock.Release();
        }
    }

    private string BuildProbeCacheKey(string modelPath)
    {
        var binaryPath = GetBinaryPath();
        var modelInfo = new FileInfo(modelPath);
        var jsonInfo = new FileInfo(modelPath + ".json");
        var binaryInfo = new FileInfo(binaryPath);
        return string.Join("|",
            binaryPath,
            binaryInfo.Exists ? binaryInfo.LastWriteTimeUtc.Ticks : 0,
            binaryInfo.Exists ? binaryInfo.Length : 0,
            modelPath,
            modelInfo.LastWriteTimeUtc.Ticks,
            modelInfo.Length,
            jsonInfo.LastWriteTimeUtc.Ticks,
            jsonInfo.Length);
    }

    private static string GetProbeText(string lang)
        => NormalizeLang(lang) switch
        {
            "ar" => "اختبار",
            "en" => "Test.",
            _ => "Test."
        };

    private static bool IsValidWav(byte[]? wav)
        => wav is { Length: >= 44 }
           && wav[0] == (byte)'R' && wav[1] == (byte)'I' && wav[2] == (byte)'F' && wav[3] == (byte)'F'
           && wav[8] == (byte)'W' && wav[9] == (byte)'A' && wav[10] == (byte)'V' && wav[11] == (byte)'E';

    private static string NormalizeLang(string? lang)
    {
        var value = (lang ?? "fr").Trim().ToLowerInvariant();
        return value is "fr" or "en" or "ar" ? value : "fr";
    }

    private static string GetModelFileName(string? modelPath)
        => string.IsNullOrWhiteSpace(modelPath) ? "fallback" : Path.GetFileName(modelPath);

    private static string ComputeCacheKey(string text, string lang, string modelFile)
    {
        using var sha = SHA1.Create();
        var input = $"{lang}|{modelFile}|{text}";
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private void CleanupCacheIfNeeded()
    {
        try
        {
            var dir = new DirectoryInfo(_cacheDir);
            var files = dir.GetFiles("*.wav");
            if (files.Length > 120)
            {
                foreach (var f in files.OrderBy(f => f.LastAccessTimeUtc).Take(files.Length / 3))
                    try { f.Delete(); } catch { }
            }
            foreach (var f in dir.GetFiles("_tmp_*.wav"))
                try { if ((DateTime.UtcNow - f.LastWriteTimeUtc).TotalMinutes > 5) f.Delete(); } catch { }
        }
        catch { }
    }
}
