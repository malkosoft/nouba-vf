using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Services;

namespace Nouba.Controllers;

/// <summary>
/// Contrôleur dédié à la gestion de l'imprimante :
///   - Sauvegarde des paramètres (admin)
///   - Test d'impression (admin)
///   - Endpoint d'impression à la demande appelé en interne par la borne
/// </summary>
public class PrinterController : Controller
{
    private readonly AppDbContext _context;
    private readonly EscPosPrinter _printer;
    private readonly UiSettingsCache _cache;
    private readonly PrinterMonitor _monitor;
    private const string SessionKey = "AdminUserId";
    private const string SessionRoleKey = "AdminRole";

    public PrinterController(AppDbContext context, EscPosPrinter printer, UiSettingsCache cache, PrinterMonitor monitor)
    {
        _context = context;
        _printer = printer;
        _cache = cache;
        _monitor = monitor;
    }

    // Tous les endpoints de ce contrôleur sont des réglages TECHNIQUES
    // d'imprimante : on exige session admin ET rôle fournisseur.
    private bool IsAdmin() =>
        HttpContext.Session.GetInt32(SessionKey).HasValue &&
        string.Equals(HttpContext.Session.GetString(SessionRoleKey), "fournisseur", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Mise à jour des paramètres d'impression depuis l'admin.
    /// On reste tolérant : champs vides = on conserve la valeur précédente.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(
        string? printMode,
        bool printerEnabled,
        string? printerConnection,
        string? printerIp,
        int? printerPort,
        string? printerName,
        string? printerComPort,
        int? printerBaudRate,
        int? ticketWidth,
        int? printerTimeoutMs,
        bool printerAutoCut,
        bool printerBeep,
        bool ticketShowLogo,
        bool ticketShowQr,
        bool ticketShowNoubaFooter,
        string? ticketFooterFr,
        string? ticketFooterAr,
        string? ticketFooterTz,
        string? ticketFooterEn,
        bool reportShowClientLogo,
        bool reportShowNoubaLogo,
        bool printerMonitoringEnabled = true,
        bool printerAlertSound = true,
        int? printerMonitorIntervalSec = null)
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Admin");
        var s = await _context.UiSettings.FirstAsync();

        if (!string.IsNullOrWhiteSpace(printMode) &&
            (printMode == "browser" || printMode == "chrome-kiosk" || printMode == "escpos" || printMode == "all"))
            s.PrintMode = printMode;

        s.PrinterEnabled = printerEnabled;
        if (!string.IsNullOrWhiteSpace(printerConnection) &&
            (printerConnection == "network" || printerConnection == "usb" || printerConnection == "serial"))
            s.PrinterConnection = printerConnection;
        if (!string.IsNullOrWhiteSpace(printerIp)) s.PrinterIp = printerIp.Trim();
        if (printerPort.HasValue && printerPort.Value > 0 && printerPort.Value <= 65535) s.PrinterPort = printerPort.Value;
        s.PrinterName = string.IsNullOrWhiteSpace(printerName) ? null : printerName.Trim();
        s.PrinterComPort = string.IsNullOrWhiteSpace(printerComPort) ? null : printerComPort.Trim();
        if (printerBaudRate.HasValue && printerBaudRate.Value >= 1200) s.PrinterBaudRate = printerBaudRate.Value;
        if (ticketWidth.HasValue && (ticketWidth.Value == 58 || ticketWidth.Value == 80)) s.TicketWidth = ticketWidth.Value;
        if (printerTimeoutMs.HasValue && printerTimeoutMs.Value >= 500 && printerTimeoutMs.Value <= 30000) s.PrinterTimeoutMs = printerTimeoutMs.Value;
        s.PrinterAutoCut = printerAutoCut;
        s.PrinterBeep = printerBeep;
        s.TicketShowLogo = ticketShowLogo;
        s.TicketShowQr = ticketShowQr;
        s.TicketShowNoubaFooter = ticketShowNoubaFooter;
        s.TicketFooterFr = string.IsNullOrWhiteSpace(ticketFooterFr) ? null : ticketFooterFr.Trim();
        s.TicketFooterAr = string.IsNullOrWhiteSpace(ticketFooterAr) ? null : ticketFooterAr.Trim();
        s.TicketFooterTz = string.IsNullOrWhiteSpace(ticketFooterTz) ? null : ticketFooterTz.Trim();
        s.TicketFooterEn = string.IsNullOrWhiteSpace(ticketFooterEn) ? null : ticketFooterEn.Trim();
        s.ReportShowClientLogo = reportShowClientLogo;
        s.ReportShowNoubaLogo  = reportShowNoubaLogo;
        s.PrinterMonitoringEnabled = printerMonitoringEnabled;
        s.PrinterAlertSound = printerAlertSound;
        if (printerMonitorIntervalSec.HasValue && printerMonitorIntervalSec.Value >= 10 && printerMonitorIntervalSec.Value <= 600)
            s.PrinterMonitorIntervalSec = printerMonitorIntervalSec.Value;

