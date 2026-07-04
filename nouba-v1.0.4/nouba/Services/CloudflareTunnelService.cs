using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Nouba.Infrastructure;

namespace Nouba.Services;

/// <summary>
/// Démarre cloudflared en arrière-plan si l'exécutable est présent.
/// Expose TunnelUrl dès que le tunnel est opérationnel ; TicketTrackingUrl
/// l'utilise comme fallback quand QrFollowPublicUrl n'est pas configuré.
///
/// Cycle de vie robuste (plan P2 — pas d'orphelin ni de doublon) :
///   • Job Object Windows « kill-on-close » : si Nouba est tué brutalement
///     (crash, taskkill /f, coupure de courant), Windows termine cloudflared
///     automatiquement → aucun processus orphelin ne subsiste.
///   • Fichier PID (cloudflared.pid) : au démarrage on termine le tunnel resté
///     d'une exécution précédente AVANT d'en lancer un nouveau → pas de doublon.
///     Garde-fous (nom du process + date de démarrage) : on ne touche JAMAIS au
///     cloudflared lancé par une autre application.
/// </summary>
public sealed class CloudflareTunnelService : BackgroundService
{
    private static readonly Regex _urlRx = new(
        @"https://[a-z0-9\-]+\.trycloudflare\.com",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CloudflareTunnelService> _logger;
    private readonly string _pidFile;

    // volatile : lu depuis n'importe quel thread HTTP
    private volatile string? _tunnelUrl;
    private volatile string _status = "initializing";

    // Job Object qui « possède » cloudflared ; fermé => cloudflared tué par Windows.
    private IntPtr _job = IntPtr.Zero;

    public string? TunnelUrl => _tunnelUrl;
    public string Status    => _status;

    public CloudflareTunnelService(IWebHostEnvironment env, ILogger<CloudflareTunnelService> logger, AppStoragePaths paths)
    {
        _env    = env;
        _logger = logger;
        _pidFile = Path.Combine(paths.DataRoot, "cloudflared.pid");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Laisser l'app démarrer complètement avant de lancer cloudflared.
        await Task.Delay(4_000, stoppingToken);

        var cfPath = FindCloudflared();
        if (cfPath == null)
        {
            _status = "not_found";
            _logger.LogInformation("cloudflared.exe introuvable — suivi QR limité au réseau local.");
            return;
        }

        _logger.LogInformation("cloudflared trouvé : {Path}", cfPath);

        // 1) Nettoyer un éventuel tunnel orphelin laissé par un crash précédent.
        KillStaleTunnel();
        // 2) Créer le Job Object qui garantit l'absence d'orphelin par la suite.
        EnsureJob();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _status = "starting";
                _tunnelUrl = null;

                try
                {
                    await RunTunnelAsync(cfPath, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur inattendue du tunnel Cloudflare.");
                    _status = "error";
                }

                _tunnelUrl = null;

                if (!stoppingToken.IsCancellationRequested)
                {
                    _status = "restarting";
                    _logger.LogWarning("Tunnel Cloudflare arrêté, redémarrage dans 30 s…");
                    await Task.Delay(30_000, stoppingToken);
                }
            }
        }
        finally
        {
            _status = "stopped";
            TryDeletePidFile();
        }
    }

