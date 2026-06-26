using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nouba.Helpers;

/// <summary>
/// Construit le texte d'annonce vocale exactement comme le fait le
/// JavaScript de <c>Views/Display/Index.cshtml</c> (fonctions
/// <c>speakTicket</c>, <c>formatCounterForSpeech</c>, <c>buildAnnouncement</c>).
///
/// Pourquoi ce doublon volontaire entre JS et C# ? Le pré-chauffage Piper
/// déclenché par <c>AgentController.CallNext</c> doit produire EXACTEMENT
/// le même texte que celui que l'écran Display demandera ensuite à
/// <c>/Tts/Speak</c> — sinon la clé de cache (basée sur le texte exact)
/// ne correspond jamais, le pré-chauffage ne sert à rien, et CHAQUE
/// annonce réelle repart d'une synthèse Piper à froid. C'était la cause
/// directe du « ça marche un coup sur deux, sur PC comme sur TV » : selon
/// la charge du serveur au moment précis de l'appel, la synthèse à froid
/// dépassait parfois les délais de sécurité côté navigateur.
///
/// Si vous changez la formulation des annonces dans Display/Index.cshtml,
/// reportez le même changement ici, sinon le pré-chauffage redevient inutile.
/// </summary>
public static class AnnouncementTextBuilder
{
    public static string Build(string? ticketNumber, string? counterName, string? serviceName, string? lang)
    {
        var safeLang = (lang ?? "fr").Trim().ToLowerInvariant();
        var counterText = FormatCounterForSpeech(counterName, safeLang);
        var ticketText = SpeakTicket(ticketNumber ?? string.Empty, safeLang);
        var cleanService = (serviceName ?? string.Empty).Trim();

        if (safeLang == "ar")
            return $"نِداءٌ، التذكرةُ رقم {ticketText}" + (cleanService.Length > 0 ? $"، خدمةُ {cleanService}" : "") + $"، الرجاءُ التوجهُ إلى {counterText}";
        if (safeLang == "en")
            return $"Ticket {ticketText}" + (cleanService.Length > 0 ? $", {cleanService} service" : "") + $", please proceed to {counterText}";
        if (safeLang == "tz")
            return $"Ticket {SpeakTicket(ticketNumber ?? string.Empty, "fr")}" + (cleanService.Length > 0 ? $", {cleanService}" : "") + $", {counterText}";
        return $"Ticket {ticketText}" + (cleanService.Length > 0 ? $", service {cleanService}" : "") + $", veuillez vous présenter au {counterText}";
    }

    // Signes de vocalisation arabe (tashkeel/harakat) + tatweel. On les retire
    // pour comparer à l'oreille « tashkeel manuel » vs « tout-automatique ».
    private static readonly Regex ArabicDiacritics = new(
        "[\\u0610-\\u061A\\u064B-\\u065F\\u0670\\u06D6-\\u06DC\\u06DF-\\u06E8\\u06EA-\\u06ED\\u0640]",
        RegexOptions.Compiled);

    /// <summary>
    /// Retire les signes de vocalisation (tashkeel) et le tatweel d'un texte
    /// arabe, en laissant les lettres intactes. Sans effet sur le texte
    /// non-arabe. Sert à tester si la diacritisation automatique de Piper
    /// (libtashkeel) sonne mieux que les voyelles posées à la main : un texte
    /// à moitié vocalisé perturbe souvent le diacritiseur.
    /// </summary>
    public static string StripArabicDiacritics(string? text)
        => string.IsNullOrEmpty(text) ? (text ?? string.Empty) : ArabicDiacritics.Replace(text, string.Empty);

    private static string SpeakTicket(string ticketNumber, string lang)
    {
        var value = ticketNumber.Trim().ToUpperInvariant();
        var m = Regex.Match(value, "^([A-Z]+)(\\d+)$");
        if (!m.Success) return value;

        var prefix = m.Groups[1].Value;
        var digits = m.Groups[2].Value;
        var n = int.Parse(digits, CultureInfo.InvariantCulture);
        var leading = digits.Length - digits.TrimStart('0').Length;

        var number = lang == "ar" ? NumberToArabic(n) : lang == "en" ? NumberToEnglish(n) : NumberToFrench(n);
        if (leading > 0 && n > 0)
        {
            var zero = lang == "ar" ? "صِفر" : lang == "en" ? "zero" : "zéro";
            number = string.Join(" ", Enumerable.Repeat(zero, leading)) + " " + number;
        }
        var spokenPrefix = lang == "ar" ? LatinLettersToArabic(prefix) : prefix;
        return (spokenPrefix + " " + number).Trim();
    }

    private static string ExtractCounterNumber(string? name)
    {
        var safe = name ?? string.Empty;
        var m = Regex.Match(safe, "(\\d+)");
        return m.Success ? m.Groups[1].Value : safe;
    }

    private static string FormatCounterForSpeech(string? name, string lang)
    {
        var raw = ExtractCounterNumber(name);
        if (raw.Length == 0) return string.Empty;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        {
            // Identique au comportement JS : un guichet sans chiffre dans son
            // nom (cas rare) produit "Guichet " sans rien après. Pas idéal,
            // mais on reste fidèle à ce que l'écran Display dira réellement,
            // sinon la clé de cache ne correspondrait plus du tout.
            return lang == "ar" ? "الشباك " : lang == "en" ? "counter " : "Guichet ";
        }
        if (lang == "ar") return "الشباك " + NumberToArabic(n);
        if (lang == "en") return "counter " + NumberToEnglish(n);
        return "Guichet " + NumberToFrench(n);
    }

