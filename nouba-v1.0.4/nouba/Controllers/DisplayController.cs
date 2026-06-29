using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Models;
using Nouba.Services;
using System.Security.Cryptography;
using System.Text;

namespace Nouba.Controllers;

public class DisplayController : Controller
{
    private readonly AppDbContext _context;
    private readonly UiSettingsCache _settingsCache;

    public DisplayController(AppDbContext context, UiSettingsCache settingsCache)
    {
        _context = context;
        _settingsCache = settingsCache;
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index(string? lang = null)
    {
        var model = await BuildDisplayModelAsync(lang);
        return View(model);
    }

    /// <summary>
    /// Endpoint polling Display (toutes les 3 s).
    /// Optimisation : projection SQL minimale, settings depuis cache,
    /// et ETag pour court-circuit côté client.
    /// </summary>
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> State(string? lang = null)
    {
        var ct = HttpContext.RequestAborted;
        var settings = await _settingsCache.GetAsync(ct);
        var selectedLang = ResolveLanguage(settings, lang);
        await RefreshServiceNamesAsync(ct); // noms de service traduits (NameAr) à jour pour l'annonce

        // Projection SQL : on ne tire que les colonnes nécessaires + Take(30) avec index sur CalledAt.
        var rawHistory = await _context.CallHistories.AsNoTracking()
            .OrderByDescending(c => c.CalledAt)
            .Take(30)
            .Select(c => new CallSlim
            {
                Id = c.Id,
                TicketId = c.TicketId,
                TicketNumber = c.TicketNumber,
                ServiceName = c.ServiceName,
                CounterName = c.CounterName,
                CalledAt = c.CalledAt
            })
            .ToListAsync(ct);

        var activeWindowStart = DateTime.Now.AddSeconds(-20);
        var activeCalls = rawHistory
            .Where(c => c.CalledAt >= activeWindowStart)
            .GroupBy(c => c.CounterName)
            .Select(g => g.OrderByDescending(x => x.CalledAt).First())
            .OrderByDescending(c => c.CalledAt)
            .ToList();

        var historyRows = settings.DisplayHistoryRows > 0 ? settings.DisplayHistoryRows : 5;

        var displayRows = new List<CallSlim>(activeCalls.Take(3));
        var usedTickets = new HashSet<string>(displayRows.Select(x => x.TicketNumber), StringComparer.OrdinalIgnoreCase);
        foreach (var call in rawHistory)
        {
            if (displayRows.Count >= 3) break;
            if (usedTickets.Contains(call.TicketNumber)) continue;
            displayRows.Add(call);
            usedTickets.Add(call.TicketNumber);
        }

        var history = rawHistory.Where(h => !usedTickets.Contains(h.TicketNumber)).Take(historyRows).ToList();

        var currentCall = activeCalls.FirstOrDefault() ?? displayRows.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.CounterName));
        var announcement = currentCall is null
            ? string.Empty
            : BuildAnnouncement(selectedLang, currentCall.TicketNumber, LocalizeCounterName(currentCall.CounterName, selectedLang), LocalizeServiceName(currentCall.ServiceName, selectedLang));

        var serviceLogos = await _context.Services.AsNoTracking()
            .Where(s => s.LogoUrl != null && s.LogoUrl != "")
            .Select(s => new { s.Name, s.LogoUrl })
            .ToDictionaryAsync(s => LocalizeServiceName(s.Name, selectedLang), s => s.LogoUrl!, ct);

