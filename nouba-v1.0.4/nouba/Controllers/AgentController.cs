using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Hubs;
using Nouba.Helpers;
using Nouba.Models;
using Nouba.Services;
using Nouba.Security;

namespace Nouba.Controllers;

public class AgentController : Controller
{
    private readonly AppDbContext _context;
    private readonly IHubContext<QueueHub> _hub;
    private readonly UiSettingsCache _settingsCache;
    private readonly TicketNumberAllocator _numberAllocator;
    private readonly LoginRateLimiter _loginRateLimiter;
    private readonly Nouba.Services.PiperTtsService _piper;
    private const string AgentSessionKey = "AgentId";
    private static readonly SemaphoreSlim CallNextSemaphore = new(1, 1);

    public AgentController(AppDbContext context, IHubContext<QueueHub> hub, UiSettingsCache settingsCache, TicketNumberAllocator numberAllocator, LoginRateLimiter loginRateLimiter, ILogger<AgentController> logger, Nouba.Services.PiperTtsService piper)
    {
        _context = context;
        _hub = hub;
        _settingsCache = settingsCache;
        _numberAllocator = numberAllocator;
        _loginRateLimiter = loginRateLimiter;
        _logger = logger;
        _piper = piper;
    }

    private readonly ILogger<AgentController> _logger;

    // ── Authentication ────────────────────────────────────────────
    private bool IsLoggedIn() => HttpContext.Session.GetInt32(AgentSessionKey).HasValue;

    [HttpGet]
    public async Task<IActionResult> Login()
    {
        if (IsLoggedIn()) return RedirectToAction(nameof(Index));
        ViewBag.Settings = await _settingsCache.GetAsync(HttpContext.RequestAborted);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string login, string password)
    {
        if (_loginRateLimiter.IsBlocked(HttpContext, "agent", login, out var remaining))
        {
            TempData["Error"] = $"Trop de tentatives. Réessayez dans {Math.Ceiling(remaining.TotalMinutes)} minute(s).";
            ViewBag.Settings = await _settingsCache.GetAsync(HttpContext.RequestAborted);
            return View();
        }

        var normalizedLogin = (login ?? string.Empty).Trim();
        await _loginRateLimiter.DelayIfNeededAsync(HttpContext, "agent", normalizedLogin, HttpContext.RequestAborted);
        var agent = await _context.Agents
            .Include(a => a.Counter)
            .Include(a => a.ServiceType)
            .FirstOrDefaultAsync(a => a.IsActive && a.Login == normalizedLogin);

        if (agent is null || !VerifyAgentPassword(agent, password ?? string.Empty))
        {
            _loginRateLimiter.RegisterFailure(HttpContext, "agent", normalizedLogin);
            TempData["Error"] = "Identifiants invalides.";
            ViewBag.Settings = await _settingsCache.GetAsync(HttpContext.RequestAborted);
            return View();
        }

        _loginRateLimiter.RegisterSuccess(HttpContext, "agent", normalizedLogin);

        if (string.IsNullOrWhiteSpace(agent.PasswordHash) && !string.IsNullOrWhiteSpace(agent.Password))
        {
            // Migration silencieuse des anciens mots de passe en clair vers PBKDF2.
            var migrated = PasswordHasher.HashPassword(password ?? string.Empty);
            agent.PasswordHash = migrated.Hash;
            agent.PasswordSalt = migrated.Salt;
            agent.Password = string.Empty;
            await _context.SaveChangesAsync();
        }

        HttpContext.Session.SetInt32(AgentSessionKey, agent.Id);
        HttpContext.Session.SetString("AgentName", agent.FullName);
        HttpContext.Session.SetString("AgentLogin", agent.Login);
        HttpContext.Session.SetInt32("AgentServiceId", agent.ServiceTypeId);
        HttpContext.Session.SetInt32("AgentCounterId", agent.CounterId);
        HttpContext.Session.SetString("AgentCounterName", agent.Counter?.Name ?? "Guichet");
        HttpContext.Session.SetString("AgentServiceName", agent.ServiceType?.Name ?? "");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        // Ne vide pas toute la session : évite de déconnecter l'admin si l'interface agent est dans le même navigateur.
        HttpContext.Session.Remove(AgentSessionKey);
        HttpContext.Session.Remove("AgentName");
        HttpContext.Session.Remove("AgentLogin");
        HttpContext.Session.Remove("AgentServiceId");
        HttpContext.Session.Remove("AgentCounterId");
        HttpContext.Session.Remove("AgentCounterName");
        HttpContext.Session.Remove("AgentServiceName");
        return RedirectToAction(nameof(Login));
    }

