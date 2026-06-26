using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Nouba.Services;

/// <summary>
/// Démarre cloudflared en arrière-plan si l'exécutable est présent.
/// Expose TunnelUrl dès que le tunnel est opérationnel ; TicketTrackingUrl
/// l'utilise comme fallback quand QrFollowPublicUrl n'est pas configuré.
/// </summary>
public sealed class CloudflareTunnelService : BackgroundService
{
    private static readonly Regex _urlRx = new(
        @"https://[a-z0-9\-]+\.trycloudflare\.com",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CloudflareTunnelService> _logger;

    // volatile : lu depuis n'importe quel thread HTTP
    private volatile string? _tunnelUrl;
    private volatile string _status = "initializing";

    public string? TunnelUrl => _tunnelUrl;
    public string Status    => _status;

    public CloudflareTunnelService(IWebHostEnvironment env, ILogger<CloudflareTunnelService> logger)
    {
        _env    = env;
        _logger = logger;
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

        _status = "stopped";
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
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Tuer le processus proprement à l'arrêt du service.
        await using var reg = ct.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch { }
        });

        await process.WaitForExitAsync(ct);
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
