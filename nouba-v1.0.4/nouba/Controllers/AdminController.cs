using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Helpers;
using Nouba.Hubs;
using Nouba.Infrastructure;
using Nouba.Models;
using Nouba.Services;
using Nouba.Security;

namespace Nouba.Controllers;

public class AdminController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly AppStoragePaths _storagePaths;
    private readonly UiSettingsCache _settingsCache;
    private readonly IHubContext<QueueHub> _hub;
    private readonly LoginRateLimiter _loginRateLimiter;
    private const string SessionKey = "AdminUserId";
    private const string SessionRoleKey = "AdminRole";
    public const string RoleProvider = "fournisseur";
    public const string RoleClient = "client";

    private readonly CloudflareTunnelService _tunnel;

    public AdminController(AppDbContext context, IWebHostEnvironment environment, AppStoragePaths storagePaths, UiSettingsCache settingsCache, IHubContext<QueueHub> hub, LoginRateLimiter loginRateLimiter, CloudflareTunnelService tunnel)
    {
        _context = context;
        _environment = environment;
        _storagePaths = storagePaths;
        _settingsCache = settingsCache;
        _hub = hub;
        _loginRateLimiter = loginRateLimiter;
        _tunnel = tunnel;
    }

    private bool IsLoggedIn() => HttpContext.Session.GetInt32(SessionKey).HasValue;
    private IActionResult RequireLogin() => RedirectToAction(nameof(Login));

    /// <summary>Vrai si l'administrateur connecté a le rôle fournisseur (accès technique total).</summary>
    private bool IsProvider() => string.Equals(HttpContext.Session.GetString(SessionRoleKey), RoleProvider, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Garde-fou serveur des actions techniques réservées au fournisseur.
    /// Renvoie null si l'accès est autorisé, sinon un IActionResult de refus.
    /// </summary>
    private IActionResult? GuardProvider()
    {
        if (!IsLoggedIn()) return RequireLogin();
        if (!IsProvider())
        {
            TempData["Error"] = "Réglage technique réservé à l'administrateur fournisseur.";
            return RedirectToIndexTab("dashboard");
        }
        return null;
    }

    private IActionResult RedirectToIndexTab(string? tab = null)
    {
        var url = Url.Action(nameof(Index), "Admin") ?? "/Admin/Index";
        if (string.IsNullOrWhiteSpace(tab)) return Redirect(url);
        var cleanTab = tab.StartsWith("tab-", StringComparison.OrdinalIgnoreCase) ? tab[4..] : tab;
        return Redirect($"{url}?tab={Uri.EscapeDataString(cleanTab)}");
    }


    private async Task<bool> CanActivateOneMoreAgentAsync(int? currentAgentId = null)
    {
        var license = LicenseManager.CheckLicense(_storagePaths);
        if (!license.IsValid || license.Payload is null) return false;

        var activeAgents = await _context.Agents
            .Where(a => a.IsActive && (!currentAgentId.HasValue || a.Id != currentAgentId.Value))
            .CountAsync();

        return activeAgents + 1 <= license.Payload.MaxAgents;
    }

    private string AgentLimitMessage()
    {
        var license = LicenseManager.CheckLicense(_storagePaths);
        var max = license.Payload?.MaxAgents ?? 0;
        return max > 0
            ? $"Limite de licence atteinte : maximum {max} agent(s) actif(s)."
            : "Licence invalide : impossible d'activer un agent.";
    }

    // ── Auth ──────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Login()
    {
        if (IsLoggedIn()) return RedirectToAction(nameof(Index));
        ViewBag.RequiresSetup = !await _context.AdminUsers.AnyAsync(a => a.IsActive);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Setup(string fullName, string login, string password, string confirmPassword)
    {
        if (await _context.AdminUsers.AnyAsync(a => a.IsActive)) return RedirectToAction(nameof(Login));
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        { TempData["Error"] = "Tous les champs sont obligatoires."; return RedirectToAction(nameof(Login)); }
        if (password != confirmPassword)
        { TempData["Error"] = "Les mots de passe ne correspondent pas."; return RedirectToAction(nameof(Login)); }

        var hash = PasswordHasher.HashPassword(password);
        // Le tout premier compte (créé à l'installation par l'intégrateur) est
        // un compte FOURNISSEUR : accès technique total. Il pourra ensuite créer
        // le(s) compte(s) CLIENT depuis l'onglet Système.
        _context.AdminUsers.Add(new AdminUser { FullName = fullName.Trim(), Username = login.Trim(), PasswordHash = hash.Hash, PasswordSalt = hash.Salt, IsActive = true, CreatedAt = DateTime.UtcNow, Role = RoleProvider });
        await _context.SaveChangesAsync();
        TempData["Success"] = "Compte administrateur (fournisseur) créé. Connectez-vous.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string login, string password)
    {
        if (_loginRateLimiter.IsBlocked(HttpContext, "admin", login, out var remaining))
        { TempData["Error"] = $"Trop de tentatives. Réessayez dans {Math.Ceiling(remaining.TotalMinutes)} minute(s)."; return RedirectToAction(nameof(Login)); }

        var normalizedLogin = (login ?? string.Empty).Trim();
        await _loginRateLimiter.DelayIfNeededAsync(HttpContext, "admin", normalizedLogin, HttpContext.RequestAborted);
        var admin = await _context.AdminUsers.FirstOrDefaultAsync(a => a.IsActive && a.Username == normalizedLogin);
        if (admin is null || !PasswordHasher.VerifyPassword(password ?? string.Empty, admin.PasswordHash, admin.PasswordSalt))
        { _loginRateLimiter.RegisterFailure(HttpContext, "admin", normalizedLogin); TempData["Error"] = "Identifiants invalides."; return RedirectToAction(nameof(Login)); }

        _loginRateLimiter.RegisterSuccess(HttpContext, "admin", normalizedLogin);
        admin.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        HttpContext.Session.SetInt32(SessionKey, admin.Id);
        HttpContext.Session.SetString("AdminName", admin.FullName);
        HttpContext.Session.SetString("AdminUsername", admin.Username);
        HttpContext.Session.SetString(SessionRoleKey, string.IsNullOrWhiteSpace(admin.Role) ? RoleClient : admin.Role.Trim().ToLowerInvariant());
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Logout() { HttpContext.Session.Clear(); return RedirectToAction(nameof(Login)); }

    // ── Dashboard ─────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        if (!IsLoggedIn()) return RequireLogin();

        var today = DateTime.Today;
        var services = await _context.Services.Where(s => s.IsActive).OrderBy(s => s.DisplayOrder).ToListAsync();
        // Pour l'admin on charge TOUS les guichets et agents (y compris inactifs) afin de
        // permettre la réactivation/réassignation. Les listes d'assignation côté UI filtrent
        // explicitement sur les actifs là où c'est nécessaire.
        var counters = await _context.Counters.Include(c => c.ServiceType).OrderBy(c => c.Name).ToListAsync();
        var agents = await _context.Agents.Include(a => a.Counter).Include(a => a.ServiceType).OrderBy(a => a.FullName).ToListAsync();
        var settings = await _context.UiSettings.FirstAsync();

        // Dashboard KPIs
        var ticketsToday = await _context.Tickets.Where(t => t.CreatedAt >= today).ToListAsync();
        var callHistoryTodayAll = await _context.CallHistories.Where(c => c.CalledAt >= today).OrderByDescending(c => c.CalledAt).ToListAsync();
        var callHistoryToday = callHistoryTodayAll.Take(20).ToList();

        var waitingNow = ticketsToday.Count(t => t.Status == TicketStatus.Waiting);
        var calledNow = ticketsToday.Count(t => t.Status == TicketStatus.Called);
        var servedToday = ticketsToday.Count(t => t.Status == TicketStatus.Finished);
        var absentToday = ticketsToday.Count(t => t.Status == TicketStatus.Absent);
        var totalToday = ticketsToday.Count;

        // Per-service stats
        var serviceStats = services.Select(s => new
        {
            Service = s,
            Total = ticketsToday.Count(t => t.ServiceTypeId == s.Id),
            Waiting = ticketsToday.Count(t => t.ServiceTypeId == s.Id && t.Status == TicketStatus.Waiting),
            Served = ticketsToday.Count(t => t.ServiceTypeId == s.Id && t.Status == TicketStatus.Finished),
            Absent = ticketsToday.Count(t => t.ServiceTypeId == s.Id && t.Status == TicketStatus.Absent),
        }).ToList();

        var agentStats = agents.Select(a => new AgentDashboardStat
        {
            Agent = a,
            Calls = callHistoryTodayAll.Count(h => h.AgentId == a.Id || h.AgentName == a.FullName),
            Served = ticketsToday.Count(t => t.Status == TicketStatus.Finished && t.CounterId == a.CounterId),
            WaitingService = ticketsToday.Count(t => t.Status == TicketStatus.Waiting && t.ServiceTypeId == a.ServiceTypeId),
            LastCall = callHistoryTodayAll.FirstOrDefault(h => h.AgentId == a.Id || h.AgentName == a.FullName)?.CalledAt
        }).ToList();

        // Average wait time
        var processedTickets = ticketsToday.Where(t => t.CalledAt.HasValue).ToList();
        var avgWait = processedTickets.Count > 0
            ? processedTickets.Average(t => (t.CalledAt!.Value - t.CreatedAt).TotalMinutes)
            : 0;

        // ── Données enrichies pour le dashboard pro ──────────────
        // 1) Volume horaire (buckets 0-23h)
        var hourlyBuckets = new int[24];
        foreach (var t in ticketsToday)
            hourlyBuckets[Math.Clamp(t.CreatedAt.Hour, 0, 23)]++;

        // 2) Pic d'activité
        var peakHour = -1;
        var peakHourCount = 0;
        for (var h = 0; h < 24; h++)
            if (hourlyBuckets[h] > peakHourCount) { peakHourCount = hourlyBuckets[h]; peakHour = h; }

        // 3) Stats hier (pour comparaison)
        var yesterdayStart = today.AddDays(-1);
        var yesterdayTickets = await _context.Tickets.AsNoTracking()
            .Where(t => t.CreatedAt >= yesterdayStart && t.CreatedAt < today).ToListAsync();
        var yesterdayTotal = yesterdayTickets.Count;
        var yesterdayServed = yesterdayTickets.Count(t => t.Status == TicketStatus.Finished);

        // 4) Service le + demandé
        var topService = serviceStats.OrderByDescending(s => s.Total).FirstOrDefault();

        // 5) Taux d'absence (qualité)
        var absenceRate = totalToday > 0 ? Math.Round(absentToday * 100.0 / totalToday, 1) : 0.0;

        // 6) Statut imprimante (pour widget rapide)
        ViewBag.PrinterEnabled = settings.PrinterEnabled;
        ViewBag.PrintMode = settings.PrintMode;
        ViewBag.PrinterIp = settings.PrinterIp;
        ViewBag.PrinterPort = settings.PrinterPort;

        ViewBag.HourlyBuckets   = hourlyBuckets;
        ViewBag.PeakHour        = peakHour;
        ViewBag.PeakHourCount   = peakHourCount;
        ViewBag.YesterdayTotal  = yesterdayTotal;
        ViewBag.YesterdayServed = yesterdayServed;
        ViewBag.AbsenceRate     = absenceRate;
        ViewBag.TopServiceName  = topService?.Service?.Name ?? "—";
        ViewBag.TopServiceCount = (int?)topService?.Total ?? 0;

        ViewBag.Counters = counters;
        ViewBag.Agents = agents;
        ViewBag.Settings = settings;
        ViewBag.AdminName = HttpContext.Session.GetString("AdminName") ?? "Admin";
        ViewBag.AdminLogin = HttpContext.Session.GetString("AdminUsername") ?? "admin";
        // Rôle de l'admin connecté + liste des comptes (réservée au fournisseur).
        ViewBag.IsProvider = IsProvider();
        ViewBag.AdminRole = IsProvider() ? RoleProvider : RoleClient;
        if (IsProvider())
        {
            ViewBag.AdminAccounts = await _context.AdminUsers
                .OrderByDescending(a => a.Role == RoleProvider)
                .ThenBy(a => a.Username)
                .ToListAsync();
        }
        ViewBag.WaitingNow = waitingNow;
        ViewBag.CalledNow = calledNow;
        ViewBag.ServedToday = servedToday;
        ViewBag.AbsentToday = absentToday;
        ViewBag.TotalToday = totalToday;
        ViewBag.AvgWaitMinutes = Math.Round(avgWait, 1);
        ViewBag.ServiceStats = serviceStats;
        ViewBag.AgentStats = agentStats;
        ViewBag.RecentCalls = callHistoryToday;
        ViewBag.DatabasePath = _storagePaths.DatabasePath;
        // ── IPs locales détectées (pour la config réseau premium) ──
        ViewBag.LocalIpAddresses = GetLocalIpAddresses();
        ViewBag.LicenseDisplay = LicenseInfo.LicenseDisplay;
        ViewBag.ProductVersion = LicenseInfo.FullVersion;
        var licStatus = LicenseManager.CheckLicense(_storagePaths);
        ViewBag.LicenseValid     = licStatus.IsValid;
        ViewBag.LicenseMachineId = licStatus.MachineId;
        ViewBag.LicensePrimaryMac = licStatus.PrimaryMac ?? "Non détectée";

        // Détails du payload de licence pour affichage admin
        var licPayload = licStatus.Payload;
        if (licPayload != null)
        {
            ViewBag.LicenseClient    = string.IsNullOrWhiteSpace(licPayload.Client) ? "—" : licPayload.Client;
            ViewBag.LicenseProduct   = licPayload.Product;
            ViewBag.LicenseIssuedAt  = licPayload.IssuedAt.LocalDateTime;
            ViewBag.LicenseExpiresAt = licPayload.ExpiresAt.LocalDateTime;
            ViewBag.LicenseMaxAgents = licPayload.MaxAgents;
            var daysRemaining = (int)Math.Floor((licPayload.ExpiresAt.UtcDateTime.Date - DateTime.UtcNow.Date).TotalDays);
            ViewBag.LicenseDaysRemaining = daysRemaining;
            ViewBag.LicenseExpiringSoon  = daysRemaining >= 0 && daysRemaining <= 30;
        }
        else
        {
            ViewBag.LicenseClient    = "—";
            ViewBag.LicenseProduct   = "—";
            ViewBag.LicenseIssuedAt  = (DateTime?)null;
            ViewBag.LicenseExpiresAt = (DateTime?)null;
            ViewBag.LicenseMaxAgents = 0;
            ViewBag.LicenseDaysRemaining = 0;
            ViewBag.LicenseExpiringSoon  = false;
        }
        // Nombre d'agents configurés / actifs (pour comparer à MaxAgents)
        ViewBag.LicenseActiveAgents     = agents.Count(a => a.IsActive);
        ViewBag.LicenseConfiguredAgents = agents.Count;
        ViewBag.BackupsPath = _storagePaths.BackupsPath;
        ViewBag.UploadsPath = _storagePaths.UploadsPath;
        // Dernier fichier de sauvegarde
        var backupDir = new System.IO.DirectoryInfo(_storagePaths.BackupsPath);
        var lastBackup = backupDir.Exists
            ? backupDir.GetFiles("nouba_*.db").OrderByDescending(f => f.CreationTimeUtc).FirstOrDefault()
            : null;
        ViewBag.LastBackupFile = lastBackup?.Name ?? "Aucune sauvegarde";
        ViewBag.LastBackupDate = lastBackup != null ? lastBackup.CreationTime.ToString("dd/MM/yyyy HH:mm") : "—";
        ViewBag.BackupCount = backupDir.Exists ? backupDir.GetFiles("nouba_*.db").Length : 0;

        // ── Santé système (alerte admin) ────────────────────────────
        var (diskFreeMb, diskTotalMb, diskWarn) = HealthController.GetDiskInfo(_storagePaths.DataRoot);
        bool dbOk;
        try { dbOk = await _context.Database.CanConnectAsync(); }
        catch { dbOk = false; }
        ViewBag.SystemDiskFreeMb  = diskFreeMb;
        ViewBag.SystemDiskTotalMb = diskTotalMb;
        ViewBag.SystemDiskWarn    = diskWarn;
        ViewBag.SystemDbOk        = dbOk;
        ViewBag.SystemHasAlert    = diskWarn || !dbOk;
        ViewBag.TunnelStatus      = _tunnel.Status;
        ViewBag.TunnelUrl         = _tunnel.TunnelUrl;

        return View(services);
    }

    // ── Services ──────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddService(ServiceType model, IFormFile? serviceLogoFile)
    {
        if (!IsLoggedIn()) return RequireLogin();
        if (!ModelState.IsValid) { TempData["Error"] = "Données invalides."; return RedirectToAction(nameof(Index)); }
        model.Code = model.Code.Trim().ToUpperInvariant();
        model.ButtonColor = string.IsNullOrWhiteSpace(model.ButtonColor) ? "#6366f1" : model.ButtonColor;
        model.TextColor = string.IsNullOrWhiteSpace(model.TextColor) ? "#ffffff" : model.TextColor;
        // Le formulaire d'ajout n'envoie pas IsActive ; sans cette ligne, le binding le mettrait
        // à false par défaut et le service serait invisible partout.
        model.IsActive = true;
        model.Emoji = string.IsNullOrWhiteSpace(model.Emoji) ? null : model.Emoji.Trim();

        // Vérification d'unicité : on ne tient compte QUE des services actifs.
        // Sinon un service précédemment désactivé (et invisible dans l'admin) bloquerait
        // la création d'un nouveau service avec le même code.
        var conflict = await _context.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IsActive && s.Code == model.Code);
        if (conflict != null)
        {
            TempData["Error"] = $"Le code « {model.Code} » est déjà utilisé par le service « {conflict.Name} ». Choisissez un autre code.";
            return RedirectToIndexTab("tab-services");
        }

        // Si un service inactif portait ce code, on le réactive plutôt que de bloquer
        // (cas typique : utilisateur a supprimé puis veut recréer le même service).
        var dormant = await _context.Services
            .FirstOrDefaultAsync(s => !s.IsActive && s.Code == model.Code);
        if (dormant != null)
        {
            dormant.IsActive = true;
            dormant.Name = model.Name;
            dormant.DisplayOrder = model.DisplayOrder;
            dormant.ButtonColor = model.ButtonColor;
            dormant.TextColor = model.TextColor;
            dormant.Emoji = string.IsNullOrWhiteSpace(model.Emoji) ? null : model.Emoji.Trim();
            dormant.NameAr = string.IsNullOrWhiteSpace(model.NameAr) ? null : model.NameAr.Trim();
            dormant.NameTz = string.IsNullOrWhiteSpace(model.NameTz) ? null : model.NameTz.Trim();
            dormant.NameEn = string.IsNullOrWhiteSpace(model.NameEn) ? null : model.NameEn.Trim();
            dormant.LogoUrl = await ResolveMediaPathAsync(serviceLogoFile, model.LogoUrl, dormant.LogoUrl, $"service_{dormant.Id}");
            await _context.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("ServicesChanged");
            TempData["Success"] = $"Service « {dormant.Name} » réactivé.";
            return RedirectToIndexTab("tab-services");
        }

        model.LogoUrl = await ResolveMediaPathAsync(serviceLogoFile, model.LogoUrl, model.LogoUrl, "service_new");
        _context.Services.Add(model);
        await _context.SaveChangesAsync();
        await _hub.Clients.All.SendAsync("ServicesChanged");
        TempData["Success"] = $"Service « {model.Name} » ajouté.";
        return RedirectToIndexTab("tab-services");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateService(int id, string name, string code, int displayOrder, string? nameAr, string? nameTz, string? nameEn)
    {
        if (!IsLoggedIn()) return RequireLogin();
        var service = await _context.Services.FindAsync(id);
        if (service is null) { TempData["Error"] = "Service introuvable."; return RedirectToAction(nameof(Index)); }
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
        { TempData["Error"] = "Nom et code obligatoires."; return RedirectToAction(nameof(Index)); }
        var normalizedCode = code.Trim().ToUpperInvariant();
        if (await _context.Services.AnyAsync(s => s.Id != id && s.Code == normalizedCode))
        { TempData["Error"] = "Ce code est déjà utilisé."; return RedirectToAction(nameof(Index)); }
        service.Name = name.Trim();
        service.Code = normalizedCode;
        service.DisplayOrder = displayOrder;
        service.NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        service.NameTz = string.IsNullOrWhiteSpace(nameTz) ? null : nameTz.Trim();
        service.NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        await _context.SaveChangesAsync();
        await _hub.Clients.All.SendAsync("ServicesChanged");
        TempData["Success"] = "Service mis à jour.";
        return RedirectToIndexTab("tab-services");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateServiceStyle(int id, string buttonColor, string textColor, string? logoUrl, string? emoji, IFormFile? serviceLogoFile)
    {
        if (!IsLoggedIn()) return RequireLogin();
        var service = await _context.Services.FindAsync(id);
        if (service is null) { TempData["Error"] = "Service introuvable."; return RedirectToAction(nameof(Index)); }
        service.ButtonColor = string.IsNullOrWhiteSpace(buttonColor) ? "#0d6efd" : buttonColor;
        service.TextColor = string.IsNullOrWhiteSpace(textColor) ? "#ffffff" : textColor;
        service.Emoji = string.IsNullOrWhiteSpace(emoji) ? null : emoji.Trim();
        service.LogoUrl = await ResolveMediaPathAsync(serviceLogoFile, logoUrl, service.LogoUrl, $"service_{id}");
        await _context.SaveChangesAsync();
        await _hub.Clients.All.SendAsync("ServicesChanged");
        TempData["Success"] = $"Style de '{service.Name}' mis à jour.";
        return RedirectToIndexTab("tab-services");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteService(int id, int? replacementServiceId)
    {
        if (!IsLoggedIn()) return RequireLogin();
        var service = await _context.Services.FindAsync(id);
        if (service is null) { TempData["Error"] = "Service introuvable."; return RedirectToIndexTab("tab-services"); }

        // Politique : suppression du service, de ses tickets et de ses guichets,
        // mais PRÉSERVATION des agents (réassignation ou désactivation).
        var tickets = await _context.Tickets.Where(t => t.ServiceTypeId == id).ToListAsync();
        var ticketIds = tickets.Select(t => t.Id).ToList();
        var histories = await _context.CallHistories
            .Where(h => ticketIds.Contains(h.TicketId) || h.ServiceName == service.Name)
            .ToListAsync();
        var counters = await _context.Counters.Where(c => c.ServiceTypeId == id).ToListAsync();
        var counterIds = counters.Select(c => c.Id).ToList();
        var agents = await _context.Agents.Where(a => a.ServiceTypeId == id || counterIds.Contains(a.CounterId)).ToListAsync();

        ServiceType? replacement = null;
        if (replacementServiceId.HasValue)
            replacement = await _context.Services.FirstOrDefaultAsync(s => s.Id == replacementServiceId.Value && s.IsActive);
        replacement ??= await _context.Services.FirstOrDefaultAsync(s => s.Id != id && s.IsActive);

        var fallbackCounter = replacement is null ? null
            : await _context.Counters.FirstOrDefaultAsync(c => c.ServiceTypeId == replacement.Id && c.IsActive);

        foreach (var agent in agents)
        {
            if (replacement is not null && fallbackCounter is not null)
            {
                agent.ServiceTypeId = replacement.Id;
                agent.CounterId = fallbackCounter.Id;
            }
            else
            {
                agent.IsActive = false;
            }
        }

        _context.CallHistories.RemoveRange(histories);
        _context.Tickets.RemoveRange(tickets);
        _context.Counters.RemoveRange(counters);
        _context.Services.Remove(service);
        await _context.SaveChangesAsync();
        await _hub.Clients.All.SendAsync("ServicesChanged");
        // Les agents réaffectés/désactivés doivent aussi se mettre à jour (#6).
        foreach (var a in agents)
            await _hub.Clients.All.SendAsync("AgentUpdated", a.Id);

        TempData["Success"] = replacement is not null && fallbackCounter is not null
            ? $"Service supprimé. {agents.Count} agent(s) réaffecté(s) à « {replacement.Name} »."
            : $"Service supprimé. {agents.Count} agent(s) désactivé(s).";
        return RedirectToIndexTab("tab-services");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCounter(string name, int serviceTypeId)
    {
        if (!IsLoggedIn()) return RequireLogin();
        if (string.IsNullOrWhiteSpace(name)) { TempData["Error"] = "Nom obligatoire."; return RedirectToAction(nameof(Index)); }
        _context.Counters.Add(new Counter { Name = name.Trim(), ServiceTypeId = serviceTypeId, IsActive = true });
        await _context.SaveChangesAsync();
        TempData["Success"] = "Guichet ajouté.";
        return RedirectToIndexTab("tab-counters");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCounter(int id, string? name, int serviceTypeId)
    {
        if (!IsLoggedIn()) return RequireLogin();
        var counter = await _context.Counters.FindAsync(id);
        if (counter is null) { TempData["Error"] = "Guichet introuvable."; return RedirectToAction(nameof(Index)); }
        if (!string.IsNullOrWhiteSpace(name)) counter.Name = name.Trim();
        counter.ServiceTypeId = serviceTypeId;
        var linkedAgents = await _context.Agents.Where(a => a.CounterId == id).ToListAsync();
        foreach (var agent in linkedAgents) agent.ServiceTypeId = serviceTypeId;
        await _context.SaveChangesAsync();
        TempData["Success"] = "Guichet mis à jour.";
        return RedirectToIndexTab("tab-counters");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCounter(int id)
    {
        if (!IsLoggedIn()) return RequireLogin();
        var counter = await _context.Counters.FindAsync(id);
        if (counter is null) { TempData["Error"] = "Guichet introuvable."; return RedirectToIndexTab("tab-counters"); }

        // Politique : préserver les agents en les réassignant ou désactivant.
        var tickets = await _context.Tickets.Where(t => t.CounterId == id).ToListAsync();
        var ticketIds = tickets.Select(t => t.Id).ToList();
        // Only delete histories tied to tickets of THIS counter, not all counters sharing the same name.
        var histories = await _context.CallHistories
            .Where(h => ticketIds.Contains(h.TicketId))
            .ToListAsync();
        var agents = await _context.Agents.Where(a => a.CounterId == id).ToListAsync();

        var fallbackCounter = await _context.Counters
            .FirstOrDefaultAsync(c => c.Id != id && c.ServiceTypeId == counter.ServiceTypeId && c.IsActive);

        foreach (var agent in agents)
        {
            if (fallbackCounter is not null)
            {
                agent.CounterId = fallbackCounter.Id;
                agent.ServiceTypeId = fallbackCounter.ServiceTypeId;
            }
            else
            {
                agent.IsActive = false;
            }
        }

        _context.CallHistories.RemoveRange(histories);
        _context.Tickets.RemoveRange(tickets);
        _context.Counters.Remove(counter);
        await _context.SaveChangesAsync();

        TempData["Success"] = fallbackCounter is not null
            ? $"Guichet supprimé. {agents.Count} agent(s) réaffecté(s) à « {fallbackCounter.Name} »."
            : $"Guichet supprimé. {agents.Count} agent(s) désactivé(s).";
        return RedirectToIndexTab("tab-counters");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAgent(string fullName, string login, string password, int counterId)
    {
        if (!IsLoggedIn()) return RequireLogin();
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        { TempData["Error"] = "Tous les champs sont obligatoires."; return RedirectToIndexTab("tab-agents"); }
        if (await _context.Agents.AnyAsync(a => a.Login == login))
        { TempData["Error"] = "Ce login existe déjà."; return RedirectToIndexTab("tab-agents"); }

        var counter = await _context.Counters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == counterId && c.IsActive);
        if (counter is null)
        { TempData["Error"] = "Guichet actif introuvable."; return RedirectToIndexTab("tab-agents"); }

        if (!await CanActivateOneMoreAgentAsync())
        {
            TempData["Error"] = AgentLimitMessage();
            return RedirectToIndexTab("tab-agents");
        }

        var agentHash = PasswordHasher.HashPassword(password);
        _context.Agents.Add(new Agent
        {
            FullName = fullName.Trim(),
            Login = login.Trim(),
            Password = string.Empty,
            PasswordHash = agentHash.Hash,
            PasswordSalt = agentHash.Salt,
            CounterId = counter.Id,
            ServiceTypeId = counter.ServiceTypeId,
            IsActive = true
        });
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Agent ajouté et assigné au guichet {counter.Name}.";
        return RedirectToIndexTab("tab-agents");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAgent(int id, string fullName, string login, string? password, int counterId, bool isActive)
    {
        if (!IsLoggedIn()) return RequireLogin();
        var agent = await _context.Agents.FindAsync(id);
        if (agent is null) { TempData["Error"] = "Agent introuvable."; return RedirectToIndexTab("tab-agents"); }
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(login))
        { TempData["Error"] = "Nom et login obligatoires."; return RedirectToIndexTab("tab-agents"); }
        if (await _context.Agents.AnyAsync(a => a.Id != id && a.Login == login))
        { TempData["Error"] = "Ce login est déjà utilisé."; return RedirectToIndexTab("tab-agents"); }
        if (isActive && !agent.IsActive && !await CanActivateOneMoreAgentAsync(agent.Id))
        {
            TempData["Error"] = AgentLimitMessage();
            return RedirectToIndexTab("tab-agents");
        }
        agent.FullName = fullName.Trim();
        agent.Login = login.Trim();
        if (!string.IsNullOrWhiteSpace(password))
        {
            var agentHash = PasswordHasher.HashPassword(password);
            agent.Password = string.Empty;
            agent.PasswordHash = agentHash.Hash;
            agent.PasswordSalt = agentHash.Salt;
        }
        var counter = await _context.Counters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == counterId && c.IsActive);
        if (counter is null) { TempData["Error"] = "Guichet actif introuvable."; return RedirectToIndexTab("tab-agents"); }
        agent.CounterId = counter.Id;
        agent.ServiceTypeId = counter.ServiceTypeId;
        agent.IsActive = isActive;
        await _context.SaveChangesAsync();
        // Notifie l'agent connecté en temps réel : sa session sera invalidée
        // côté client (rechargement automatique avec la nouvelle affectation),
        // sans que l'agent ait besoin de se déconnecter/reconnecter manuellement.
        await _hub.Clients.All.SendAsync("AgentUpdated", agent.Id);
        TempData["Success"] = "Agent mis à jour.";
        return RedirectToIndexTab("tab-agents");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAgent(int id)
    {
        if (!IsLoggedIn()) return RequireLogin();
        var agent = await _context.Agents.FindAsync(id);
        if (agent is null) { TempData["Error"] = "Agent introuvable."; return RedirectToIndexTab("tab-agents"); }
        _context.Agents.Remove(agent);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Agent supprimé.";
        return RedirectToIndexTab("tab-agents");
    }

    /// <summary>
    /// Réassignation rapide d'un agent à un guichet, sans rouvrir la modale d'édition.
    /// Utilisé par le menu déroulant inline sur chaque ligne d'agent.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignAgentCounter(int agentId, int counterId)
    {
        if (!IsLoggedIn()) return RequireLogin();
        var agent = await _context.Agents.FindAsync(agentId);
        if (agent is null) { TempData["Error"] = "Agent introuvable."; return RedirectToIndexTab("tab-agents"); }
        var counter = await _context.Counters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == counterId && c.IsActive);
        if (counter is null) { TempData["Error"] = "Guichet actif introuvable."; return RedirectToIndexTab("tab-agents"); }

        if (!agent.IsActive && !await CanActivateOneMoreAgentAsync(agent.Id))
        {
            TempData["Error"] = AgentLimitMessage();
            return RedirectToIndexTab("tab-agents");
        }

        agent.CounterId = counter.Id;
        agent.ServiceTypeId = counter.ServiceTypeId;
        agent.IsActive = true;
        await _context.SaveChangesAsync();
        await _hub.Clients.All.SendAsync("AgentUpdated", agent.Id);

        TempData["Success"] = $"« {agent.FullName} » assigné(e) à « {counter.Name} ».";
        return RedirectToIndexTab("tab-agents");
    }

    // ── Settings ──────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 536870912, ValueLengthLimit = int.MaxValue)]
    [RequestSizeLimit(536870912)]
    public async Task<IActionResult> UpdateSettings(
        [FromForm] UiSettings input,
        [FromForm] string? officeState,
        IFormFile? backgroundImageFile,
        IFormFile? backgroundVideoFile,
        IFormFile? displayLogoFile,
        IFormFile? bannerImageFile,
        IFormFile? headerImageFile)
    {
        if (!IsLoggedIn()) return RequireLogin();
        var settings = await _context.UiSettings.FirstAsync();
        bool Has(string key) => Request.HasFormContentType && Request.Form.ContainsKey(key);
        string? Posted(string key) => Has(key) ? Request.Form[key].ToString() : null;
        bool Bool(string key, bool current) => Has(key) && Request.Form[key].Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "on" || v == "1") ? true : (Has(key) ? false : current);
        int IntRange(string key, int current, int min, int max, int fallback)
            => Has(key) && int.TryParse(Posted(key), out var v) && v >= min && v <= max ? v : (Has(key) ? fallback : current);
        double DoubleRange(string key, double current, double min, double max, double fallback)
            => Has(key) && double.TryParse(Posted(key), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) && v >= min && v <= max ? v : (Has(key) ? fallback : current);
        string Str(string key, string current, string fallback = "")
            => Has(key) ? (string.IsNullOrWhiteSpace(Posted(key)) ? fallback : Posted(key)!.Trim()) : current;
        bool HasDisplayHeaderTitle(string suffix)
            => Has("DisplayHeaderTitle" + suffix) || Has("DisplayTitle" + suffix) || Has("HeaderText" + suffix);
        string ResolvePostedDisplayHeaderTitle(string suffix, string? currentDisplayTitle, string? currentHeaderText)
        {
            var displayHeaderKey = "DisplayHeaderTitle" + suffix;
            if (Has(displayHeaderKey))
            {
                return Posted(displayHeaderKey)?.Trim() ?? string.Empty;
            }

            var postedKeys = new[]
            {
                (Key: "DisplayTitle" + suffix, Current: currentDisplayTitle ?? string.Empty),
                (Key: "HeaderText" + suffix, Current: currentHeaderText ?? string.Empty)
            };
            string? firstPosted = null;

            foreach (var item in postedKeys)
            {
                if (!Has(item.Key)) continue;
                var value = Posted(item.Key)?.Trim() ?? string.Empty;

                firstPosted ??= value;
                if (!string.Equals(value, item.Current.Trim(), StringComparison.Ordinal))
                {
                    return value;
                }
            }

            return firstPosted ?? currentDisplayTitle ?? string.Empty;
        }
        string? StrN(string key, string? current)
            => Has(key) ? Posted(key) : current;

        // Identité générale : préserve les valeurs si le formulaire vient d'un autre onglet.
        settings.SiteName = Str(nameof(input.SiteName), settings.SiteName, "Nouba");
        settings.ThemePreset = Str(nameof(input.ThemePreset), settings.ThemePreset, "clinique");
        settings.DefaultLanguage = NormalizeAdminLanguage(Str(nameof(input.DefaultLanguage), settings.DefaultLanguage, "fr"));

        // Borne
        settings.BorneTitleFr = Str(nameof(input.BorneTitleFr), settings.BorneTitleFr, settings.BorneTitleFr);
        settings.BorneTitleAr = Str(nameof(input.BorneTitleAr), settings.BorneTitleAr, settings.BorneTitleAr);
        settings.BorneTitleTz = Str(nameof(input.BorneTitleTz), settings.BorneTitleTz, settings.BorneTitleTz);
        settings.BorneTitleEn = Str(nameof(input.BorneTitleEn), settings.BorneTitleEn, settings.BorneTitleEn);
        settings.BorneSubtitleFr = Str(nameof(input.BorneSubtitleFr), settings.BorneSubtitleFr, settings.BorneSubtitleFr);
        settings.BorneSubtitleAr = Str(nameof(input.BorneSubtitleAr), settings.BorneSubtitleAr, settings.BorneSubtitleAr);
        settings.BorneSubtitleTz = Str(nameof(input.BorneSubtitleTz), settings.BorneSubtitleTz, settings.BorneSubtitleTz);
        settings.BorneSubtitleEn = Str(nameof(input.BorneSubtitleEn), settings.BorneSubtitleEn, settings.BorneSubtitleEn);
        if (Has("officeState")) settings.TicketsClosed = string.Equals(officeState, "closed", StringComparison.OrdinalIgnoreCase);
        else if (Has(nameof(input.TicketsClosed))) settings.TicketsClosed = Bool(nameof(input.TicketsClosed), settings.TicketsClosed);
        settings.TicketClosureMessageFr = Str(nameof(input.TicketClosureMessageFr), settings.TicketClosureMessageFr, settings.TicketClosureMessageFr);
        settings.TicketClosureMessageAr = Str(nameof(input.TicketClosureMessageAr), settings.TicketClosureMessageAr, settings.TicketClosureMessageAr);
        settings.TicketClosureMessageTz = Str(nameof(input.TicketClosureMessageTz), settings.TicketClosureMessageTz, settings.TicketClosureMessageTz);
        settings.TicketClosureMessageEn = Str(nameof(input.TicketClosureMessageEn), settings.TicketClosureMessageEn, settings.TicketClosureMessageEn);

        // Affichage / textes
        if (HasDisplayHeaderTitle("Fr"))
        {
            var title = ResolvePostedDisplayHeaderTitle("Fr", settings.DisplayTitleFr, settings.HeaderTextFr);
            settings.DisplayTitleFr = title;
            settings.HeaderTextFr = title;
        }

        if (HasDisplayHeaderTitle("Ar"))
        {
            var title = ResolvePostedDisplayHeaderTitle("Ar", settings.DisplayTitleAr, settings.HeaderTextAr);
            settings.DisplayTitleAr = title;
            settings.HeaderTextAr = title;
        }

        if (HasDisplayHeaderTitle("Tz"))
        {
            var title = ResolvePostedDisplayHeaderTitle("Tz", settings.DisplayTitleTz, settings.HeaderTextTz);
            settings.DisplayTitleTz = title;
            settings.HeaderTextTz = title;
        }

        if (HasDisplayHeaderTitle("En"))
        {
            var title = ResolvePostedDisplayHeaderTitle("En", settings.DisplayTitleEn, settings.HeaderTextEn);
            settings.DisplayTitleEn = title;
            settings.HeaderTextEn = title;
        }

        settings.DisplayBannerTextFr = StrN(nameof(input.DisplayBannerTextFr), settings.DisplayBannerTextFr);
        settings.DisplayBannerTextAr = StrN(nameof(input.DisplayBannerTextAr), settings.DisplayBannerTextAr);
        settings.DisplayBannerTextTz = StrN(nameof(input.DisplayBannerTextTz), settings.DisplayBannerTextTz);
        settings.DisplayBannerTextEn = StrN(nameof(input.DisplayBannerTextEn), settings.DisplayBannerTextEn);

        // Couleurs
        settings.DisplayAccentColor = Str(nameof(input.DisplayAccentColor), settings.DisplayAccentColor, settings.DisplayAccentColor);
        settings.DisplayCardColor = Str(nameof(input.DisplayCardColor), settings.DisplayCardColor, settings.DisplayCardColor);
        settings.DisplayHistoryColor = Str(nameof(input.DisplayHistoryColor), settings.DisplayHistoryColor, settings.DisplayHistoryColor);
        settings.DisplayTextColor = Str(nameof(input.DisplayTextColor), settings.DisplayTextColor, settings.DisplayTextColor);
        settings.DisplayTicketBgColor = Str(nameof(input.DisplayTicketBgColor), settings.DisplayTicketBgColor, settings.DisplayTicketBgColor);
        settings.DisplayFooterColor = Str(nameof(input.DisplayFooterColor), settings.DisplayFooterColor, settings.DisplayFooterColor);

        // Options — important : si une option n'est pas présente dans l'onglet posté, on conserve l'ancienne valeur.
        settings.ShowClockOnDisplay = Bool(nameof(input.ShowClockOnDisplay), settings.ShowClockOnDisplay);
        settings.EnableAnimations = Bool(nameof(input.EnableAnimations), settings.EnableAnimations);
        settings.EnableFullScreenButton = Bool(nameof(input.EnableFullScreenButton), settings.EnableFullScreenButton);
        settings.EnableVoiceAnnouncements = Bool(nameof(input.EnableVoiceAnnouncements), settings.EnableVoiceAnnouncements);
        settings.ShowServiceNameOnDisplay = Bool(nameof(input.ShowServiceNameOnDisplay), settings.ShowServiceNameOnDisplay);
        settings.ShowWaitingCountOnAgent = Bool(nameof(input.ShowWaitingCountOnAgent), settings.ShowWaitingCountOnAgent);
        settings.EnableBorneAutoReturn = Bool(nameof(input.EnableBorneAutoReturn), settings.EnableBorneAutoReturn);
        settings.EnablePriorityTickets = Bool(nameof(input.EnablePriorityTickets), settings.EnablePriorityTickets);
        settings.ShowCounterNameOnDisplay = Bool(nameof(input.ShowCounterNameOnDisplay), settings.ShowCounterNameOnDisplay);
        settings.ShowCallTimeOnDisplay = Bool(nameof(input.ShowCallTimeOnDisplay), settings.ShowCallTimeOnDisplay);

        settings.BorneAutoReturnSeconds = IntRange(nameof(input.BorneAutoReturnSeconds), settings.BorneAutoReturnSeconds, 3, 60, 8);
        settings.DisplayHistoryRows = IntRange(nameof(input.DisplayHistoryRows), settings.DisplayHistoryRows, 1, 20, 5);
        settings.DisplayLayout = Str(nameof(input.DisplayLayout), settings.DisplayLayout, "standard") is var layout && (layout == "standard" || layout == "compact" || layout == "large" || layout == "tv-wall" || layout == "video-hero" || layout == "minimal") ? layout : "standard";

        // Voix
        var voiceGender = Str(nameof(input.VoiceGender), settings.VoiceGender, "female");
        settings.VoiceGender = voiceGender is "male" or "female" ? voiceGender : "female";
        settings.VoiceRepeatCount = IntRange(nameof(input.VoiceRepeatCount), settings.VoiceRepeatCount, 1, 5, 2);
        settings.VoiceRate = DoubleRange(nameof(input.VoiceRate), settings.VoiceRate, 0.5, 2.0, 0.90);
        settings.VoicePitch = DoubleRange(nameof(input.VoicePitch), settings.VoicePitch, 0.5, 2.0, 1.0);
        settings.VoiceVolume = DoubleRange(nameof(input.VoiceVolume), settings.VoiceVolume, 0.1, 1.0, 1.0);

        // Thème preset uniquement si l'utilisateur l'a explicitement changé et qu'il n'a pas envoyé de couleurs personnalisées.
        if (Has(nameof(input.ThemePreset)) && !Has(nameof(input.DisplayAccentColor)) && !Has(nameof(input.DisplayCardColor)))
            ApplyThemePreset(settings);

        // Médias / uploads
        settings.DisplayBackgroundImageUrl = await ResolveMediaPathAsync(backgroundImageFile, input.DisplayBackgroundImageUrl, settings.DisplayBackgroundImageUrl, "bg");
        settings.DisplayBackgroundVideoUrl = await ResolveMediaPathAsync(backgroundVideoFile, input.DisplayBackgroundVideoUrl, settings.DisplayBackgroundVideoUrl, "video");
        settings.DisplayLogoUrl = await ResolveMediaPathAsync(displayLogoFile, input.DisplayLogoUrl, settings.DisplayLogoUrl, "logo");
        settings.DisplayBannerImageUrl = await ResolveMediaPathAsync(bannerImageFile, input.DisplayBannerImageUrl, settings.DisplayBannerImageUrl, "banner");
        settings.HeaderImageUrl = await ResolveMediaPathAsync(headerImageFile, input.HeaderImageUrl, settings.HeaderImageUrl, "header");

        // ── Suivi mobile par QR code (v2.7.12+) ──
        // Les bools "absents" du formulaire (checkbox non cochée) ne sont PAS
        // posted — Has() retourne false → on les passe à false explicitement
        // grâce à la convention Bool() qui lit ?? false dans ce cas.
        settings.QrFollowEnabled       = Bool(nameof(input.QrFollowEnabled),       settings.QrFollowEnabled);
        settings.QrFollowPublicUrl     = StrN(nameof(input.QrFollowPublicUrl),     settings.QrFollowPublicUrl);
        settings.QrFollowSiteId        = StrN(nameof(input.QrFollowSiteId),        settings.QrFollowSiteId);
        settings.QrFollowApiKey        = StrN(nameof(input.QrFollowApiKey),        settings.QrFollowApiKey);
        settings.QrFollowShowOnDisplay = Bool(nameof(input.QrFollowShowOnDisplay), settings.QrFollowShowOnDisplay);

        // ── Effet wow (#1, #3) ──
        settings.WowDurationMs = IntRange(nameof(input.WowDurationMs), settings.WowDurationMs, 0, 8000, 2200);
        var wowSoundChoicePosted = Str(nameof(input.WowSoundChoice), settings.WowSoundChoice, "chime");
        settings.WowSoundChoice = wowSoundChoicePosted is "chime" or "ding" or "gong" or "none" ? wowSoundChoicePosted : "chime";

        // ── Borne : sélecteur de langue (#14) ──
        settings.BorneShowLanguageSwitcher = Bool(nameof(input.BorneShowLanguageSwitcher), settings.BorneShowLanguageSwitcher);
        // Les langues activées sont envoyées comme checkboxes individuelles.
        if (Has("BorneLangEnabled_fr") || Has("BorneLangEnabled_ar") || Has("BorneLangEnabled_tz") || Has("BorneLangEnabled_en"))
        {
            var langs = new List<string>();
            foreach (var lc in new[] { "fr", "ar", "tz", "en" })
                if (Has($"BorneLangEnabled_{lc}")) langs.Add(lc);
            if (langs.Count == 0) langs.Add("fr"); // au moins une langue
            settings.BorneEnabledLanguages = string.Join(",", langs);
        }

        await _context.SaveChangesAsync();
        _settingsCache.Invalidate();

        // IMPORTANT UX : dès qu'un paramètre d'affichage/langue est changé depuis l'admin,
        // l'écran TV doit se mettre à jour tout seul. Avant, il fallait actualiser
        // manuellement /Display. On pousse donc un événement SignalR dédié, puis un
        // refresh général pour les clients qui n'écoutent que l'ancien événement.
        await _hub.Clients.All.SendAsync("SettingsChanged", settings.DefaultLanguage);
        await _hub.Clients.All.SendAsync("RefreshQueue", settings.DefaultLanguage);

        TempData["Success"] = settings.TicketsClosed ? "Paramètres enregistrés — Bureaux FERMÉS." : "Paramètres enregistrés — Bureaux OUVERTS.";
        if (Has(nameof(input.BorneTitleFr)) || Has(nameof(input.TicketClosureMessageFr)) || Has(nameof(input.BorneShowLanguageSwitcher)) || Has("BorneLangEnabled_fr"))
            return RedirectToIndexTab("tab-borne");
        return RedirectToIndexTab("tab-display");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDisplayHeaderTitle([FromForm] string lang, [FromForm] string? title)
    {
        if (!IsLoggedIn()) return Unauthorized();

        var settings = await _context.UiSettings.FirstAsync();
        var normalizedLang = NormalizeAdminLanguage(lang);
        var value = string.IsNullOrWhiteSpace(title)
            ? settings.SiteName.Trim()
            : title.Trim();

        switch (normalizedLang)
        {
            case "ar":
                settings.DisplayTitleAr = value;
                settings.HeaderTextAr = value;
                break;
            case "tz":
                settings.DisplayTitleTz = value;
                settings.HeaderTextTz = value;
                break;
            case "en":
                settings.DisplayTitleEn = value;
                settings.HeaderTextEn = value;
                break;
            default:
                settings.DisplayTitleFr = value;
                settings.HeaderTextFr = value;
                break;
        }

        await _context.SaveChangesAsync();
        _settingsCache.Invalidate();
        await _hub.Clients.All.SendAsync("SettingsChanged", settings.DefaultLanguage);
        await _hub.Clients.All.SendAsync("RefreshQueue", settings.DefaultLanguage);

        return Json(new { ok = true, lang = normalizedLang, displayTitle = value });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateVoiceGender([FromForm] string? voiceGender)
    {
        if (!IsLoggedIn()) return Unauthorized();

        var normalizedGender = (voiceGender ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedGender is not ("female" or "male"))
        {
            return BadRequest(new { ok = false, message = "Genre de voix invalide." });
        }

        var settings = await _context.UiSettings.FirstAsync();
        settings.VoiceGender = normalizedGender;

        await _context.SaveChangesAsync();
        _settingsCache.Invalidate();
        await _hub.Clients.All.SendAsync("SettingsChanged", settings.DefaultLanguage);
        await _hub.Clients.All.SendAsync("RefreshQueue", settings.DefaultLanguage);

        return Json(new { ok = true, voiceGender = normalizedGender });
    }

    private static string NormalizeAdminLanguage(string? lang)
    {
        var value = (lang ?? "fr").Trim().ToLowerInvariant();
        return value is "fr" or "ar" or "tz" or "en" ? value : "fr";
    }

    // ── Admin credentials ─────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAdminCredentials(string fullName, string login, string? currentPassword, string newPassword, string confirmPassword)
    {
        if (!IsLoggedIn()) return RequireLogin();
        var adminId = HttpContext.Session.GetInt32(SessionKey);
        var admin = await _context.AdminUsers.FirstAsync(a => a.Id == adminId);
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(login))
        { TempData["Error"] = "Nom et identifiant obligatoires."; return RedirectToAction(nameof(Index)); }
        if (string.IsNullOrWhiteSpace(currentPassword) || !PasswordHasher.VerifyPassword(currentPassword, admin.PasswordHash, admin.PasswordSalt))
        { TempData["Error"] = "Mot de passe actuel incorrect."; return RedirectToAction(nameof(Index)); }
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword != confirmPassword) { TempData["Error"] = "Les nouveaux mots de passe ne correspondent pas."; return RedirectToAction(nameof(Index)); }
            var hashed = PasswordHasher.HashPassword(newPassword);
            admin.PasswordHash = hashed.Hash;
            admin.PasswordSalt = hashed.Salt;
        }
        admin.FullName = fullName.Trim();
        admin.Username = login.Trim();
        await _context.SaveChangesAsync();
        HttpContext.Session.SetString("AdminName", admin.FullName);
        HttpContext.Session.SetString("AdminUsername", admin.Username);
        TempData["Success"] = "Identifiants mis à jour.";
        return RedirectToIndexTab("tab-system");
    }

    // ── Gestion des comptes administrateurs (fournisseur uniquement) ──────
    // Permet à l'intégrateur de créer le compte CLIENT sur site, de le
    // (ré)activer, réinitialiser son mot de passe, ou le supprimer.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdminAccount(string fullName, string login, string password, string? role)
    {
        var guard = GuardProvider(); if (guard != null) return guard;
        var cleanLogin = (login ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(cleanLogin) || string.IsNullOrWhiteSpace(password))
        { TempData["Error"] = "Nom, identifiant et mot de passe obligatoires."; return RedirectToIndexTab("tab-system"); }
        if (await _context.AdminUsers.AnyAsync(a => a.Username == cleanLogin))
        { TempData["Error"] = $"L'identifiant « {cleanLogin} » existe déjà."; return RedirectToIndexTab("tab-system"); }

        var normalizedRole = string.Equals(role, RoleProvider, StringComparison.OrdinalIgnoreCase) ? RoleProvider : RoleClient;
        var hash = PasswordHasher.HashPassword(password);
        _context.AdminUsers.Add(new AdminUser
        {
            FullName = fullName.Trim(),
            Username = cleanLogin,
            PasswordHash = hash.Hash,
            PasswordSalt = hash.Salt,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Role = normalizedRole
        });
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Compte « {cleanLogin} » créé ({normalizedRole}).";
        return RedirectToIndexTab("tab-system");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAdminPassword(int id, string newPassword)
    {
        var guard = GuardProvider(); if (guard != null) return guard;
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
        { TempData["Error"] = "Mot de passe trop court (4 caractères minimum)."; return RedirectToIndexTab("tab-system"); }
        var target = await _context.AdminUsers.FirstOrDefaultAsync(a => a.Id == id);
        if (target is null) { TempData["Error"] = "Compte introuvable."; return RedirectToIndexTab("tab-system"); }
        var hash = PasswordHasher.HashPassword(newPassword);
        target.PasswordHash = hash.Hash;
        target.PasswordSalt = hash.Salt;
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Mot de passe réinitialisé pour « {target.Username} ».";
        return RedirectToIndexTab("tab-system");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdminAccount(int id)
    {
        var guard = GuardProvider(); if (guard != null) return guard;
        var meId = HttpContext.Session.GetInt32(SessionKey);
        var target = await _context.AdminUsers.FirstOrDefaultAsync(a => a.Id == id);
        if (target is null) { TempData["Error"] = "Compte introuvable."; return RedirectToIndexTab("tab-system"); }
        if (target.Id == meId) { TempData["Error"] = "Vous ne pouvez pas désactiver votre propre compte."; return RedirectToIndexTab("tab-system"); }
        // Garde-fou : ne jamais désactiver le dernier fournisseur actif.
        if (target.Role == RoleProvider && target.IsActive)
        {
            var otherActiveProviders = await _context.AdminUsers.CountAsync(a => a.Role == RoleProvider && a.IsActive && a.Id != target.Id);
            if (otherActiveProviders == 0)
            { TempData["Error"] = "Impossible de désactiver le dernier compte fournisseur actif."; return RedirectToIndexTab("tab-system"); }
        }
        target.IsActive = !target.IsActive;
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Compte « {target.Username} » {(target.IsActive ? "activé" : "désactivé")}.";
        return RedirectToIndexTab("tab-system");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAdminAccount(int id)
    {
        var guard = GuardProvider(); if (guard != null) return guard;
        var meId = HttpContext.Session.GetInt32(SessionKey);
        var target = await _context.AdminUsers.FirstOrDefaultAsync(a => a.Id == id);
        if (target is null) { TempData["Error"] = "Compte introuvable."; return RedirectToIndexTab("tab-system"); }
        if (target.Id == meId) { TempData["Error"] = "Vous ne pouvez pas supprimer votre propre compte."; return RedirectToIndexTab("tab-system"); }
        if (target.Role == RoleProvider)
        {
            var otherProviders = await _context.AdminUsers.CountAsync(a => a.Role == RoleProvider && a.Id != target.Id);
            if (otherProviders == 0)
            { TempData["Error"] = "Impossible de supprimer le dernier compte fournisseur."; return RedirectToIndexTab("tab-system"); }
        }
        _context.AdminUsers.Remove(target);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Compte « {target.Username} » supprimé.";
        return RedirectToIndexTab("tab-system");
    }

    // ── Network config ────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNetworkConfig(string serverUrl)
    {
        var guard = GuardProvider(); if (guard != null) return guard;
        if (string.IsNullOrWhiteSpace(serverUrl))
        { TempData["Error"] = "URL invalide."; return RedirectToIndexTab("tab-system"); }

        serverUrl = serverUrl.Trim();
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        { TempData["Error"] = "URL invalide. Format attendu : http://adresse:port"; return RedirectToIndexTab("tab-system"); }

        // Persist in UiSettings so it survives restarts.
        var settings = await _context.UiSettings.FirstAsync();
        settings.ServerUrl = serverUrl;
        await _context.SaveChangesAsync();

        // Also update appsettings.json so the next launch uses this URL.
        try
        {
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!System.IO.File.Exists(appSettingsPath))
                appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");

            if (System.IO.File.Exists(appSettingsPath))
            {
                var json = await System.IO.File.ReadAllTextAsync(appSettingsPath);
                // Simple regex replacement to avoid a full JSON parse dependency.
                var updated = System.Text.RegularExpressions.Regex.Replace(
                    json,
                    @"""Urls""\s*:\s*""[^""]*""",
                    $@"""Urls"": ""{serverUrl}""");
                await System.IO.File.WriteAllTextAsync(appSettingsPath, updated);
            }
        }
        catch { /* Non-fatal: setting is saved in DB anyway. */ }

        TempData["Success"] = $"URL réseau mise à jour : {serverUrl}. Redémarrez l'application pour appliquer.";
        return RedirectToIndexTab("tab-system");
    }

    // ── Day reset ─────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetDay()
    {
        if (!IsLoggedIn()) return RequireLogin();
        var today = DateTime.Today;
        var todayTickets = await _context.Tickets.Where(t => t.CreatedAt >= today).ToListAsync();
        var todayTicketIds = todayTickets.Select(t => t.Id).ToList();
        var todayHistories = await _context.CallHistories.Where(h => h.CalledAt >= today || todayTicketIds.Contains(h.TicketId)).ToListAsync();
        _context.CallHistories.RemoveRange(todayHistories);
        _context.Tickets.RemoveRange(todayTickets);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Journée réinitialisée avec succès.";
        return RedirectToIndexTab("tab-system");
    }

    // ── Live stats API ────────────────────────────────────────────
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult DetectIp()
    {
        if (!IsLoggedIn()) return Unauthorized();
        if (!IsProvider()) return Unauthorized();
        // Renvoie l'IP LAN principale (non-loopback) du serveur, formattée en URL prête à l'emploi.
        try
        {
            var addr = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName())
                .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                     && !System.Net.IPAddress.IsLoopback(a));
            if (addr is null) return Json(new { url = "http://0.0.0.0:5000" });
            // On garde le port actuel si on peut le lire dans UiSettings.
            int port = 5000;
            try
            {
                var s = _context.UiSettings.AsNoTracking().FirstOrDefault();
                if (s != null && Uri.TryCreate(s.ServerUrl, UriKind.Absolute, out var uri)) port = uri.Port;
            }
            catch { }
            return Json(new { url = $"http://{addr}:{port}" });
        }
        catch
        {
            return Json(new { url = "http://0.0.0.0:5000" });
        }
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> DashboardStats()
    {
        if (!IsLoggedIn()) return Unauthorized();
        var today = DateTime.Today;
        var tickets = await _context.Tickets.Where(t => t.CreatedAt >= today).ToListAsync();
        var processed = tickets.Where(t => t.CalledAt.HasValue).ToList();
        var avgWait = processed.Count > 0 ? processed.Average(t => (t.CalledAt!.Value - t.CreatedAt).TotalMinutes) : 0;
        return Json(new
        {
            waitingNow = tickets.Count(t => t.Status == TicketStatus.Waiting),
            calledNow = tickets.Count(t => t.Status == TicketStatus.Called),
            servedToday = tickets.Count(t => t.Status == TicketStatus.Finished),
            absentToday = tickets.Count(t => t.Status == TicketStatus.Absent),
            totalToday = tickets.Count,
            avgWaitMinutes = Math.Round(avgWait, 1)
        });
    }

    [HttpGet]
    public async Task<IActionResult> PrintReport()
    {
        if (!IsLoggedIn()) return RequireLogin();
        var today = DateTime.Today;
        var settings = await _context.UiSettings.AsNoTracking().FirstAsync();
        var tickets = await _context.Tickets
            .Include(t => t.ServiceType)
            .Where(t => t.CreatedAt >= today)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
        var services = await _context.Services.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.DisplayOrder).ToListAsync();
        var histories = await _context.CallHistories.Where(h => h.CalledAt >= today).OrderByDescending(h => h.CalledAt).ToListAsync();
        var processed = tickets.Where(t => t.CalledAt.HasValue).ToList();
        var avgWait = processed.Count > 0 ? processed.Average(t => (t.CalledAt!.Value - t.CreatedAt).TotalMinutes) : 0;

        ViewBag.SiteName = settings.SiteName;
        ViewBag.Date = today.ToString("dd/MM/yyyy");
        ViewBag.Tickets = tickets;
        ViewBag.Services = services;
        ViewBag.Histories = histories;
        ViewBag.TotalToday = tickets.Count;
        ViewBag.ServedToday = tickets.Count(t => t.Status == TicketStatus.Finished);
        ViewBag.AbsentToday = tickets.Count(t => t.Status == TicketStatus.Absent);
        ViewBag.WaitingNow = tickets.Count(t => t.Status == TicketStatus.Waiting);
        ViewBag.CalledNow = tickets.Count(t => t.Status == TicketStatus.Called);
        ViewBag.AvgWaitMinutes = Math.Round(avgWait, 1);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ExportStats()
    {
        if (!IsLoggedIn()) return RequireLogin();
        var today = DateTime.Today;
        var tickets = await _context.Tickets.Include(t => t.ServiceType).Where(t => t.CreatedAt >= today).OrderBy(t => t.CreatedAt).ToListAsync();
        var histories = await _context.CallHistories.Where(h => h.CalledAt >= today).OrderByDescending(h => h.CalledAt).ToListAsync();
        var lines = new List<string> { "Numero;Service;Statut;Prioritaire;MotifPriorite;CreeLe;AppeLe;Guichet;AgentNomPrenom" };
        string Clean(string? v) => (v ?? string.Empty).Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
        var counterIds = tickets.Where(t => t.CounterId.HasValue).Select(t => t.CounterId!.Value).Distinct().ToList();
        var countersDict = await _context.Counters.Where(c => counterIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        foreach (var t in tickets)
        {
            var counter = t.CounterId.HasValue && countersDict.TryGetValue(t.CounterId.Value, out var c) ? c : null;
            var history = histories.FirstOrDefault(h => h.TicketId == t.Id || h.TicketNumber == t.Number);
            var agentName = !string.IsNullOrWhiteSpace(history?.AgentName) ? history!.AgentName : string.Empty;
            lines.Add($"{Clean(t.Number)};{Clean(t.ServiceType?.Name)};{t.Status};{(t.IsPriority ? "Oui" : "Non")};{Clean(t.PriorityReason)};{t.CreatedAt:yyyy-MM-dd HH:mm:ss};{t.CalledAt:yyyy-MM-dd HH:mm:ss};{Clean(counter?.Name ?? history?.CounterName)};{Clean(agentName)}");
        }
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(string.Join("\r\n", lines))).ToArray();
        return File(bytes, "text/csv", $"nouba_stats_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }

    // ── Helpers ───────────────────────────────────────────────────
    private bool ParseBoolFromForm(string key)
    {
        if (!Request.HasFormContentType) return false;
        var values = Request.Form[key];
        return values.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "on" || v == "1");
    }

    private void ApplyThemePreset(UiSettings s)
    {
        switch (s.ThemePreset?.ToLowerInvariant())
        {
            case "clinique":
                s.DisplayAccentColor = "#0ea5e9";
                s.DisplayCardColor = "#082f49";
                s.DisplayHistoryColor = "#0c4a6e";
                s.DisplayFooterColor = "#0ea5e9";
                s.DisplayBannerTextFr = "Bienvenue — merci de suivre les indications affichées";
                s.DisplayBannerTextEn = "Welcome — please follow the displayed instructions";
                break;
            case "administration":
                s.DisplayAccentColor = "#1d4ed8";
                s.DisplayCardColor = "#102a43";
                s.DisplayHistoryColor = "#0b1f33";
                s.DisplayFooterColor = "#1d4ed8";
                s.DisplayBannerTextFr = "Bienvenue — préparez vos documents et suivez l'appel affiché";
                s.DisplayBannerTextEn = "Welcome — please prepare your documents and follow the call screen";
                break;
            case "banque":
                s.DisplayAccentColor = "#15803d";
                s.DisplayCardColor = "#052e16";
                s.DisplayHistoryColor = "#0a2512";
                s.DisplayFooterColor = "#15803d";
                s.DisplayBannerTextFr = "Bienvenue — un conseiller va vous recevoir prochainement";
                s.DisplayBannerTextEn = "Welcome — an advisor will receive you shortly";
                break;
            case "mairie":
                s.DisplayAccentColor = "#2563eb";
                s.DisplayCardColor = "#0f172a";
                s.DisplayHistoryColor = "#111827";
                s.DisplayFooterColor = "#1e40af";
                s.DisplayBannerTextFr = "Bienvenue — veuillez attendre l'appel de votre numéro";
                s.DisplayBannerTextAr = "مرحبا — يرجى انتظار مناداة رقمكم";
                s.DisplayBannerTextEn = "Welcome — please wait for your number to be called";
                break;
            case "apc":
                s.DisplayAccentColor = "#047857";
                s.DisplayCardColor = "#064e3b";
                s.DisplayHistoryColor = "#022c22";
                s.DisplayFooterColor = "#047857";
                s.DisplayBannerTextFr = "Bienvenue a l'APC - preparez vos documents et suivez l'appel";
                s.DisplayBannerTextAr = "مرحبا بكم في البلدية - يرجى تحضير الوثائق وانتظار النداء";
                s.DisplayBannerTextEn = "Welcome - please prepare your documents and follow the call screen";
                break;
            case "telecom":
                s.DisplayAccentColor = "#0f766e";
                s.DisplayCardColor = "#0b1120";
                s.DisplayHistoryColor = "#134e4a";
                s.DisplayFooterColor = "#dc2626";
                s.DisplayBannerTextFr = "Bienvenue - nos conseillers vous orientent dans quelques instants";
                s.DisplayBannerTextAr = "مرحبا - سيتم توجيهكم من طرف المستشارين بعد قليل";
                s.DisplayBannerTextEn = "Welcome - our advisors will guide you shortly";
                break;
            case "aeroport":
                s.DisplayAccentColor = "#0891b2";
                s.DisplayCardColor = "#083344";
                s.DisplayHistoryColor = "#0e7490";
                s.DisplayFooterColor = "#06b6d4";
                s.DisplayBannerTextFr = "Bienvenue - surveillez votre numero et votre guichet";
                s.DisplayBannerTextEn = "Welcome - watch your number and counter";
                break;
            case "poste":
                s.DisplayAccentColor = "#eab308";
                s.DisplayCardColor = "#1c1917";
                s.DisplayHistoryColor = "#292524";
                s.DisplayFooterColor = "#ca8a04";
                s.DisplayBannerTextFr = "Bienvenue - merci de garder votre ticket visible";
                s.DisplayBannerTextAr = "مرحبا - يرجى الاحتفاظ بالتذكرة";
                s.DisplayBannerTextEn = "Welcome - please keep your ticket visible";
                break;
            case "tribunal":
                s.DisplayAccentColor = "#7c2d12";
                s.DisplayCardColor = "#1c1917";
                s.DisplayHistoryColor = "#292524";
                s.DisplayFooterColor = "#9a3412";
                s.DisplayBannerTextFr = "Bienvenue - veuillez attendre l'appel de votre numero";
                s.DisplayBannerTextEn = "Welcome - please wait for your number to be called";
                break;
            case "entreprise":
                s.DisplayAccentColor = "#6366f1";
                s.DisplayCardColor = "#111827";
                s.DisplayHistoryColor = "#0f172a";
                s.DisplayFooterColor = "#4f46e5";
                s.DisplayBannerTextFr = "Bienvenue — merci de patienter dans l'espace d'attente";
                s.DisplayBannerTextEn = "Welcome — please wait in the waiting area";
                break;
            default:
                s.ThemePreset = "clinique";
                s.DisplayAccentColor = "#0ea5e9";
                s.DisplayCardColor = "#082f49";
                s.DisplayHistoryColor = "#0c4a6e";
                s.DisplayFooterColor = "#0ea5e9";
                break;
        }
    }

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm"
    };

    private static readonly Dictionary<string, string[]> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new[] { "image/jpeg" },
        [".jpeg"] = new[] { "image/jpeg" },
        [".png"] = new[] { "image/png" },
        [".webp"] = new[] { "image/webp" },
        [".mp4"] = new[] { "video/mp4" },
        [".webm"] = new[] { "video/webm" }
    };

    private async Task<string?> ResolveMediaPathAsync(IFormFile? upload, string? directUrl, string? currentValue, string prefix)
    {
        if (upload is { Length: > 0 })
        {
            var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
            var isImage = AllowedImageExtensions.Contains(extension);
            var isVideo = AllowedVideoExtensions.Contains(extension);

            if (!isImage && !isVideo)
            {
                TempData["Error"] = "Fichier refusé : seuls JPG, PNG, WEBP, MP4 et WEBM sont acceptés.";
                return currentValue;
            }

            var maxBytes = isVideo ? 200L * 1024L * 1024L : 5L * 1024L * 1024L;
            if (upload.Length > maxBytes)
            {
                TempData["Error"] = isVideo
                    ? "Vidéo refusée : taille maximale 200 MB."
                    : "Image refusée : taille maximale 5 MB.";
                return currentValue;
            }

            var contentType = upload.ContentType ?? string.Empty;
            if (!AllowedContentTypes.TryGetValue(extension, out var allowedTypes) || !allowedTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Fichier refusé : le type du fichier ne correspond pas à son extension.";
                return currentValue;
            }

            if (!await HasExpectedFileSignatureAsync(upload, extension))
            {
                TempData["Error"] = "Fichier refusé : signature binaire invalide.";
                return currentValue;
            }

            var uploadsFolder = _storagePaths.UploadsPath;
            Directory.CreateDirectory(uploadsFolder);
            var safePrefix = string.Concat(prefix.Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'));
            if (string.IsNullOrWhiteSpace(safePrefix)) safePrefix = "media";
            var fileName = $"{safePrefix}_{Guid.NewGuid():N}{extension}";
            var absolutePath = Path.Combine(uploadsFolder, fileName);
            await using var stream = System.IO.File.Create(absolutePath);
            await upload.CopyToAsync(stream);
            return $"/uploads/{fileName}";
        }

        if (!string.IsNullOrWhiteSpace(directUrl))
        {
            var trimmed = directUrl.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") return trimmed;
            if (trimmed.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("/images/", StringComparison.OrdinalIgnoreCase)) return trimmed;
            TempData["Error"] = "URL média refusée : utilisez une URL http(s) ou un chemin /uploads/ ou /images/.";
            return currentValue;
        }

        return currentValue;
    }
    private static async Task<bool> HasExpectedFileSignatureAsync(IFormFile upload, string extension)
    {
        var header = new byte[16];
        await using var input = upload.OpenReadStream();
        var read = await input.ReadAsync(header.AsMemory(0, header.Length));
        if (read < 4) return false;

        return extension switch
        {
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
            ".webp" => read >= 12 && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            ".mp4" => read >= 12 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70,
            ".webm" => header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3,
            _ => false
        };
    }

    /// <summary>
    /// Récupère la liste des IPs IPv4 locales (LAN) du serveur, hors loopback.
    /// Utile pour proposer des presets dans la configuration réseau.
    /// </summary>
    private static List<string> GetLocalIpAddresses()
    {
        var list = new List<string>();
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) continue;
                    var ip = addr.Address.ToString();
                    if (ip.StartsWith("169.254.")) continue; // APIPA
                    if (!list.Contains(ip)) list.Add(ip);
                }
            }
        }
        catch { /* ignore */ }
        return list;
    }
}
