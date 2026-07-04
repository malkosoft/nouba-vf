using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Helpers;
using Nouba.Models;
using Nouba.Services;

namespace Nouba.Controllers;

/// <summary>
/// Suivi mobile public d'un ticket.
///   GET  /suivi/{publicId}           → page mobile responsive
///   GET  /suivi/{publicId}/state     → JSON pour polling (5-10 s)
///   GET  /suivi/qr/{publicId}.png    → QR code PNG pour la borne et le ticket
///   GET  /suivi                       → page générique « Saisissez votre numéro »
///                                       (pour QR général sur Display TV)
///
/// Routes publiques : aucune authentification, mais l'accès est protégé par
/// le caractère imprévisible du PublicId (8 caractères alphanumériques,
/// ~47 bits d'entropie). Les tickets terminés ou d'un autre jour ne révèlent
/// que le statut, pas de données personnelles.
/// </summary>
[Route("suivi")]
public class SuiviController : Controller
{
    private readonly AppDbContext _db;
    private readonly QrCodeService _qr;
    private readonly UiSettingsCache _settingsCache;
    private readonly CloudflareTunnelService _tunnel;

    public SuiviController(AppDbContext db, QrCodeService qr, UiSettingsCache settingsCache, CloudflareTunnelService tunnel)
    {
        _db = db;
        _qr = qr;
        _settingsCache = settingsCache;
        _tunnel = tunnel;
    }

