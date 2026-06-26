using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Models;

namespace Nouba.Services;

/// <summary>
/// Service « Intelligence Artificielle » 100 % offline pour Nouba.
///
/// Stratégie : pas de modèle de deep learning embarqué (pour rester léger et
/// sans dépendances externes), mais des algorithmes statistiques et lexicaux
/// qui produisent des résultats utiles et professionnels.
///
/// Quatre fonctions sont implémentées pleinement :
///   1. Résumé quotidien intelligent (template avec données réelles)
///   2. Rapport textuel professionnel FR (long format)
///   3. Suggestion de routage chatbot borne (matching par mots-clés)
///   4. Traduction services basique (dictionnaire embarqué FR ↔ AR/TZ/EN)
///
/// Pour le TTS naturel (Piper / Coqui XTTS), un point d'extension est exposé
/// mais nécessite un binaire externe — voir <see cref="GenerateNaturalSpeechAsync"/>.
/// </summary>
public sealed class NoubaAiService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NoubaAiService> _logger;

    public NoubaAiService(AppDbContext db, ILogger<NoubaAiService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════
    // 1. RÉSUMÉ QUOTIDIEN INTELLIGENT
    // ════════════════════════════════════════════════════════════════
    public async Task<DailySummary> BuildDailySummaryAsync(DateTime? day = null, CancellationToken ct = default)
    {
        var date = (day ?? DateTime.Today).Date;
        var nextDay = date.AddDays(1);

        var tickets = await _db.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= date && t.CreatedAt < nextDay)
            .Include(t => t.ServiceType)
            .ToListAsync(ct);

        var calls = await _db.CallHistories.AsNoTracking()
            .Where(c => c.CalledAt >= date && c.CalledAt < nextDay)
            .ToListAsync(ct);

        var summary = new DailySummary { Date = date };
        if (tickets.Count == 0)
        {
            summary.HeadlineFr = $"Aucun ticket le {date:dd/MM/yyyy}.";
            return summary;
        }

        // Statistiques de base
        int total   = tickets.Count;
        int served  = tickets.Count(t => t.Status == TicketStatus.Finished);
        int absent  = tickets.Count(t => t.Status == TicketStatus.Absent);
        int prio    = tickets.Count(t => t.IsPriority);

        // Pic d'activité
        var byHour = tickets.GroupBy(t => t.CreatedAt.Hour)
                            .ToDictionary(g => g.Key, g => g.Count());
        var (peakHour, peakCount) = byHour.OrderByDescending(kv => kv.Value).FirstOrDefault();

        // Service le plus demandé
        var topService = tickets.Where(t => t.ServiceType != null)
                                .GroupBy(t => t.ServiceType!.Name)
                                .OrderByDescending(g => g.Count())
                                .FirstOrDefault();

        // Agent le plus rapide : on calcule l'écart moyen entre 2 appels consécutifs
        // d'un même agent (puisqu'on n'a pas FinishedAt). C'est une bonne approximation
        // du temps de traitement moyen.
        var agentSpeeds = calls
            .Where(c => !string.IsNullOrEmpty(c.AgentName))
            .OrderBy(c => c.CalledAt)
            .GroupBy(c => c.AgentName!)
            .Select(g =>
            {
                var ordered = g.OrderBy(c => c.CalledAt).ToList();
                var deltas = new List<double>();
                for (int i = 1; i < ordered.Count; i++)
                {
                    var d = (ordered[i].CalledAt - ordered[i - 1].CalledAt).TotalSeconds;
                    if (d > 10 && d < 1800) deltas.Add(d); // filtre [10s, 30min]
                }
                return new
                {
                    Agent = g.Key,
                    AvgSec = deltas.Count > 0 ? deltas.Average() : 0,
                    Count = ordered.Count
                };
            })
            .Where(x => x.Count >= 3 && x.AvgSec > 0)
            .OrderBy(x => x.AvgSec)
            .ToList();
        var fastestAgent = agentSpeeds.FirstOrDefault();

        // Construction du headline
        summary.Total       = total;
        summary.Served      = served;
        summary.Absent      = absent;
        summary.PriorityCount = prio;
        summary.PeakHour    = peakHour;
        summary.PeakCount   = peakCount;
        summary.TopServiceName = topService?.Key;
        summary.TopServiceCount = topService?.Count() ?? 0;
        summary.FastestAgentName = fastestAgent?.Agent;
        summary.FastestAgentAvgSeconds = (int?)fastestAgent?.AvgSec;
        summary.AbsenceRate = total > 0 ? (absent * 100.0 / total) : 0;

        // Résumé en français pro (1 phrase percutante)
        var parts = new List<string>();
        parts.Add($"{total} ticket{Plural(total)} pris en charge");
        if (peakCount > 0)
            parts.Add($"pic à {peakHour:00}h ({peakCount} tickets)");
        if (topService != null)
            parts.Add($"« {topService.Key} » est le service le plus demandé ({topService.Count()} tickets)");
        if (fastestAgent != null)
            parts.Add($"{fastestAgent.Agent} est l'agent le plus rapide ({(int)fastestAgent.AvgSec}s/ticket en moyenne)");
        summary.HeadlineFr = "Aujourd'hui : " + string.Join(", ", parts) + ".";

        // Recommandations actionnables
        var recos = new List<string>();
        if (summary.AbsenceRate > 20)
            recos.Add($"Taux d'absence élevé ({summary.AbsenceRate:F0}%). Pensez à activer le suivi mobile par QR code pour que les clients suivent leur file à distance.");
        if (peakCount > total / 3)
            recos.Add($"Forte concentration à {peakHour:00}h. Renforcez les guichets sur ce créneau.");
        if (served < total * 0.6 && served + absent < total)
            recos.Add($"{total - served - absent} tickets non traités en fin de journée. Élargissez les horaires si récurrent.");
        summary.Recommendations = recos;

        return summary;
    }

    // ════════════════════════════════════════════════════════════════
    // 2. RAPPORT TEXTUEL PROFESSIONNEL FR
    // ════════════════════════════════════════════════════════════════
    public async Task<string> GenerateProfessionalReportAsync(DateTime? day = null, CancellationToken ct = default)
    {
        var s = await BuildDailySummaryAsync(day, ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"RAPPORT D'ACTIVITÉ — {s.Date:dddd dd MMMM yyyy}");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine();

        if (s.Total == 0)
        {
            sb.AppendLine("Aucune activité enregistrée sur cette journée.");
            return sb.ToString();
        }

        sb.AppendLine("SYNTHÈSE");
        sb.AppendLine("--------");
        sb.AppendLine($"L'établissement a traité {s.Total} ticket{Plural(s.Total)} ce jour, dont {s.Served} ont été servis ({Pct(s.Served, s.Total)}%) et {s.Absent} se sont avérés absents ({Pct(s.Absent, s.Total)}%). " +
                      $"Le taux d'absentéisme s'établit à {s.AbsenceRate:F1}%, " +
                      (s.AbsenceRate > 20 ? "ce qui est supérieur à la norme acceptable." : "ce qui est dans la norme.") );
        if (s.PriorityCount > 0)
            sb.AppendLine($"{s.PriorityCount} ticket{Plural(s.PriorityCount)} prioritaire{Plural(s.PriorityCount)} ({Pct(s.PriorityCount, s.Total)}%) ont été pris en charge.");
        sb.AppendLine();

        sb.AppendLine("ACTIVITÉ");
        sb.AppendLine("--------");
        if (s.PeakCount > 0)
            sb.AppendLine($"Le pic d'affluence est survenu entre {s.PeakHour:00}h et {(s.PeakHour + 1):00}h avec {s.PeakCount} tickets.");
        if (!string.IsNullOrEmpty(s.TopServiceName))
            sb.AppendLine($"Le service « {s.TopServiceName} » a généré le plus grand volume avec {s.TopServiceCount} ticket{Plural(s.TopServiceCount)} ({Pct(s.TopServiceCount, s.Total)}% du total).");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(s.FastestAgentName))
        {
            sb.AppendLine("PERFORMANCE AGENTS");
            sb.AppendLine("------------------");
            var min = s.FastestAgentAvgSeconds! / 60; var sec = s.FastestAgentAvgSeconds % 60;
            sb.AppendLine($"L'agent {s.FastestAgentName} affiche le temps de traitement moyen le plus rapide : {min}m{sec:00}s par ticket.");
            sb.AppendLine();
        }

        if (s.Recommendations.Count > 0)
        {
            sb.AppendLine("RECOMMANDATIONS");
            sb.AppendLine("---------------");
            for (int i = 0; i < s.Recommendations.Count; i++)
                sb.AppendLine($"{i+1}. {s.Recommendations[i]}");
            sb.AppendLine();
        }

        sb.AppendLine($"Rapport généré automatiquement par Nouba Pro IA — {DateTime.Now:dd/MM/yyyy HH:mm}.");
        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════
    // 3. CHATBOT BORNE — Routage par mots-clés
    // ════════════════════════════════════════════════════════════════
    /// <summary>
    /// Analyse une requête en langage naturel du client et propose le service
    /// le plus pertinent parmi ceux disponibles.
    /// Algorithme : tokenisation + matching pondéré sur nom, code et mots-clés.
    /// </summary>
    public async Task<ChatbotResponse> RouteCustomerQueryAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new ChatbotResponse { Confidence = 0, Reply = "Pouvez-vous préciser votre demande ?" };

        var services = await _db.Services.AsNoTracking()
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        if (!services.Any())
            return new ChatbotResponse { Confidence = 0, Reply = "Aucun service disponible." };

        var queryNorm = NormalizeText(query);
        var tokens = queryNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 2).ToArray();

        var scores = new List<(ServiceType svc, double score)>();
        foreach (var svc in services)
        {
            double score = 0;
            var nameNorm = NormalizeText(svc.Name);
            var codeNorm = NormalizeText(svc.Code ?? "");

            // Match exact ou inclusion → fort score
            if (nameNorm.Contains(queryNorm)) score += 100;
            if (queryNorm.Contains(nameNorm) && nameNorm.Length > 3) score += 80;

            // Match par tokens
            foreach (var tok in tokens)
            {
                if (nameNorm.Contains(tok)) score += 25;
                if (codeNorm == tok) score += 50;

                // Synonymes courants (extensible)
                foreach (var (key, syns) in Synonyms)
                {
                    if (syns.Contains(tok) && nameNorm.Contains(key)) score += 20;
                }
            }

            if (score > 0) scores.Add((svc, score));
        }

        var best = scores.OrderByDescending(x => x.score).FirstOrDefault();
        if (best.svc == null)
        {
            return new ChatbotResponse
            {
                Confidence = 0,
                Reply = "Je n'ai pas trouvé de service correspondant. Veuillez choisir directement sur la borne."
            };
        }

        // Confiance : 0..1 selon le score
        double conf = Math.Min(1.0, best.score / 100.0);
        return new ChatbotResponse
        {
            ServiceTypeId = best.svc.Id,
            ServiceName = best.svc.Name,
            Confidence = conf,
            Reply = conf > 0.6
                ? $"D'accord. Je vous oriente vers « {best.svc.Name} ». Confirmer ?"
                : $"Je pense que vous cherchez « {best.svc.Name} ». Est-ce correct ?"
        };
    }

    // Dictionnaire de synonymes basique (FR — extensible).
    private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["carte"]      = new[] { "passeport", "papiers", "identite", "cni" },
        ["renouveler"] = new[] { "renouvellement", "refaire", "prolonger", "nouvelle" },
        ["paiement"]   = new[] { "payer", "facture", "regler", "encaissement" },
        ["info"]       = new[] { "renseignement", "information", "demande" },
        ["depot"]      = new[] { "deposer", "remettre", "donner" },
        ["retrait"]    = new[] { "recuperer", "reprendre", "chercher" }
    };

    private static string NormalizeText(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var n = s.ToLowerInvariant().Trim();
        // Suppression des accents pour matcher plus largement
        var sb = new System.Text.StringBuilder();
        foreach (var c in n.Normalize(System.Text.NormalizationForm.FormD))
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    // ════════════════════════════════════════════════════════════════
    // 4. TRADUCTION DE SERVICES (basique, dictionnaire embarqué)
    // ════════════════════════════════════════════════════════════════
    /// <summary>
    /// Traduit le nom d'un service depuis le français vers AR/TZ/EN en utilisant
    /// un dictionnaire embarqué de termes administratifs courants.
    /// Si aucune traduction n'est trouvée, on renvoie le terme original
    /// (l'admin pourra l'éditer manuellement).
    /// </summary>
    public Dictionary<string, string> TranslateServiceName(string nameFr)
    {
        var lower = (nameFr ?? "").Trim().ToLowerInvariant();
        var result = new Dictionary<string, string>
        {
            ["fr"] = nameFr ?? "",
            ["ar"] = nameFr ?? "",
            ["tz"] = nameFr ?? "",
            ["en"] = nameFr ?? ""
        };

        if (string.IsNullOrEmpty(lower)) return result;

        // Dictionnaire de termes administratifs algériens courants.
        // Format : motif FR → (AR, TZ, EN). Le matching se fait sur "Contains".
        var terms = new (string Fr, string Ar, string Tz, string En)[]
        {
            ("carte d'identite",  "بطاقة التعريف",      "Takerṭa n yiman",       "ID card"),
            ("carte identite",    "بطاقة التعريف",      "Takerṭa n yiman",       "ID card"),
            ("passeport",         "جواز السفر",          "Aselkin n unekcum",     "Passport"),
            ("permis de conduire","رخصة السياقة",        "Tasureft n usewweq",    "Driving license"),
            ("permis",            "رخصة",                "Tasureft",              "License"),
            ("acte de naissance", "شهادة الميلاد",       "Aselkin n tlalit",      "Birth certificate"),
            ("acte de mariage",   "شهادة الزواج",        "Aselkin n uzewwej",     "Marriage certificate"),
            ("certificat",        "شهادة",               "Aselkin",               "Certificate"),
            ("declaration",       "تصريح",               "Tinawalt",              "Declaration"),
            ("paiement",          "الدفع",                "Aẓeṛqi",                "Payment"),
            ("facture",           "فاتورة",              "Tafatura",              "Invoice"),
            ("information",       "معلومات",             "Talɣut",                "Information"),
            ("renseignement",     "استعلامات",            "Talɣut",                "Information"),
            ("depot",             "إيداع",               "Aɛruḍ",                  "Deposit"),
            ("retrait",           "سحب",                  "Tukksa",                "Withdrawal"),
            ("renouvellement",    "تجديد",                "Tujjla",                "Renewal"),
            ("reservation",       "حجز",                  "Aḥraz",                 "Booking"),
            ("rendez-vous",       "موعد",                 "Aɣimi",                 "Appointment"),
            ("retraite",          "التقاعد",             "Asunfu",                "Retirement"),
            ("famille",           "العائلة",             "Twacult",               "Family"),
            ("entreprise",        "المؤسسة",             "Tamɣiwent",             "Company"),
            ("guichet",           "الشباك",              "Tasunt",                "Counter"),
            ("service",           "الخدمة",              "Ameẓluḍ",               "Service")
        };

        foreach (var t in terms)
        {
            if (lower.Contains(t.Fr))
            {
                result["ar"] = t.Ar;
                result["tz"] = t.Tz;
                result["en"] = t.En;
                return result;
            }
        }

        // Si rien ne match, on garde le FR partout. L'admin pourra éditer.
        return result;
    }

    // ════════════════════════════════════════════════════════════════
    // 5. TTS NATUREL (Piper, Coqui XTTS) — STUB DOCUMENTÉ
    // ════════════════════════════════════════════════════════════════
    /// <summary>
    /// Génère un fichier audio via un moteur TTS local (Piper, Coqui).
    /// IMPLÉMENTATION : nécessite un binaire externe à installer manuellement.
    /// Voir <c>docs/ia/PIPER_INSTALL.md</c> dans la livraison.
    ///
    /// Tant que le binaire n'est pas configuré, on retourne null et l'app
    /// retombe automatiquement sur le TTS du navigateur (déjà fonctionnel).
    /// </summary>
    public Task<byte[]?> GenerateNaturalSpeechAsync(string text, string lang, CancellationToken ct = default)
    {
        // Pour activer Piper : configurer le chemin du binaire dans appsettings
        // sous "Nouba:Ai:PiperPath" et placer les modèles .onnx dans le dossier.
        // Implémentation complète : exécuter "piper --model fr_FR-siwis-medium.onnx
        // --output_file out.wav" via Process.Start, lire le wav, le retourner.
        _logger.LogDebug("Piper TTS non configuré — fallback navigateur (Web Speech API).");
        return Task.FromResult<byte[]?>(null);
    }

    // ── Helpers ──
    private static string Plural(int n) => n > 1 ? "s" : "";
    private static int Pct(int part, int total) => total > 0 ? (int)Math.Round(part * 100.0 / total) : 0;
}

public sealed class DailySummary
{
    public DateTime Date { get; set; }
    public int Total { get; set; }
    public int Served { get; set; }
    public int Absent { get; set; }
    public int PriorityCount { get; set; }
    public int PeakHour { get; set; }
    public int PeakCount { get; set; }
    public string? TopServiceName { get; set; }
    public int TopServiceCount { get; set; }
    public string? FastestAgentName { get; set; }
    public int? FastestAgentAvgSeconds { get; set; }
    public double AbsenceRate { get; set; }
    public string HeadlineFr { get; set; } = "";
    public List<string> Recommendations { get; set; } = new();
}

public sealed class ChatbotResponse
{
    public int? ServiceTypeId { get; set; }
    public string? ServiceName { get; set; }
    public double Confidence { get; set; }
    public string Reply { get; set; } = "";
}