        await _context.SaveChangesAsync();
        _cache.Invalidate();
        TempData["Success"] = $"Paramètres d'impression enregistrés (mode {s.PrintMode}, connexion {s.PrinterConnection}).";
        return Redirect("/Admin?tab=printer");
    }

    /// <summary>Liste des imprimantes installées sur le serveur Windows + ports COM disponibles.</summary>
    [HttpGet]
    public IActionResult List()
    {
        if (!IsAdmin()) return Unauthorized();
        return Json(new
        {
            ok = true,
            os = Environment.OSVersion.Platform.ToString(),
            printers = _printer.ListInstalledPrinters(),
            comPorts = _printer.ListSerialPorts()
        });
    }

    /// <summary>Test d'impression manuel depuis l'admin.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TestPrint()
    {
        if (!IsAdmin()) return RedirectToAction("Login", "Admin");
        var s = await _context.UiSettings.FirstAsync();
        var (ok, err) = await _printer.TestPrintAsync(s, HttpContext.RequestAborted);
        if (ok)
            TempData["Success"] = $"✅ Test envoyé à l'imprimante {s.PrinterIp}:{s.PrinterPort}. Vérifiez la sortie papier.";
        else
            TempData["Error"] = $"❌ Échec : {err}. Vérifiez l'IP, le port (9100), le câble réseau et que l'imprimante est allumée.";
        return Redirect("/Admin?tab=printer");
    }

    /// <summary>
    /// Test d'impression côté API/AJAX (renvoie un JSON pour affichage live sans recharger).
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TestPrintJson()
    {
        if (!IsAdmin()) return Unauthorized();
        var s = await _context.UiSettings.FirstAsync();
        var (ok, err) = await _printer.TestPrintAsync(s, HttpContext.RequestAborted);
        return Json(new { ok, error = err, ip = s.PrinterIp, port = s.PrinterPort });
    }

    /// <summary>
    /// Statut « ping » de l'imprimante : tente une connexion TCP brève sans envoyer de données.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Ping()
    {
        if (!IsAdmin()) return Unauthorized();
        var s = await _context.UiSettings.AsNoTracking().FirstAsync();
        if (!s.PrinterEnabled) return Json(new { ok = false, reason = "disabled" });
        if (string.IsNullOrWhiteSpace(s.PrinterIp)) return Json(new { ok = false, reason = "no-ip" });
        try
        {
            using var client = new System.Net.Sockets.TcpClient { SendTimeout = 1500, ReceiveTimeout = 1500 };
            var connect = client.ConnectAsync(s.PrinterIp.Trim(), s.PrinterPort <= 0 ? 9100 : s.PrinterPort);
            var done = await Task.WhenAny(connect, Task.Delay(1500));
            if (done != connect || !client.Connected) return Json(new { ok = false, reason = "timeout", ip = s.PrinterIp, port = s.PrinterPort });
            return Json(new { ok = true, ip = s.PrinterIp, port = s.PrinterPort });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, reason = ex.Message, ip = s.PrinterIp, port = s.PrinterPort });
        }
    }

    /// <summary>Statut détaillé (online + capot + papier) — utilisé par le widget admin.</summary>
    [HttpGet]
    public IActionResult Status()
    {
        if (!IsAdmin()) return Unauthorized();
        var st = _monitor.Last;
        return Json(new
        {
            online = st.Online,
            paperOk = st.PaperOk,
            paperNearEnd = st.PaperNearEnd,
            coverOpen = st.CoverOpen,
            error = st.ErrorMessage,
            summary = st.Summary(),
            checkedAt = st.CheckedAt.ToString("HH:mm:ss")
        });
    }
}