        var payload = new
        {
            currentLanguage = selectedLang,
            displayTitle = ResolveDisplayHeaderTitle(settings, selectedLang),
            waitingText = selectedLang switch { "ar" => "في الانتظار", "tz" => "Yettṛaǧu", "en" => "Waiting", _ => "En attente" },
            counterText = selectedLang switch { "ar" => "الشباك", "tz" => "Guichet", "en" => "Counter", _ => "Guichet" },
            serviceText = selectedLang switch { "ar" => "الخدمة", "tz" => "Ameẓluḍ", "en" => "Service", _ => "Service" },
            recentText = selectedLang switch { "ar" => "النداءات السابقة", "tz" => "Isiwlen yezrin", "en" => "Previous calls", _ => "Appels précédents" },
            bannerText = ResolveBannerText(settings, selectedLang),
            headerText = selectedLang switch { "ar" => settings.HeaderTextAr ?? string.Empty, "tz" => settings.HeaderTextTz ?? string.Empty, "en" => settings.HeaderTextEn ?? string.Empty, _ => settings.HeaderTextFr ?? string.Empty },
            settings = new
            {
                siteName = settings.SiteName,
                displayHistoryRows = historyRows,
                displayLayout = settings.DisplayLayout,
                showClockOnDisplay = settings.ShowClockOnDisplay,
                enableFullScreenButton = settings.EnableFullScreenButton,
                enableVoiceAnnouncements = settings.EnableVoiceAnnouncements,
                voiceGender = settings.VoiceGender,
                voiceRepeatCount = settings.VoiceRepeatCount,
                voiceRate = settings.VoiceRate,
                voicePitch = settings.VoicePitch,
                voiceVolume = settings.VoiceVolume,
                displayBannerImageUrl = settings.DisplayBannerImageUrl,
                displayBackgroundImageUrl = settings.DisplayBackgroundImageUrl,
                displayBackgroundVideoUrl = settings.DisplayBackgroundVideoUrl,
                headerImageUrl = settings.HeaderImageUrl,
                qrFollowEnabled = settings.QrFollowEnabled,
                qrFollowShowOnDisplay = settings.QrFollowShowOnDisplay,
                displayAccentColor = settings.DisplayAccentColor,
                displayCardColor = settings.DisplayCardColor,
                displayHistoryColor = settings.DisplayHistoryColor,
                displayFooterColor = settings.DisplayFooterColor,
                displayTextColor = settings.DisplayTextColor,
                displayTicketBgColor = settings.DisplayTicketBgColor,
                enableAnimations = settings.EnableAnimations,
                themePreset = settings.ThemePreset,
                wowDurationMs = settings.WowDurationMs,
                wowSoundChoice = settings.WowSoundChoice
            },
            activeCalls = displayRows.Select(a => new
            {
                ticketNumber = a.TicketNumber,
                counterName = LocalizeCounterName(a.CounterName, selectedLang),
                serviceName = LocalizeServiceName(a.ServiceName, selectedLang),
                calledAt = a.CalledAt.ToString("HH:mm:ss"),
                calledAtTicks = a.CalledAt.Ticks.ToString(),
                isFresh = a.CalledAt >= DateTime.Now.AddSeconds(-20)
            }),
            history = history.Select(h => new
            {
                ticketNumber = h.TicketNumber,
                counterName = LocalizeCounterName(h.CounterName, selectedLang),
                serviceName = LocalizeServiceName(h.ServiceName, selectedLang),
                calledAt = h.CalledAt.ToString("HH:mm:ss")
            }),
            serviceLogos,
            announcement
        };

        // ETag basé sur l'identifiant + horodatage du dernier appel : permet au client
        // de couper court avec un 304 Not Modified si rien n'a bougé.
        var settingsSignature = BuildSettingsSignature(settings, selectedLang);
        var signature = currentCall is null
            ? $"{selectedLang}|empty|{settings.Id}|{settingsSignature}"
            : $"{selectedLang}|{currentCall.Id}|{currentCall.CalledAt.Ticks}|{rawHistory.Count}|{settingsSignature}";
        var etag = '"' + ToShortHash(signature) + '"';