    private static string NumberToFrench(int n)
    {
        string[] units = { "zéro", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf" };
        string[] teens = { "dix", "onze", "douze", "treize", "quatorze", "quinze", "seize", "dix-sept", "dix-huit", "dix-neuf" };
        string[] tens = { "", "", "vingt", "trente", "quarante", "cinquante", "soixante" };
        if (n < 0) return n.ToString(CultureInfo.InvariantCulture);
        if (n < 10) return units[n];
        if (n < 20) return teens[n - 10];
        if (n < 70)
        {
            int t = n / 10, u = n % 10;
            return u == 0 ? tens[t] : u == 1 ? tens[t] + " et un" : tens[t] + "-" + units[u];
        }
        if (n < 80) return n == 71 ? "soixante et onze" : "soixante-" + NumberToFrench(n - 60);
        if (n < 100) return n == 80 ? "quatre-vingts" : "quatre-vingt-" + NumberToFrench(n - 80);
        if (n < 1000)
        {
            int h = n / 100, r = n % 100;
            return (h == 1 ? "cent" : NumberToFrench(h) + " cent") + (r != 0 ? " " + NumberToFrench(r) : "");
        }
        return n.ToString(CultureInfo.InvariantCulture);
    }

    private static string NumberToEnglish(int n)
    {
        string[] units = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
        string[] teens = { "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
        string[] tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };
        if (n < 0) return n.ToString(CultureInfo.InvariantCulture);
        if (n < 10) return units[n];
        if (n < 20) return teens[n - 10];
        if (n < 100)
        {
            int t = n / 10, u = n % 10;
            return u == 0 ? tens[t] : tens[t] + "-" + units[u];
        }
        if (n < 1000)
        {
            int h = n / 100, r = n % 100;
            return units[h] + " hundred" + (r != 0 ? " " + NumberToEnglish(r) : "");
        }
        return n.ToString(CultureInfo.InvariantCulture);
    }

    private static readonly Dictionary<int, string> ArOnes = new()
    {
        { 0, "صِفر" }, { 1, "واحِد" }, { 2, "اِثنان" }, { 3, "ثلاثة" }, { 4, "أربعة" }, { 5, "خمسة" }, { 6, "سِتّة" }, { 7, "سبعة" }, { 8, "ثمانية" }, { 9, "تسعة" },
        { 10, "عَشَرة" }, { 11, "أحَدَ عَشَر" }, { 12, "اِثنا عَشَر" }, { 13, "ثلاثةَ عَشَر" }, { 14, "أربعةَ عَشَر" }, { 15, "خمسةَ عَشَر" }, { 16, "سِتّةَ عَشَر" }, { 17, "سبعةَ عَشَر" }, { 18, "ثمانيةَ عَشَر" }, { 19, "تسعةَ عَشَر" },
        { 20, "عِشرون" }, { 30, "ثلاثون" }, { 40, "أربعون" }, { 50, "خمسون" }, { 60, "سِتّون" }, { 70, "سبعون" }, { 80, "ثمانون" }, { 90, "تسعون" }
    };

    private static readonly Dictionary<int, string> ArHundreds = new()
    {
        { 1, "مِئة" }, { 2, "مِئتان" }, { 3, "ثلاثُمِئة" }, { 4, "أربعُمِئة" }, { 5, "خمسُمِئة" }, { 6, "سِتُّمِئة" }, { 7, "سبعُمِئة" }, { 8, "ثمانِمِئة" }, { 9, "تسعُمِئة" }
    };

    private static string NumberToArabic(int n)
    {
        if (n < 0) return n.ToString(CultureInfo.InvariantCulture);
        if (ArOnes.TryGetValue(n, out var exact)) return exact;
        if (n < 100)
        {
            int u = n % 10, t = (n / 10) * 10;
            return u == 0 ? ArOnes[t] : ArOnes[u] + " و" + ArOnes[t];
        }
        if (n < 1000)
        {
            int h = n / 100, r = n % 100;
            var head = ArHundreds.TryGetValue(h, out var hv) ? hv : string.Empty;
            return r == 0 ? head : head + " و" + NumberToArabic(r);
        }
        // Au-delà de 999 (rare pour un ticket) : chiffre par chiffre, comme en JS.
        return string.Join(" ", n.ToString(CultureInfo.InvariantCulture)
            .Select(ch => ArOnes.TryGetValue(ch - '0', out var dv) ? dv : string.Empty)).Trim();
    }

    private static readonly Dictionary<char, string> ArLetters = new()
    {
        { 'A', "أ" }, { 'B', "بي" }, { 'C', "سي" }, { 'D', "دي" }, { 'E', "إي" }, { 'F', "إف" }, { 'G', "جي" }, { 'H', "إتش" }, { 'I', "آي" }, { 'J', "جي" },
        { 'K', "كي" }, { 'L', "إل" }, { 'M', "إم" }, { 'N', "إن" }, { 'O', "أو" }, { 'P', "بي" }, { 'Q', "كيو" }, { 'R', "آر" }, { 'S', "إس" }, { 'T', "تي" },
        { 'U', "يو" }, { 'V', "في" }, { 'W', "دبليو" }, { 'X', "إكس" }, { 'Y', "واي" }, { 'Z', "زد" }
    };

    private static string LatinLettersToArabic(string letters)
        => string.Join(" ", letters.Select(ch => ArLetters.TryGetValue(ch, out var v) ? v : ch.ToString()));
}
