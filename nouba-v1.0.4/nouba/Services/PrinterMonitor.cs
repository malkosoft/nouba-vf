using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Hubs;
using Nouba.Models;

namespace Nouba.Services;

/// <summary>
/// Hosted service qui interroge périodiquement l'imprimante pour détecter :
///   - hors ligne (pas de réponse TCP)
///   - capot ouvert
///   - manque papier / papier proche fin
/// Émet des alertes en temps réel via SignalR (canal "PrinterStatus") consommé par l'admin.
/// </summary>
public sealed class PrinterMonitor : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly EscPosPrinter _printer;
    private readonly IHubContext<QueueHub> _hub;
    private readonly ILogger<PrinterMonitor> _logger;

    private PrinterStatus _last = new();
    public PrinterStatus Last => _last;

    public PrinterMonitor(IServiceProvider services, EscPosPrinter printer, IHubContext<QueueHub> hub, ILogger<PrinterMonitor> logger)
    { _services = services; _printer = printer; _hub = hub; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Petit délai initial pour laisser l'app démarrer.
        await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            int waitSec = 30;
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var s = await db.UiSettings.AsNoTracking().FirstAsync(stoppingToken);
                waitSec = Math.Clamp(s.PrinterMonitorIntervalSec, 10, 600);

                if (s.PrinterMonitoringEnabled && s.PrinterEnabled && !string.IsNullOrWhiteSpace(s.PrinterIp))
                {
                    var status = await _printer.QueryStatusAsync(s, stoppingToken);
                    var changed = status.HasChangedFrom(_last);
                    _last = status;
                    if (changed)
                    {
                        _logger.LogInformation("Statut imprimante changé : {Status}", status.Summary());
                        await _hub.Clients.All.SendAsync("PrinterStatus", new
                        {
                            online      = status.Online,
                            paperOk     = status.PaperOk,
                            paperNearEnd= status.PaperNearEnd,
                            coverOpen   = status.CoverOpen,
                            error       = status.ErrorMessage,
                            checkedAt   = DateTime.Now.ToString("HH:mm:ss"),
                            ip          = s.PrinterIp,
                            port        = s.PrinterPort
                        }, cancellationToken: stoppingToken);
                    }
                }
                else
                {
                    _last = new PrinterStatus { Online = false, ErrorMessage = "Monitoring désactivé" };
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Erreur monitoring imprimante");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(waitSec), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}

public sealed class PrinterStatus
{
    public bool Online { get; set; }
    public bool PaperOk { get; set; } = true;
    public bool PaperNearEnd { get; set; }
    public bool CoverOpen { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.Now;

    public bool HasChangedFrom(PrinterStatus o) =>
        Online != o.Online || PaperOk != o.PaperOk || PaperNearEnd != o.PaperNearEnd ||
        CoverOpen != o.CoverOpen || (ErrorMessage ?? "") != (o.ErrorMessage ?? "");

    public string Summary()
    {
        if (!Online) return $"Hors ligne ({ErrorMessage})";
        var alerts = new List<string>();
        if (!PaperOk)      alerts.Add("MANQUE PAPIER");
        else if (PaperNearEnd) alerts.Add("Papier presque fini");
        if (CoverOpen)     alerts.Add("Capot ouvert");
        return alerts.Count == 0 ? "OK" : string.Join(" + ", alerts);
    }
}