        if (Request.Headers.IfNoneMatch.ToString() == etag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }
        Response.Headers.ETag = etag;
        return Json(payload);
    }

    private static string ToShortHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash, 0, 8);
    }

    private static string BuildSettingsSignature(UiSettings settings, string lang)
    {
        // Signature légère des champs visibles sur l'écran TV. Elle permet au polling
        // de détecter les changements admin (langue, textes, couleurs, médias, voix)
        // même s'il n'y a aucun nouvel appel de ticket.
        var parts = new[]
        {
            lang,
            settings.DefaultLanguage,
            settings.SiteName,
            settings.DisplayTitleFr, settings.DisplayTitleAr, settings.DisplayTitleTz, settings.DisplayTitleEn,
            settings.DisplayBannerTextFr, settings.DisplayBannerTextAr, settings.DisplayBannerTextTz, settings.DisplayBannerTextEn,
            settings.HeaderTextFr, settings.HeaderTextAr, settings.HeaderTextTz, settings.HeaderTextEn,
            settings.DisplayBannerImageUrl, settings.DisplayBackgroundImageUrl, settings.DisplayBackgroundVideoUrl, settings.HeaderImageUrl,
            settings.QrFollowEnabled.ToString(), settings.QrFollowShowOnDisplay.ToString(), settings.QrFollowPublicUrl,
            settings.DisplayAccentColor, settings.DisplayCardColor, settings.DisplayHistoryColor, settings.DisplayTextColor, settings.DisplayTicketBgColor, settings.DisplayFooterColor,
            settings.EnableVoiceAnnouncements.ToString(), settings.VoiceGender, settings.VoiceRepeatCount.ToString(),
            settings.VoiceRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            settings.VoicePitch.ToString(System.Globalization.CultureInfo.InvariantCulture),
            settings.VoiceVolume.ToString(System.Globalization.CultureInfo.InvariantCulture),
            settings.ShowClockOnDisplay.ToString(), settings.ShowServiceNameOnDisplay.ToString(), settings.ShowCounterNameOnDisplay.ToString(),
            settings.ShowCallTimeOnDisplay.ToString(), settings.DisplayHistoryRows.ToString(), settings.DisplayLayout
        };
        return ToShortHash(string.Join("|", parts));
    }

    private string ResolveLanguage(UiSettings settings, string? lang)
    {
        var selected = string.IsNullOrWhiteSpace(lang) ? settings.DefaultLanguage : lang;
        selected = selected!.Trim().ToLowerInvariant();
        return selected is "fr" or "ar" or "tz" or "en" ? selected : "fr";
    }

    private async Task<DisplayPageViewModel> BuildDisplayModelAsync(string? lang)
    {
        var ct = HttpContext.RequestAborted;
        var settings = await _settingsCache.GetAsync(ct);
        var selectedLang = ResolveLanguage(settings, lang);
        await RefreshServiceNamesAsync(ct); // noms de service traduits (NameAr) à jour dès le 1er rendu

        var rawHistory = await _context.CallHistories.AsNoTracking()
            .OrderByDescending(c => c.CalledAt)
            .Take(30)
            .ToListAsync(ct);

        var activeWindowStart = DateTime.Now.AddSeconds(-20);
        var activeCalls = rawHistory
            .Where(c => c.CalledAt >= activeWindowStart)
            .GroupBy(c => c.CounterName)
            .Select(g => g.OrderByDescending(x => x.CalledAt).First())
            .OrderByDescending(c => c.CalledAt)
            .Select(c => new DisplayActiveCallViewModel
            {
                TicketNumber = c.TicketNumber,
                CounterName = LocalizeCounterName(c.CounterName, selectedLang),
                ServiceName = LocalizeServiceName(c.ServiceName, selectedLang),
                CalledAt = c.CalledAt
            })
            .ToList();

        var displayRows = new List<DisplayActiveCallViewModel>();
        displayRows.AddRange(activeCalls.Take(3));

        var usedTickets = displayRows.Select(x => x.TicketNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var call in rawHistory)
        {
            if (displayRows.Count >= 3) break;
            if (usedTickets.Contains(call.TicketNumber)) continue;
            displayRows.Add(new DisplayActiveCallViewModel
            {
                TicketNumber = call.TicketNumber,
                CounterName = LocalizeCounterName(call.CounterName, selectedLang),
                ServiceName = LocalizeServiceName(call.ServiceName, selectedLang),
                CalledAt = call.CalledAt
            });
            usedTickets.Add(call.TicketNumber);
        }

        var historyRows = settings.DisplayHistoryRows > 0 ? settings.DisplayHistoryRows : 5;
        var history = rawHistory.Where(h => !usedTickets.Contains(h.TicketNumber)).Take(historyRows)
            .Select(h => new CallHistory
            {
                Id = h.Id,
                TicketId = h.TicketId,
                TicketNumber = h.TicketNumber,
                CounterName = LocalizeCounterName(h.CounterName, selectedLang),
                ServiceName = LocalizeServiceName(h.ServiceName, selectedLang),
                CalledAt = h.CalledAt
            })
            .ToList();

        var currentCall = activeCalls.FirstOrDefault() ?? displayRows.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.CounterName));
        var announcement = currentCall is null ? string.Empty : BuildAnnouncement(selectedLang, currentCall.TicketNumber, currentCall.CounterName, currentCall.ServiceName);

        return new DisplayPageViewModel
        {
            Settings = settings,
            CurrentLanguage = selectedLang,
            DisplayTitle = ResolveDisplayHeaderTitle(settings, selectedLang),
            HeaderText = selectedLang switch { "ar" => settings.HeaderTextAr ?? string.Empty, "tz" => settings.HeaderTextTz ?? string.Empty, "en" => settings.HeaderTextEn ?? string.Empty, _ => settings.HeaderTextFr ?? string.Empty },
            WaitingText = selectedLang switch { "ar" => "في الانتظار", "tz" => "Yettṛaǧu", "en" => "Waiting", _ => "En attente" },
            CounterText = selectedLang switch { "ar" => "الشباك", "tz" => "Guichet", "en" => "Counter", _ => "Guichet" },
            ServiceText = selectedLang switch { "ar" => "الخدمة", "tz" => "Ameẓluḍ", "en" => "Service", _ => "Service" },
            RecentText = selectedLang switch { "ar" => "النداءات السابقة", "tz" => "Isiwlen yezrin", "en" => "Previous calls", _ => "Appels précédents" },
            BannerText = ResolveBannerText(settings, selectedLang),
            ServiceLogos = await _context.Services.AsNoTracking().Where(s => s.LogoUrl != null && s.LogoUrl != "").ToDictionaryAsync(s => LocalizeServiceName(s.Name, selectedLang), s => s.LogoUrl!, ct),
            History = history,
            ActiveCalls = displayRows,
            Announcement = announcement
        };
    }

    private sealed class CallSlim
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string CounterName { get; set; } = string.Empty;
        public DateTime CalledAt { get; set; }
    }

    private static string ResolveBannerText(UiSettings settings, string lang)
    {
        var candidate = lang switch { "ar" => settings.DisplayBannerTextAr, "tz" => settings.DisplayBannerTextTz, "en" => settings.DisplayBannerTextEn, _ => settings.DisplayBannerTextFr };
        if (!string.IsNullOrWhiteSpace(candidate)) return candidate!;
        return settings.DisplayBannerTextFr ?? settings.DisplayBannerTextEn ?? settings.DisplayBannerTextAr ?? settings.DisplayBannerTextTz ?? "Bienvenue - Merci de suivre les indications affichées";
    }

    private static string ResolveDisplayHeaderTitle(UiSettings settings, string lang)
    {
        var displayTitle = lang switch { "ar" => settings.DisplayTitleAr, "tz" => settings.DisplayTitleTz, "en" => settings.DisplayTitleEn, _ => settings.DisplayTitleFr };
        var headerText = lang switch { "ar" => settings.HeaderTextAr, "tz" => settings.HeaderTextTz, "en" => settings.HeaderTextEn, _ => settings.HeaderTextFr };
        return IsStockDisplayTitle(displayTitle, lang)
            ? FirstNonEmpty(headerText, settings.SiteName, displayTitle)
            : FirstNonEmpty(displayTitle, headerText, settings.SiteName);
    }

    private static bool IsStockDisplayTitle(string? value, string lang)
    {
        var text = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text)) return true;
        return lang switch
        {
            "en" => text.Equals("Now serving", StringComparison.OrdinalIgnoreCase),
            "ar" => text == "الرقم المُنادى" || text == "Ø§Ù„Ø±Ù‚Ù… Ø§Ù„Ù…ÙÙ†Ø§Ø¯Ù‰",
            "tz" => text.Equals("Uṭṭun yettwasiwlen", StringComparison.OrdinalIgnoreCase) || text.Equals("Uá¹­á¹­un yettwasiwlen", StringComparison.OrdinalIgnoreCase),
            _ => text.Equals("Numéro appelé", StringComparison.OrdinalIgnoreCase) || text.Equals("NumÃ©ro appelÃ©", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return string.Empty;
    }

    // Cache des noms de service traduits SAISIS EN ADMIN (Name FR -> NameAr/En/Tz).
    // Rafraîchi par RefreshServiceNamesAsync (appelé depuis State/Index). Sans ça,
    // LocalizeServiceName retombait sur le français pour tout service hors des
    // quelques mots-clés génériques → l'annonce arabe disait le nom en français.
    private static volatile Dictionary<string, (string? Ar, string? En, string? Tz)> _svcNameMap
        = new(StringComparer.OrdinalIgnoreCase);
    private static DateTime _svcNameMapAtUtc = DateTime.MinValue;

    private async Task RefreshServiceNamesAsync(CancellationToken ct)
    {
        if ((DateTime.UtcNow - _svcNameMapAtUtc) < TimeSpan.FromSeconds(15)) return;
        try
        {
            var rows = await _context.Services.AsNoTracking()
                .Select(s => new { s.Name, s.NameAr, s.NameEn, s.NameTz })
                .ToListAsync(ct);
            var map = new Dictionary<string, (string?, string?, string?)>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows)
            {
                var n = (r.Name ?? string.Empty).Trim();
                if (n.Length > 0) map[n] = (r.NameAr, r.NameEn, r.NameTz);
            }
            _svcNameMap = map;
            _svcNameMapAtUtc = DateTime.UtcNow;
        }
        catch { /* on conserve l'ancienne map en cas d'erreur */ }
    }

    public static string LocalizeServiceName(string? serviceName, string lang)
    {
        var name = (serviceName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        // 1) PRIORITÉ : nom traduit configuré par l'admin (NameAr/NameEn/NameTz).
        if (_svcNameMap.TryGetValue(name, out var tr))
        {
            var configured = lang switch { "ar" => tr.Ar, "en" => tr.En, "tz" => tr.Tz, _ => (string?)null };
            if (!string.IsNullOrWhiteSpace(configured)) return configured!.Trim();
        }

        // 2) REPLI : traductions génériques par mots-clés (services standards).
        var key = name.ToLowerInvariant();
        return lang switch
        {
            "ar" when key.Contains("consult") => "الاستشارة",
            "ar" when key.Contains("accueil") || key.Contains("general") || key.Contains("général") => "الاستقبال العام",
            "ar" when key.Contains("paiement") || key.Contains("payment") => "الدفع",
            "ar" when key.Contains("réclamation") || key.Contains("reclamation") => "الشكاوى",
            "en" when key.Contains("accueil") || key.Contains("général") => "General reception",
            "en" when key.Contains("paiement") => "Payment",
            "tz" when key.Contains("accueil") || key.Contains("général") => "Asefrak amatu",
            "tz" when key.Contains("consult") => "Tamsulta",
            "tz" when key.Contains("paiement") => "Lexlaṣ",
            _ => name
        };
    }

    public static string LocalizeCounterName(string? counterName, string lang)
    {
        var digits = new string((counterName ?? string.Empty).Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits)) return counterName ?? string.Empty;
        return lang switch { "ar" => $"الشباك {digits}", "en" => $"Counter {digits}", _ => $"Guichet {digits}" };
    }

    private static string BuildAnnouncement(string lang, string ticketNumber, string counterName, string? serviceName = null)
    {
        var spokenTicket = SpellTicket(ticketNumber, lang);
        var counterLabel = new string((counterName ?? string.Empty).Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(counterLabel)) counterLabel = counterName ?? string.Empty;
        var localService = LocalizeServiceName(serviceName, lang);
        return lang switch
        {
            "ar" => string.IsNullOrWhiteSpace(localService) ? $"الرقم {spokenTicket}، يرجى التوجه إلى الشباك {counterLabel}" : $"الرقم {spokenTicket}، خدمة {localService}، يرجى التوجه إلى الشباك {counterLabel}",
            "en" => string.IsNullOrWhiteSpace(localService) ? $"Ticket {spokenTicket}, please proceed to counter {counterLabel}" : $"Ticket {spokenTicket}, {localService} service, please proceed to counter {counterLabel}",
            "tz" => string.IsNullOrWhiteSpace(localService) ? $"Uṭṭun {spokenTicket}, ruḥ ɣer guichet {counterLabel}" : $"Uṭṭun {spokenTicket}, {localService}, ruḥ ɣer guichet {counterLabel}",
            _ => string.IsNullOrWhiteSpace(localService) ? $"Numéro {spokenTicket}, veuillez vous présenter au guichet {counterLabel}" : $"Numéro {spokenTicket}, service {localService}, veuillez vous présenter au guichet {counterLabel}"
        };
    }

    private static string SpellTicket(string ticketNumber, string lang)
    {
        if (string.IsNullOrWhiteSpace(ticketNumber)) return string.Empty;
        var letters = new string(ticketNumber.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        var digits = new string(ticketNumber.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits)) return letters;
        var number = int.TryParse(digits, out var n) ? n : -1;
        if (number < 0) return $"{letters} {digits}".Trim();
        var spokenNumber = NumberToWords(number, lang);
        var leadingZeros = digits.TakeWhile(c => c == '0').Count();
        if (leadingZeros > 0 && number > 0)
        {
            var zeroWord = lang == "ar" ? "صفر" : lang == "en" ? "zero" : "zéro";
            spokenNumber = string.Join(" ", Enumerable.Repeat(zeroWord, leadingZeros)) + " " + spokenNumber;
        }
        return $"{letters} {spokenNumber}".Trim();
    }

    private static string NumberToWords(int n, string lang) => lang switch { "ar" => NumberToWordsAr(n), "en" => NumberToWordsEn(n), _ => NumberToWordsFr(n) };
    private static string NumberToWordsFr(int n)
    {
        if (n == 0) return "zéro";
        var parts = new List<string>();
        if (n >= 1000) { parts.Add(NumberToWordsFr(n / 1000) + " mille"); n %= 1000; }
        if (n >= 100) { parts.Add(n / 100 == 1 ? "cent" : NumberToWordsFr(n / 100) + " cent"); n %= 100; }
        if (n > 0) parts.Add(UnderHundredFr(n));
        return string.Join(" ", parts);
    }
    private static string UnderHundredFr(int n)
    {
        string[] u = { "", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf", "dix", "onze", "douze", "treize", "quatorze", "quinze", "seize", "dix-sept", "dix-huit", "dix-neuf" };
        if (n < 20) return u[n];
        if (n < 70) { string[] t = { "", "", "vingt", "trente", "quarante", "cinquante", "soixante" }; var d = n / 10; var r = n % 10; return r == 0 ? t[d] : r == 1 ? t[d] + " et un" : t[d] + "-" + u[r]; }
        if (n < 80) { var r = n - 60; return r == 11 ? "soixante et onze" : "soixante-" + UnderHundredFr(r); }
        var rem = n - 80; return rem == 0 ? "quatre-vingts" : "quatre-vingt-" + UnderHundredFr(rem);
    }
    private static string NumberToWordsAr(int n)
    {
        if (n == 0) return "صفر";
        string[] ones = { "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة", "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر" };
        string[] tens = { "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون" };
        var parts = new List<string>();
        if (n >= 1000) { parts.Add(n / 1000 == 1 ? "ألف" : NumberToWordsAr(n / 1000) + " آلاف"); n %= 1000; }
        if (n >= 100) { parts.Add(n / 100 == 1 ? "مئة" : NumberToWordsAr(n / 100) + " مئة"); n %= 100; }
        if (n >= 20) { var r = n % 10; parts.Add(r == 0 ? tens[n / 10] : ones[r] + " و" + tens[n / 10]); }
        else if (n > 0) parts.Add(ones[n]);
        return string.Join(" و", parts);
    }
    private static string NumberToWordsEn(int n)
    {
        if (n == 0) return "zero";
        string[] ones = { "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
        string[] tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };
        var parts = new List<string>();
        if (n >= 1000) { parts.Add(NumberToWordsEn(n / 1000) + " thousand"); n %= 1000; }
        if (n >= 100) { parts.Add(ones[n / 100] + " hundred"); n %= 100; }
        if (n >= 20) { var r = n % 10; parts.Add(r == 0 ? tens[n / 10] : tens[n / 10] + "-" + ones[r]); }
        else if (n > 0) parts.Add(ones[n]);
        return string.Join(" ", parts);
    }
}