    /// <summary>Page d'accueil du suivi (saisie de numéro). Utile pour QR général TV.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var settings = await _settingsCache.GetAsync();
        ViewBag.Settings = settings;
        return View("Index");
    }

    /// <summary>Page mobile responsive de suivi du ticket.</summary>
    [HttpGet("{publicId}")]
    public async Task<IActionResult> Track(string publicId)
    {
        var settings = await _settingsCache.GetAsync();
        if (!settings.QrFollowEnabled)
        {
            ViewBag.Error = "Le suivi mobile n'est pas activé sur cette installation.";
            return View("Disabled");
        }
        if (string.IsNullOrWhiteSpace(publicId) || publicId.Length > 16)
            return NotFound();
        var pid = publicId.ToUpperInvariant();

        var ticket = await _db.Tickets
            .Include(t => t.ServiceType)
            .Include(t => t.Counter)
            .FirstOrDefaultAsync(t => t.PublicId == pid);

        if (ticket is null)
        {
            // Ticket inexistant : page utilisateur propre MAIS bon code HTTP (404),
            // pas un faux 200 (P7/P8 du plan). On désactive la ré-exécution
            // StatusCodePages pour conserver NOTRE page « introuvable » (sinon la
            // page d'erreur générique la remplacerait).
            var scp = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>();
            if (scp is not null) scp.Enabled = false;
            Response.StatusCode = StatusCodes.Status404NotFound;
            ViewBag.Error = "Ticket introuvable. Vérifiez votre QR code.";
            return View("Disabled");
        }

        ViewBag.Settings = settings;
        ViewBag.PublicId = pid;
        return View("Track", ticket);
    }

    /// <summary>JSON état du ticket — utilisé par la page mobile en polling 5 s.</summary>
    [HttpGet("{publicId}/state")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> State(string publicId)
    {
        var settings = await _settingsCache.GetAsync();
        if (!settings.QrFollowEnabled) return StatusCode(503);
        if (string.IsNullOrWhiteSpace(publicId) || publicId.Length > 16)
            return NotFound();
        var pid = publicId.ToUpperInvariant();

        var ticket = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.ServiceType)
            .Include(t => t.Counter)
            .FirstOrDefaultAsync(t => t.PublicId == pid);
        if (ticket is null) return NotFound();

        // Position dans la file = nombre de tickets en attente avec un
        // CreatedAt antérieur, dans le même service. Les tickets prioritaires
        // passent devant : on les compte aussi dans peopleAhead pour rester honnête.
        int peopleAhead = 0;
        if (ticket.Status == TicketStatus.Waiting)
        {
            peopleAhead = await _db.Tickets.AsNoTracking()
                .CountAsync(t =>
                    t.ServiceTypeId == ticket.ServiceTypeId &&
                    t.Status == TicketStatus.Waiting &&
                    (
                        // priorité passe devant non-priorité
                        (t.IsPriority && !ticket.IsPriority) ||
                        // sinon ordre de création
                        (t.IsPriority == ticket.IsPriority && t.CreatedAt < ticket.CreatedAt)
                    ));
        }

        // Dernier ticket appelé du même service
        var lastCalled = await _db.CallHistories.AsNoTracking()
            .Where(c => c.ServiceName == (ticket.ServiceType != null ? ticket.ServiceType.Name : ""))
            .OrderByDescending(c => c.CalledAt)
            .Select(c => new { c.TicketNumber, c.CalledAt, c.CounterName })
            .FirstOrDefaultAsync();

        // Statut humain
        string humanStatus = ticket.Status switch
        {
            TicketStatus.Waiting   => peopleAhead == 0 ? "soon" : "waiting",
            TicketStatus.Called    => "called",
            TicketStatus.Finished => "completed",
            TicketStatus.Absent   => "skipped",
            _ => "unknown"
        };

        return Json(new
        {
            number      = ticket.Number,
            serviceName = ticket.ServiceType?.Name ?? "",
            status      = humanStatus,
            statusRaw   = ticket.Status.ToString(),
            peopleAhead,
            counterName = ticket.Counter?.Name ?? "",
            calledAt    = ticket.CalledAt?.ToString("HH:mm"),
            createdAt   = ticket.CreatedAt.ToString("HH:mm"),
            isPriority  = ticket.IsPriority,
            lastCalledTicket = lastCalled?.TicketNumber,
            lastCalledCounter = lastCalled?.CounterName,
            lastCalledAt = lastCalled?.CalledAt.ToString("HH:mm"),
            ts = DateTime.UtcNow.Ticks
        });
    }

    /// <summary>QR code PNG pour un ticket (utilisé par la borne et le ticket imprimé).</summary>
    /// <remarks>
    /// Route renommée en /suivi/qr-img/{id} pour ÉVITER le conflit de routing avec
    /// la route /suivi/{publicId} (qui matche aussi "/suivi/qr"). Sans ça, l'URL
    /// /suivi/qr/8F7K2Q était parfois routée vers Track avec publicId="qr" → 404
    /// HTML retourné dans l'image, donc QR cassé sur la borne.
    /// </remarks>
    [HttpGet("qr-img/{publicId}")]
    [HttpGet("qr-img/{publicId}.png")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Qr(string publicId, int size = 8)
    {
        if (string.IsNullOrWhiteSpace(publicId) || publicId.Length > 16) return NotFound();
        var settings = await _settingsCache.GetAsync();
        if (!settings.QrFollowEnabled) return NotFound();
        var pid = publicId.ToUpperInvariant();
        var url = TicketTrackingUrl.Build(settings, pid, Request, _tunnel.TunnelUrl);
        var px = Math.Clamp(size, 4, 20);
        var png = _qr.GeneratePng(url, px);
        return File(png, "image/png");
    }

    /// <summary>QR code générique (vers /suivi) — pour QR général TV.</summary>
    [HttpGet("qr-general.png")]
    [ResponseCache(Duration = 600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> QrGeneral(int size = 8)
    {
        var settings = await _settingsCache.GetAsync();
        if (!settings.QrFollowEnabled) return NotFound();
        var publicRoot = (settings.QrFollowPublicUrl ?? "").Trim().TrimEnd('/');
        string baseUrl;
        if (!string.IsNullOrEmpty(publicRoot))
            baseUrl = publicRoot;
        else if (!string.IsNullOrEmpty(_tunnel.TunnelUrl))
            baseUrl = _tunnel.TunnelUrl!.TrimEnd('/');
        else if (Nouba.Helpers.NetworkHelper.IsLoopbackOrAny(Request.Host.Host)
                 && Nouba.Helpers.NetworkHelper.GetLanIp() is string lan && !string.IsNullOrEmpty(lan))
            baseUrl = $"http://{lan}:{Request.Host.Port ?? 5000}";
        else
            baseUrl = $"{Request.Scheme}://{Request.Host.Value}";
        var url = $"{baseUrl}/suivi";
        var px = Math.Clamp(size, 4, 20);
        var png = _qr.GeneratePng(url, px);
        return File(png, "image/png");
    }
}
