using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Hubs;
using Nouba.Models;
using Nouba.Services;

namespace Nouba.Controllers;

public class BorneController : Controller
{
    private readonly AppDbContext _context;
    private readonly IHubContext<QueueHub> _hub;
    private readonly UiSettingsCache _settingsCache;
    private readonly TicketNumberAllocator _numberAllocator;
    private readonly EscPosPrinter _printer;
    private readonly ILogger<BorneController> _logger;

    public BorneController(
        AppDbContext context,
        IHubContext<QueueHub> hub,
        UiSettingsCache settingsCache,
        TicketNumberAllocator numberAllocator,
        EscPosPrinter printer,
        ILogger<BorneController> logger)
    {
        _context = context;
        _hub = hub;
        _settingsCache = settingsCache;
        _numberAllocator = numberAllocator;
        _printer = printer;
        _logger = logger;
    }

    private string ResolveLanguage(UiSettings settings, string? lang)
    {
        var selected = !string.IsNullOrWhiteSpace(lang) ? lang : Request.Cookies["nouba_lang"];
        selected = (selected ?? settings.DefaultLanguage ?? "fr").Trim().ToLowerInvariant();
        return selected is "fr" or "ar" or "tz" or "en" ? selected : "fr";
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index(string? lang = null)
    {
        // AsNoTracking : pas de suivi de changements (cette page est lecture seule).
        var services = await _context.Services
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();

        var settings = await _settingsCache.GetAsync(HttpContext.RequestAborted);
        var selectedLang = ResolveLanguage(settings, lang);

        // ── Nombre de personnes en attente par service (affiché en direct
        //    sur chaque tuile + rafraîchi par /Borne/Counts). Une seule
        //    requête groupée, AsNoTracking : négligeable pour la borne.
        var serviceIds = services.Select(s => s.Id).ToList();
        var waitingByService = await _context.Tickets.AsNoTracking()
            .Where(t => t.Status == TicketStatus.Waiting && serviceIds.Contains(t.ServiceTypeId))
            .GroupBy(t => t.ServiceTypeId)
            .Select(g => new { ServiceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ServiceId, x => x.Count, HttpContext.RequestAborted);
        ViewBag.WaitingByService = waitingByService;

        // ── Délai d'attente estimé par service ──
        // Estimation pour quelqu'un qui prendrait un ticket maintenant :
        // (personnes en attente × minutes moyennes par personne) ÷ guichets
        // actifs du service. Même logique que CreateTicket pour la cohérence.
        var activeCountersByService = await _context.Counters.AsNoTracking()
            .Where(c => c.IsActive && serviceIds.Contains(c.ServiceTypeId))
            .GroupBy(c => c.ServiceTypeId)
            .Select(g => new { ServiceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ServiceId, x => x.Count, HttpContext.RequestAborted);

        const int avgServiceMinutes = 4;
        var estimateByService = new Dictionary<int, int>();
        foreach (var s in services)
        {
            var w = waitingByService.TryGetValue(s.Id, out var wv) ? wv : 0;
            var counters = activeCountersByService.TryGetValue(s.Id, out var cv) && cv > 0 ? cv : 1;
            estimateByService[s.Id] = w > 0
                ? Math.Max((int)Math.Ceiling(w * avgServiceMinutes / (double)counters), 1)
                : 0;
        }
        ViewBag.EstimateByService = estimateByService;

        ViewBag.UiSettings = settings;
        ViewBag.CurrentLanguage = selectedLang;
        ViewBag.TitleText = selectedLang switch
        {
            "ar" => settings.BorneTitleAr,
            "tz" => settings.BorneTitleTz,
            "en" => settings.BorneTitleEn,
            _ => settings.BorneTitleFr
        };
        ViewBag.SubtitleText = selectedLang switch
        {
            "ar" => settings.BorneSubtitleAr,
            "tz" => settings.BorneSubtitleTz,
            "en" => settings.BorneSubtitleEn,
            _ => settings.BorneSubtitleFr
        };
        ViewBag.ClosureMessage = selectedLang switch
        {
            "ar" => settings.TicketClosureMessageAr,
            "tz" => settings.TicketClosureMessageTz,
            "en" => settings.TicketClosureMessageEn,
            _ => settings.TicketClosureMessageFr
        };

        return View(services);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTicket(int serviceId, string? lang = null, bool priorityTicket = false, string? priorityReason = null)
    {
        var ct = HttpContext.RequestAborted;

        // FindAsync utilise le cache de premier niveau d'EF si déjà chargé.
        var service = await _context.Services.FindAsync(new object?[] { serviceId }, ct);
        var settings = await _settingsCache.GetAsync(ct);
        if (service is null) return NotFound();

        var selectedLang = ResolveLanguage(settings, lang);

        if (settings.TicketsClosed)
            return RedirectToAction(nameof(Index), new { lang = selectedLang });

        if (!settings.EnablePriorityTickets)
        {
            priorityTicket = false;
            priorityReason = null;
        }

        var ticketDay = DateTime.Today.ToString("yyyy-MM-dd");
        Ticket? ticket = null;

        // Protection production anti-doublon :
        // 1) l'allocateur réduit les collisions ;
        // 2) l'index UNIQUE SQLite bloque toute collision restante ;
        // 3) on réessaie automatiquement avec le numéro suivant.
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            var nextNumber = await _numberAllocator.AllocateAsync(_context, service, ticketDay, ct);

            ticket = new Ticket
            {
                Number = nextNumber,
                ServiceTypeId = serviceId,
                CreatedAt = DateTime.Now,
                TicketDay = ticketDay,
                Status = TicketStatus.Waiting,
                IsPriority = priorityTicket,
                PriorityReason = priorityTicket
                    ? (string.IsNullOrWhiteSpace(priorityReason) ? "Prioritaire" : priorityReason.Trim())
                    : null,
                // Identifiant public court (8 caractères), utilisé dans
                // l'URL de suivi mobile /suivi/{PublicId}. Généré ici une
                // seule fois pour qu'il soit aussi imprimé sur le ticket.
                PublicId = QrCodeService.NewPublicId()
            };

            _context.Tickets.Add(ticket);
            try
            {
                await _context.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateException) when (attempt < 20)
            {
                _context.Entry(ticket).State = EntityState.Detached;
                await Task.Delay(20 * attempt, ct);
                ticket = null;
            }
        }

        if (ticket is null)
            throw new InvalidOperationException("Impossible de créer un ticket unique après plusieurs tentatives.");

        // Notifie tous les écrans/agents en parallèle (fire-and-forget contrôlé : pas d'await pour ne pas bloquer la réponse).
        await _hub.Clients.All.SendAsync("RefreshQueue", cancellationToken: ct);

        // Une seule requête pour récupérer compteurs/positions, plus efficace que 3 CountAsync séparés.
        var today = DateTime.Today;
        var waitingCount = await _context.Tickets.AsNoTracking()
            .CountAsync(t => t.ServiceTypeId == serviceId && t.Status == TicketStatus.Waiting, ct);

        var activeCounters = await _context.Counters.AsNoTracking()
            .CountAsync(c => c.ServiceTypeId == serviceId && c.IsActive, ct);
        if (activeCounters <= 0) activeCounters = 1;

        const int averageServiceMinutes = 4;
        var priorityAhead = priorityTicket
            ? await _context.Tickets.AsNoTracking().CountAsync(t => t.ServiceTypeId == serviceId && t.Status == TicketStatus.Waiting && t.IsPriority && t.CreatedAt < ticket.CreatedAt, ct)
            : await _context.Tickets.AsNoTracking().CountAsync(t => t.ServiceTypeId == serviceId && t.Status == TicketStatus.Waiting && (t.IsPriority || t.CreatedAt < ticket.CreatedAt), ct);
        var estimatedWaitMinutes = Math.Max((int)Math.Ceiling(priorityAhead * averageServiceMinutes / (double)activeCounters), 1);

        TempData["TicketNumber"] = ticket.Number;
        TempData["ServiceName"] = service.Name;
        TempData["Lang"] = selectedLang;
        TempData["SiteName"] = settings.SiteName;
        TempData["CreatedAt"] = ticket.CreatedAt.ToString("dd/MM/yyyy HH:mm");
        TempData["WaitingCount"] = waitingCount.ToString();
        TempData["EstimatedWaitMinutes"] = estimatedWaitMinutes.ToString();
        TempData["PriorityTicket"] = priorityTicket ? "true" : "false";
        TempData["PriorityReason"] = ticket.PriorityReason ?? string.Empty;
        // QR de suivi mobile : on passe l'ID public et l'état d'activation
        // à la vue Confirmation pour qu'elle l'affiche en grand.
        TempData["PublicId"] = ticket.PublicId ?? string.Empty;
        TempData["QrFollowEnabled"] = settings.QrFollowEnabled ? "true" : "false";
        // Mode d'impression — utilisé par la vue Confirmation pour décider window.print().
        TempData["PrintMode"] = settings.PrintMode ?? "all";
        TempData["EscPosOk"] = "skipped";

        // ── Impression ESC/POS automatique (si activée) ─────────────
        // Modes "escpos" et "all" → on lance l'impression TCP directe en
        // FIRE-AND-FORGET pour ne JAMAIS bloquer la réponse HTTP. Le ticket
        // est déjà en DB et le client est redirigé vers Confirmation
        // immédiatement (< 200 ms) ; l'imprimante reçoit l'ordre en parallèle.
        // Avant : on attendait jusqu'à PrinterTimeoutMs+500 ms (≈ 5.5 s par
        // défaut) ce qui rendait la borne « lente » côté utilisateur.
        var mode = (settings.PrintMode ?? "all").ToLowerInvariant();
        if (settings.PrinterEnabled && (mode == "escpos" || mode == "all"))
        {
            var footer = selectedLang switch
            {
                "ar" => settings.TicketFooterAr,
                "tz" => settings.TicketFooterTz,
                "en" => settings.TicketFooterEn,
                _    => settings.TicketFooterFr
            };
            // URL de suivi pour le QR sur le ticket imprimé. Si la fonction est
            // désactivée OU si le ticket n'a pas de PublicId, on ne passe rien
            // → l'imprimante n'imprime aucun QR. Compatible avec rétro-compat.
            string? qrPayload = null;
            if (settings.QrFollowEnabled && !string.IsNullOrEmpty(ticket.PublicId))
            {
                qrPayload = Nouba.Helpers.TicketTrackingUrl.Build(settings, ticket.PublicId, Request);
            }

            var data = new EscPosTicketData
            {
                SiteName = settings.SiteName,
                TicketNumber = ticket.Number,
                ServiceName = service.Name,
                CreatedAt = ticket.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                WaitingCount = waitingCount.ToString(),
                EstimateMinutes = estimatedWaitMinutes.ToString(),
                FooterText = footer,
                ShowNoubaFooter = settings.TicketShowNoubaFooter,
                IsPriority = priorityTicket,
                PriorityReason = ticket.PriorityReason,
                QrPayload = qrPayload,
                QrPublicId = ticket.PublicId
            };

            // On capture les références AVANT le fire-and-forget car la
            // réponse HTTP va se terminer et le HttpContext ne sera plus
            // valide à l'intérieur de la Task asynchrone.
            var printer = _printer;
            var loggerLocal = _logger;
            var ticketNumberLocal = ticket.Number;
            var timeoutMs = Math.Max(1000, settings.PrinterTimeoutMs + 500);

            // On marque "queued" : la vue Confirmation ne saura pas dire
            // immédiatement si l'impression a réussi, mais le client a déjà
            // son ticket à l'écran. C'est volontaire — l'expérience prime.
            TempData["EscPosOk"] = "queued";

            _ = Task.Run(async () =>
            {
                try
                {
                    using var cts = new CancellationTokenSource(timeoutMs);
                    var (ok, err) = await printer.PrintTicketAsync(settings, data, cts.Token);
                    if (!ok)
                        loggerLocal.LogWarning("ESC/POS print KO ticket {Number}: {Err}", ticketNumberLocal, err ?? "(null)");
                }
                catch (Exception ex)
                {
                    loggerLocal.LogWarning(ex, "ESC/POS print failed for ticket {Number}", ticketNumberLocal);
                }
            });
        }

        return RedirectToAction(nameof(Confirmation));
    }

    public IActionResult Confirmation()
    {
        return View();
    }

    /// <summary>
    /// Renvoie en JSON le nombre de personnes en attente par service actif.
    /// Utilisé par la borne pour rafraîchir en direct les badges « en attente »
    /// sans recharger toute la page. Très léger (une requête groupée, sans suivi).
    /// Format : { "1": 3, "2": 0, "3": 7 }
    /// </summary>
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Counts()
    {
        var ct = HttpContext.RequestAborted;
        var activeIds = await _context.Services.AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var waiting = await _context.Tickets.AsNoTracking()
            .Where(t => t.Status == TicketStatus.Waiting && activeIds.Contains(t.ServiceTypeId))
            .GroupBy(t => t.ServiceTypeId)
            .Select(g => new { ServiceId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var activeCounters = await _context.Counters.AsNoTracking()
            .Where(c => c.IsActive && activeIds.Contains(c.ServiceTypeId))
            .GroupBy(c => c.ServiceTypeId)
            .Select(g => new { ServiceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ServiceId, x => x.Count, ct);

        const int avgServiceMinutes = 4;
        var result = new Dictionary<string, object>();
        foreach (var id in activeIds)
        {
            var w = waiting.FirstOrDefault(x => x.ServiceId == id)?.Count ?? 0;
            var counters = activeCounters.TryGetValue(id, out var cv) && cv > 0 ? cv : 1;
            var e = w > 0 ? Math.Max((int)Math.Ceiling(w * avgServiceMinutes / (double)counters), 1) : 0;
            // w = personnes en attente, e = minutes estimées (0 = aucune attente)
            result[id.ToString()] = new { w, e };
        }

        return Json(result);
    }
}