    private async Task RunTunnelAsync(string cfPath, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = cfPath,
                Arguments = "tunnel --url http://localhost:5000 --no-autoupdate",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            }
        };

        void HandleLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            _logger.LogDebug("[cloudflared] {Line}", line);
            var m = _urlRx.Match(line);
            if (m.Success && _tunnelUrl == null)
            {
                _tunnelUrl = m.Value;
                _status = "running";
                _logger.LogInformation("Tunnel Cloudflare actif : {Url}", _tunnelUrl);
            }
        }

        process.OutputDataReceived += (_, e) => HandleLine(e.Data);
        process.ErrorDataReceived  += (_, e) => HandleLine(e.Data);

        process.Start();

        // Rattacher au Job Object (kill-on-close) + tracer le PID sur disque.
        AssignToJob(process);
        WritePidFile(process);

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Tuer le processus proprement à l'arrêt du service.
        await using var reg = ct.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch { }
        });

        try
        {
            await process.WaitForExitAsync(ct);
        }
        finally
        {
            // Le processus n'existe plus (mort ou tué) : le PID sur disque n'a
            // plus de raison d'être. En cas de kill BRUTAL de Nouba, ce finally
            // ne s'exécute pas et le fichier subsiste — c'est justement ce qui
            // permet au prochain démarrage de nettoyer l'orphelin.
            TryDeletePidFile();
        }
    }

    // ───────────────────────── Fichier PID ─────────────────────────

    private void WritePidFile(Process p)
    {
        try
        {
            File.WriteAllText(_pidFile, $"{p.Id}|{SafeStartTicks(p)}");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Écriture du PID tunnel ignorée.");
        }
    }

    private void TryDeletePidFile()
    {
        try { if (File.Exists(_pidFile)) File.Delete(_pidFile); } catch { }
    }

    /// <summary>
    /// Termine le tunnel cloudflared resté actif après un arrêt brutal précédent.
    /// Double garde-fou pour ne jamais tuer le cloudflared d'une AUTRE application :
    /// le processus visé doit s'appeler « cloudflared » ET avoir la même date de
    /// démarrage que celle enregistrée (sinon le PID a été réattribué).
    /// </summary>
    private void KillStaleTunnel()
    {
        try
        {
            if (!File.Exists(_pidFile)) return;
            var raw = File.ReadAllText(_pidFile).Trim();
            TryDeletePidFile(); // consommé, quel que soit le résultat

            var parts = raw.Split('|');
            if (parts.Length == 0 || !int.TryParse(parts[0], out var pid) || pid <= 0) return;
            long expectTicks = parts.Length > 1 && long.TryParse(parts[1], out var t) ? t : 0;

            Process proc;
            try { proc = Process.GetProcessById(pid); }
            catch (ArgumentException) { return; } // plus en cours d'exécution

            using (proc)
            {
                bool sameName  = proc.ProcessName.Equals("cloudflared", StringComparison.OrdinalIgnoreCase);
                bool sameStart = expectTicks == 0 || SafeStartTicks(proc) == expectTicks;
                if (sameName && sameStart)
                {
                    proc.Kill(entireProcessTree: true);
                    _logger.LogWarning("Tunnel cloudflared orphelin (PID {Pid}) d'une session précédente arrêté au démarrage.", pid);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Nettoyage du tunnel orphelin ignoré.");
        }
    }

    private static long SafeStartTicks(Process p)
    {
        try { return p.StartTime.ToUniversalTime().Ticks; }
        catch { return 0; }
    }

    // ─────────────── Job Object Windows (kill-on-close) ───────────────

    private void EnsureJob()
    {
        if (!OperatingSystem.IsWindows() || _job != IntPtr.Zero) return;
        try
        {
            var job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero) return;

            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };
            int len = Marshal.SizeOf(info);
            IntPtr ptr = Marshal.AllocHGlobal(len);
            try
            {
                Marshal.StructureToPtr(info, ptr, false);
                if (SetInformationJobObject(job, JobObjectExtendedLimitInformation, ptr, (uint)len))
                    _job = job; // handle conservé toute la vie du process => ferme à la mort de Nouba
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Job Object indisponible — repli sur le nettoyage par PID.");
        }
    }

    private void AssignToJob(Process p)
    {
        if (!OperatingSystem.IsWindows() || _job == IntPtr.Zero) return;
        try { AssignProcessToJobObject(_job, p.Handle); }
        catch (Exception ex) { _logger.LogDebug(ex, "Rattachement au Job Object ignoré."); }
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    private const int JobObjectExtendedLimitInformation = 9;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int infoType, IntPtr lpInfo, uint cbInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    private string? FindCloudflared()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "cloudflared.exe"),
            Path.Combine(_env.ContentRootPath,      "cloudflared.exe"),
            @"C:\Nouba\cloudflared.exe",
            @"C:\Program Files\Nouba\cloudflared.exe",
        };

        foreach (var path in candidates)
            if (File.Exists(path)) return path;

        // Chercher dans le PATH système.
        try
        {
            using var p = Process.Start(new ProcessStartInfo("cloudflared", "--version")
            {
                UseShellExecute        = false,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            });
            if (p != null)
            {
                p.WaitForExit(2_000);
                if (p.ExitCode == 0) return "cloudflared";
                try { p.Kill(); } catch { }
            }
        }
        catch { }

        return null;
    }
}