    private bool VerifyAgentPassword(Agent agent, string password)
    {
        if (!string.IsNullOrWhiteSpace(agent.PasswordHash) && !string.IsNullOrWhiteSpace(agent.PasswordSalt))
            return PasswordHasher.VerifyPassword(password, agent.PasswordHash, agent.PasswordSalt);

        // Compatibilité avec les bases v4 : accepter l'ancien mot de passe une seule fois,
        // puis le migrer vers PasswordHash/PasswordSalt après connexion réussie.
        return !string.IsNullOrWhiteSpace(agent.Password) && agent.Password == password;
    }

    private int? CurrentAgentServiceId() => HttpContext.Session.GetInt32("AgentServiceId");

    private bool IsAuthorizedService(int serviceId)
    {
        var sessionServiceId = CurrentAgentServiceId();
        return sessionServiceId.HasValue && sessionServiceId.Value == serviceId;
    }

    private IActionResult ForbiddenAgentAction(int serviceId, string message = "Action non autorisée pour ce service.")
    {
        TempData["Message"] = message;
        TempData["MessageType"] = "danger";
        return RedirectToAction(nameof(Index), new { serviceId });
    }

    // ── Main agent view ───────────────────────────────────────────
    private string ResolveLanguage(UiSettings settings, string? lang)
    {
        var selected = !string.IsNullOrWhiteSpace(lang) ? lang : Request.Cookies["nouba_lang"];
        selected = (selected ?? settings.DefaultLanguage ?? "fr").Trim().ToLowerInvariant();
        return selected is "fr" or "ar" or "tz" or "en" ? selected : "fr";
    }

    public async Task<IActionResult> Index(int? serviceId = null, string? lang = null)
    {
        if (!IsLoggedIn()) return RedirectToAction(nameof(Login));

        var ct = HttpContext.RequestAborted;

        // #6 — Réaffectation admin en temps réel : on relit l'affectation réelle
        // de l'agent en base à chaque chargement de page (la session, figée à la
        // connexion, pouvait être périmée). Si l'agent a été désactivé/supprimé,
        // on le déconnecte proprement.
        var meId = HttpContext.Session.GetInt32(AgentSessionKey);
        if (meId.HasValue)
        {
            var me = await _context.Agents.AsNoTracking()
                .Include(a => a.ServiceType)
                .Include(a => a.Counter)
                .FirstOrDefaultAsync(a => a.Id == meId.Value, ct);
            if (me is null || !me.IsActive)
            {
                HttpContext.Session.Remove(AgentSessionKey);
                return RedirectToAction(nameof(Login));
            }
            HttpContext.Session.SetInt32("AgentServiceId", me.ServiceTypeId);
            HttpContext.Session.SetInt32("AgentCounterId", me.CounterId);
            HttpContext.Session.SetString("AgentCounterName", me.Counter?.Name ?? "Guichet");
            HttpContext.Session.SetString("AgentServiceName", me.ServiceType?.Name ?? "");
        }

        var settings = await _settingsCache.GetAsync(ct);
        var selectedLang = ResolveLanguage(settings, lang);

        var services = await _context.Services
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);

        var sessionServiceId = HttpContext.Session.GetInt32("AgentServiceId");
        // Honor explicit serviceId from query string (e.g. after language switch),
        // but only if it matches the agent's own service (security: no cross-service access).
        int? requestedServiceId = serviceId.HasValue && serviceId.Value > 0 ? serviceId : null;
        var selectedServiceId = (requestedServiceId.HasValue && requestedServiceId.Value == sessionServiceId)
            ? requestedServiceId.Value
            : sessionServiceId ?? services.FirstOrDefault()?.Id ?? 0;

        var data = await BuildAgentStateAsync(selectedServiceId, ct);

        ViewBag.Settings = settings;
        ViewBag.Services = services;
        ViewBag.SelectedServiceId = selectedServiceId;
        ViewBag.CurrentCall = data.CurrentCall;
        ViewBag.CurrentLanguage = selectedLang;
        ViewBag.CalledTickets = data.CalledTickets;
        ViewBag.TotalToday = data.TotalToday;
        ViewBag.ServedToday = data.ServedToday;
        ViewBag.AbsentToday = data.AbsentToday;
        ViewBag.WaitingCount = data.WaitingTickets.Count;
        ViewBag.AvgWaitMinutes = data.AvgWaitMinutes;
        ViewBag.AgentName = HttpContext.Session.GetString("AgentName") ?? "Agent";
        ViewBag.AgentCounterName = HttpContext.Session.GetString("AgentCounterName") ?? "Guichet";
        ViewBag.AgentServiceName = HttpContext.Session.GetString("AgentServiceName") ?? "";
        ViewBag.AgentId = HttpContext.Session.GetInt32(AgentSessionKey) ?? 0;
        return View(data.WaitingTickets);
    }

    /// <summary>
    /// Endpoint AJAX appelé toutes les 5 secondes par la vue Agent
    /// (en remplacement du window.location.reload sauvage).
    /// Retourne uniquement le strict nécessaire à actualiser dans la page.
    /// </summary>
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> State(int serviceId)
    {
        if (!IsLoggedIn()) return Unauthorized();
        if (!IsAuthorizedService(serviceId)) return Forbid();
        var ct = HttpContext.RequestAborted;
        var data = await BuildAgentStateAsync(serviceId, ct);

        return Json(new
        {
            waiting = data.WaitingTickets.Select(t => new
            {
                id = t.Id,
                number = t.Number,
                createdAt = t.CreatedAt.ToString("HH:mm"),
                isPriority = t.IsPriority,
                priorityReason = t.PriorityReason
            }),
            called = data.CalledTickets.Select(t => new
            {
                id = t.Id,
                number = t.Number,
                calledAt = t.CalledAt?.ToString("HH:mm")
            }),
            currentCall = data.CurrentCall == null ? null : new
            {
                ticketNumber = data.CurrentCall.TicketNumber,
                counterName = data.CurrentCall.CounterName,
                serviceName = data.CurrentCall.ServiceName,
                calledAt = data.CurrentCall.CalledAt.ToString("HH:mm")
            },
            stats = new
            {
                waiting = data.WaitingTickets.Count,
                served = data.ServedToday,
                absent = data.AbsentToday,
                total = data.TotalToday,
                avgWait = data.AvgWaitMinutes
            }
        });
    }

    /// <summary>
    /// Endpoint TRÈS léger qui retourne uniquement l'affectation courante
    /// de l'agent connecté (serviceId + counterId + actif). Permet au client
    /// de détecter immédiatement, par polling court (3 s), si l'admin a
    /// changé son guichet/service — sans dépendre de SignalR ni recharger
    /// tout l'état des tickets. Si une différence est détectée côté client,
    /// la page se recharge pour appliquer la nouvelle affectation.
    /// </summary>
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> MyAssignment()
    {
        if (!IsLoggedIn()) return Unauthorized();
        var agentId = HttpContext.Session.GetInt32(AgentSessionKey) ?? 0;
        if (agentId == 0) return Unauthorized();
        var ct = HttpContext.RequestAborted;
        var a = await _context.Agents.AsNoTracking()
            .Where(x => x.Id == agentId)
            .Select(x => new { x.Id, x.ServiceTypeId, x.CounterId, x.IsActive })
            .FirstOrDefaultAsync(ct);
        if (a is null) return NotFound();
        return Json(new { id = a.Id, serviceId = a.ServiceTypeId, counterId = a.CounterId, active = a.IsActive });
    }

    private async Task<AgentStateData> BuildAgentStateAsync(int selectedServiceId, CancellationToken ct)
    {
        var today = DateTime.Today;

        // Les 4 requêtes ci-dessous sont consécutives mais bénéficient toutes de l'index
        // composite (ServiceTypeId, Status). Sur SQLite local en process, c'est < 5 ms total.
        var waitingTickets = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.ServiceTypeId == selectedServiceId && t.Status == TicketStatus.Waiting)
            .OrderByDescending(t => t.IsPriority)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

        var calledTickets = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.ServiceTypeId == selectedServiceId && t.Status == TicketStatus.Called)
            .OrderByDescending(t => t.CalledAt)
            .Take(3)
            .ToListAsync(ct);

        var serviceName = await _context.Services.AsNoTracking()
            .Where(s => s.Id == selectedServiceId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);

        // Ticket en cours = le ticket réellement au statut « Called » à ce
        // service. On le déduit en priorité de la table Tickets (source de
        // vérité, cohérente avec le garde-fou de CallNext). On enrichit avec
        // l'historique pour le nom du guichet/agent quand c'est disponible.
        // Avant, on lisait UNIQUEMENT le dernier CallHistory par nom de
        // service : il pouvait être désynchronisé (ticket clôturé mais
        // historique présent, ou inversement), ce qui masquait les boutons
        // Terminer/Absent/Transférer alors que le ticket était bloqué.
        CallHistory? currentCall = null;
        var activeCalled = calledTickets.FirstOrDefault();
        if (activeCalled != null)
        {
            // On tente de retrouver l'entrée d'historique correspondante (pour
            // le nom de guichet / l'agent), sinon on la fabrique depuis le ticket.
            currentCall = await _context.CallHistories.AsNoTracking()
                .Where(c => c.TicketId == activeCalled.Id)
                .OrderByDescending(c => c.CalledAt)
                .FirstOrDefaultAsync(ct);

            currentCall ??= new CallHistory
            {
                TicketId = activeCalled.Id,
                TicketNumber = activeCalled.Number,
                ServiceName = serviceName ?? string.Empty,
                CounterName = HttpContext.Session.GetString("AgentCounterName") ?? "Guichet",
                CalledAt = activeCalled.CalledAt ?? DateTime.Now
            };
        }

        // Stats du jour : un seul roundtrip avec GroupBy côté SQL.
        var todayStats = await _context.Tickets.AsNoTracking()
            .Where(t => t.ServiceTypeId == selectedServiceId && t.CreatedAt >= today)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalToday = todayStats.Sum(s => s.Count);
        var servedToday = todayStats.FirstOrDefault(s => s.Status == TicketStatus.Finished)?.Count ?? 0;
        var absentToday = todayStats.FirstOrDefault(s => s.Status == TicketStatus.Absent)?.Count ?? 0;

        // Moyenne d'attente : on ne projette que les 2 colonnes nécessaires.
        var calledToday = await _context.Tickets.AsNoTracking()
            .Where(t => t.ServiceTypeId == selectedServiceId && t.CreatedAt >= today
                     && t.Status != TicketStatus.Waiting && t.CalledAt.HasValue)
            .Select(t => new { t.CreatedAt, CalledAt = t.CalledAt!.Value })
            .ToListAsync(ct);
        var avgWait = calledToday.Count > 0
            ? calledToday.Average(t => (t.CalledAt - t.CreatedAt).TotalMinutes)
            : 0;

        return new AgentStateData(
            waitingTickets,
            calledTickets,
            currentCall,
            totalToday,
            servedToday,
            absentToday,
            Math.Round(avgWait, 1));
    }

    private sealed record AgentStateData(
        List<Ticket> WaitingTickets,
        List<Ticket> CalledTickets,
        CallHistory? CurrentCall,
        int TotalToday,
        int ServedToday,
        int AbsentToday,
        double AvgWaitMinutes);

    // BuildAnnouncementText a été retiré (v2.7.43) : il générait un texte de
    // pré-chauffage DIFFÉRENT de celui réellement demandé par l'écran Display
    // (ex: "Ticket A12 guichet Guichet 3" côté serveur vs "Ticket A douze,
    // veuillez vous présenter au Guichet trois" côté client réel). Comme le
    // cache Piper est indexé sur le texte exact, ce pré-chauffage ne produisait
    // JAMAIS de cache hit : chaque annonce réelle repartait d'une synthèse à
    // froid, avec une latence variable selon la charge serveur — cause
    // probable du « ça marche un coup sur deux ». On utilise maintenant
    // Nouba.Helpers.AnnouncementTextBuilder, qui reproduit fidèlement le
    // texte généré en JavaScript par Display/Index.cshtml.


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CallNext(int serviceId)
    {
        if (!IsLoggedIn()) return RedirectToAction(nameof(Login));
        if (!IsAuthorizedService(serviceId)) return ForbiddenAgentAction(CurrentAgentServiceId() ?? serviceId);
        var ct = HttpContext.RequestAborted;

        await CallNextSemaphore.WaitAsync(ct);
        try
        {

        // ── Garde-fou métier ────────────────────────────────────────────
        // Un agent ne peut PAS appeler le ticket suivant tant que le ticket
        // en cours à son guichet n'est pas clôturé (Terminé / Absent /
        // Transféré). Vérification côté serveur : cacher le bouton côté UI
        // ne suffit pas (rechargement, double-clic, requête forgée…).
        var agentCounterId = HttpContext.Session.GetInt32("AgentCounterId");
        // Si le guichet n'est pas en session, on retombe sur le guichet actif
        // du service pour ne pas laisser de trou dans la protection.
        if (!agentCounterId.HasValue)
        {
            var fallbackCounter = await _context.Counters.AsNoTracking()
                .FirstOrDefaultAsync(c => c.ServiceTypeId == serviceId && c.IsActive, ct);
            agentCounterId = fallbackCounter?.Id;
        }
        if (agentCounterId.HasValue)
        {
            // Filtre par service ET guichet : un ticket appelé sur un autre service
            // ne doit pas bloquer l'agent sur ce service-ci.
            var hasOpenTicket = await _context.Tickets.AsNoTracking().AnyAsync(
                t => t.CounterId == agentCounterId.Value
                  && t.ServiceTypeId == serviceId
                  && t.Status == TicketStatus.Called, ct);
            if (hasOpenTicket)
            {
                TempData["Message"] = "Terminez, marquez absent ou transférez le ticket en cours avant d'appeler le suivant.";
                TempData["MessageType"] = "warning";
                return RedirectToAction(nameof(Index), new { serviceId });
            }
        }

        var ticket = await _context.Tickets
            .Where(t => t.ServiceTypeId == serviceId && t.Status == TicketStatus.Waiting)
            .OrderByDescending(t => t.IsPriority)
            .ThenBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (ticket is null)
        {
            TempData["Message"] = "Aucun ticket en attente.";
            TempData["MessageType"] = "info";
            return RedirectToAction(nameof(Index), new { serviceId });
        }

        var counterId = HttpContext.Session.GetInt32("AgentCounterId");
        Counter? counter = counterId.HasValue
            ? await _context.Counters.FirstOrDefaultAsync(c => c.Id == counterId.Value && c.IsActive, ct)
            : null;
        counter ??= await _context.Counters.FirstOrDefaultAsync(c => c.ServiceTypeId == serviceId && c.IsActive, ct);
        if (counter is null)
        {
            TempData["Message"] = "Aucun guichet actif disponible pour ce service.";
            TempData["MessageType"] = "warning";
            return RedirectToAction(nameof(Index), new { serviceId });
        }

        var service = await _context.Services.FindAsync(new object?[] { serviceId }, ct);
        var agentId = HttpContext.Session.GetInt32(AgentSessionKey);
        var agentName = HttpContext.Session.GetString("AgentName") ?? "Agent";
        var now = DateTime.Now;

        ticket.Status = TicketStatus.Called;
        ticket.CalledAt = now;
        ticket.CounterId = counter.Id;

        _context.CallHistories.Add(new CallHistory
        {
            TicketId = ticket.Id,
            TicketNumber = ticket.Number,
            ServiceName = service?.Name ?? string.Empty,
            CounterName = counter.Name,
            CalledAt = now,
            AgentId = agentId,
            AgentName = agentName
        });

        await _context.SaveChangesAsync(ct);

        // Lecture des settings avant le broadcast pour connaître la langue.
        var settingsForTts = await _settingsCache.GetAsync(ct);
        var langPrewarm = (settingsForTts.DefaultLanguage ?? "fr").ToLowerInvariant();
        var genderPrewarm = settingsForTts.VoiceGender ?? "female";

        // Envoie les données du ticket en premier pour que le Display joue
        // le son et l'effet wow IMMÉDIATEMENT, sans attendre le round-trip HTTP.
        await _hub.Clients.All.SendAsync("TicketCalled",
            ticket.Number, counter.Name, service?.Name ?? string.Empty,
            langPrewarm, now.Ticks.ToString(),
            cancellationToken: ct);
        await _hub.Clients.All.SendAsync("RefreshQueue", cancellationToken: ct);

        // ── Pré-warm Piper TTS : génération anticipée du WAV ─────────
        // Quand l'écran reçoit RefreshQueue, il appelle /Tts/Speak. Piper
        // met 200-1500 ms à synthétiser → délai audible avant la voix.
        // En lançant la synthèse ICI en fire-and-forget, le WAV est
        // souvent déjà en cache disque quand le client le demande
        // (latence de propagation SignalR + fetch state ~ 100-300 ms).
        // Résultat : la voix démarre quasi instantanément côté TV.
        var ticketNumberPrewarm = ticket.Number;
        var counterNamePrewarm = counter.Name;
        var serviceNamePrewarm = service?.Name ?? string.Empty;
        var piperLocal = _piper;
        var loggerLocal = _logger;
        _ = Task.Run(async () =>
        {
            try
            {
                using var ctsPrewarm = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                // On pré-génère pour la langue par défaut. Si une autre langue
                // est ensuite demandée par le client, Piper synthétisera à la
                // volée comme avant (pas régression).
                var ann = Nouba.Helpers.AnnouncementTextBuilder.Build(ticketNumberPrewarm, counterNamePrewarm, serviceNamePrewarm, langPrewarm);
                if (!string.IsNullOrWhiteSpace(ann))
                {
                    await piperLocal.SynthesizeAsync(ann, langPrewarm, genderPrewarm, ctsPrewarm.Token);
                }
            }
            catch (Exception ex)
            {
                loggerLocal.LogDebug(ex, "Piper prewarm KO ticket {Number}", ticketNumberPrewarm);
            }
        });

        TempData["Message"] = $"Ticket {ticket.Number} appelé — {counter.Name}.";
        TempData["MessageType"] = "success";
        return RedirectToAction(nameof(Index), new { serviceId });
        }
        finally
        {
            CallNextSemaphore.Release();
        }
    }

    // ── Recall (re-announce) ──────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recall(int ticketId, int serviceId)
    {
        if (!IsLoggedIn()) return RedirectToAction(nameof(Login));
        var ct = HttpContext.RequestAborted;

        if (!IsAuthorizedService(serviceId)) return ForbiddenAgentAction(CurrentAgentServiceId() ?? serviceId);

        var ticket = await _context.Tickets
            .Include(t => t.Counter)
            .Include(t => t.ServiceType)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.ServiceTypeId == serviceId, ct);
        if (ticket is not null && ticket.Status == TicketStatus.Called)
        {
            var now = DateTime.Now;
            ticket.CalledAt = now;
            _context.CallHistories.Add(new CallHistory
            {
                TicketId = ticket.Id,
                TicketNumber = ticket.Number,
                ServiceName = ticket.ServiceType?.Name ?? string.Empty,
                CounterName = ticket.Counter?.Name ?? HttpContext.Session.GetString("AgentCounterName") ?? "Guichet",
                CalledAt = now,
                AgentId = HttpContext.Session.GetInt32(AgentSessionKey),
                AgentName = HttpContext.Session.GetString("AgentName") ?? "Agent"
            });
            await _context.SaveChangesAsync(ct);
            var recallSettings = await _settingsCache.GetAsync(ct);
            var recallLang = (recallSettings.DefaultLanguage ?? "fr").ToLowerInvariant();
            var recallCounter = ticket.Counter?.Name ?? HttpContext.Session.GetString("AgentCounterName") ?? "Guichet";
            await _hub.Clients.All.SendAsync("TicketCalled",
                ticket.Number, recallCounter, ticket.ServiceType?.Name ?? string.Empty,
                recallLang, now.Ticks.ToString(),
                cancellationToken: ct);
            await _hub.Clients.All.SendAsync("RefreshQueue", cancellationToken: ct);
            TempData["Message"] = $"Ticket {ticket.Number} rappelé.";
            TempData["MessageType"] = "warning";
        }

        return RedirectToAction(nameof(Index), new { serviceId });
    }

    // ── Finish ────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finish(int ticketId, int serviceId)
    {
        if (!IsLoggedIn()) return RedirectToAction(nameof(Login));
        var ct = HttpContext.RequestAborted;

        if (!IsAuthorizedService(serviceId)) return ForbiddenAgentAction(CurrentAgentServiceId() ?? serviceId);

        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.ServiceTypeId == serviceId, ct);
        if (ticket is not null)
        {
            ticket.Status = TicketStatus.Finished;
            await _context.SaveChangesAsync(ct);
            await _hub.Clients.All.SendAsync("RefreshQueue", cancellationToken: ct);
            TempData["Message"] = $"Ticket {ticket.Number} traité.";
            TempData["MessageType"] = "success";
        }
        return RedirectToAction(nameof(Index), new { serviceId });
    }

    // ── Mark absent ───────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAbsent(int ticketId, int serviceId)
    {
        if (!IsLoggedIn()) return RedirectToAction(nameof(Login));
        var ct = HttpContext.RequestAborted;

        if (!IsAuthorizedService(serviceId)) return ForbiddenAgentAction(CurrentAgentServiceId() ?? serviceId);

        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.ServiceTypeId == serviceId, ct);
        if (ticket is not null)
        {
            ticket.Status = TicketStatus.Absent;
            await _context.SaveChangesAsync(ct);
            await _hub.Clients.All.SendAsync("RefreshQueue", cancellationToken: ct);
            TempData["Message"] = $"Ticket {ticket.Number} marqué absent.";
            TempData["MessageType"] = "secondary";
        }
        return RedirectToAction(nameof(Index), new { serviceId });
    }

    // ── Transfer ticket to another service ───────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transfer(int ticketId, int targetServiceId, int sourceServiceId)
    {
        if (!IsLoggedIn()) return RedirectToAction(nameof(Login));
        var ct = HttpContext.RequestAborted;

        if (!IsAuthorizedService(sourceServiceId)) return ForbiddenAgentAction(CurrentAgentServiceId() ?? sourceServiceId);

        var ticket = await _context.Tickets.Include(t => t.ServiceType)
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.ServiceTypeId == sourceServiceId, ct);
        var targetService = await _context.Services.FirstOrDefaultAsync(s => s.Id == targetServiceId && s.IsActive, ct);
        if (ticket is not null && targetService is not null)
        {
            var oldNumber = ticket.Number;
            var ticketDay = DateTime.Today.ToString("yyyy-MM-dd");
            var saved = false;

            for (var attempt = 1; attempt <= 20 && !saved; attempt++)
            {
                ticket.Number = await _numberAllocator.AllocateAsync(_context, targetService, ticketDay, ct);
                ticket.ServiceTypeId = targetServiceId;
                ticket.TicketDay = ticketDay;
                ticket.Status = TicketStatus.Waiting;
                ticket.CounterId = null;
                ticket.CalledAt = null;

                try
                {
                    await _context.SaveChangesAsync(ct);
                    saved = true;
                }
                catch (DbUpdateException) when (attempt < 20)
                {
                    // Detach the entry so EF doesn't carry over the conflicting state.
                    // On the next loop iteration, we re-apply all fields with a new number.
                    _context.Entry(ticket).State = EntityState.Detached;
                    ticket = await _context.Tickets.Include(t => t.ServiceType)
                        .FirstOrDefaultAsync(t => t.Id == ticketId, ct);
                    if (ticket is null) break;
                    await Task.Delay(20 * attempt, ct);
                }
            }

            if (!saved || ticket is null)
                throw new InvalidOperationException("Impossible de transférer le ticket avec un numéro unique après plusieurs tentatives.");

            await _hub.Clients.All.SendAsync("RefreshQueue", cancellationToken: ct);
            TempData["Message"] = $"Ticket {oldNumber} transféré vers {targetService.Name} sous le numéro {ticket!.Number}.";
            TempData["MessageType"] = "info";
        }
        return RedirectToAction(nameof(Index), new { serviceId = sourceServiceId });
    }

    // ── Live queue count API (compat ascendante) ─────────────────────
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> QueueState(int serviceId)
    {
        var ct = HttpContext.RequestAborted;
        // Une seule requête grâce au GroupBy SQL.
        var counts = await _context.Tickets.AsNoTracking()
            .Where(t => t.ServiceTypeId == serviceId && (t.Status == TicketStatus.Waiting || t.Status == TicketStatus.Called))
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Json(new
        {
            waiting = counts.FirstOrDefault(c => c.Status == TicketStatus.Waiting)?.Count ?? 0,
            called = counts.FirstOrDefault(c => c.Status == TicketStatus.Called)?.Count ?? 0
        });
    }
}
