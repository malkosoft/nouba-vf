using Nouba.Helpers;

namespace Nouba.Tests.Unit;

public class AnnouncementTextBuilderTests
{
    // ── Langue française ─────────────────────────────────────────────

    [Fact]
    public void Build_French_ContainsTicketAndGuichet()
    {
        var text = AnnouncementTextBuilder.Build("A001", "Guichet 1", "Consultation", "fr");

        Assert.Contains("Ticket", text);
        Assert.Contains("Guichet un", text);
        Assert.Contains("Consultation", text);
        Assert.Contains("veuillez vous présenter", text);
    }

    [Theory]
    [InlineData("A001", "A zéro zéro un")]
    [InlineData("C012", "C zéro douze")]
    [InlineData("P021", "P zéro vingt et un")]
    [InlineData("T100", "T cent")]
    [InlineData("B007", "B zéro zéro sept")]
    public void Build_French_TicketNumberPronunciation(string ticket, string expectedFragment)
    {
        var text = AnnouncementTextBuilder.Build(ticket, "Guichet 1", null, "fr");

        Assert.Contains(expectedFragment, text);
    }

    [Theory]
    [InlineData("Guichet 1", "Guichet un")]
    [InlineData("Guichet 3", "Guichet trois")]
    [InlineData("Guichet 11", "Guichet onze")]
    public void Build_French_CounterPronunciation(string counter, string expectedFragment)
    {
        var text = AnnouncementTextBuilder.Build("A001", counter, null, "fr");

        Assert.Contains(expectedFragment, text);
    }

    // ── Langue anglaise ──────────────────────────────────────────────

    [Fact]
    public void Build_English_ContainsCounterAndService()
    {
        var text = AnnouncementTextBuilder.Build("B005", "Counter 2", "Payment", "en");

        Assert.Contains("Ticket", text);
        Assert.Contains("counter two", text);
        Assert.Contains("Payment service", text);
        Assert.Contains("please proceed to", text);
    }

    [Theory]
    [InlineData("A001", "A zero zero one")]
    [InlineData("B012", "B zero twelve")]
    [InlineData("C025", "C zero twenty-five")]
    [InlineData("D100", "D one hundred")]
    public void Build_English_TicketNumberPronunciation(string ticket, string expectedFragment)
    {
        var text = AnnouncementTextBuilder.Build(ticket, "Counter 1", null, "en");

        Assert.Contains(expectedFragment, text);
    }

    // ── Langue arabe ─────────────────────────────────────────────────

    [Fact]
    public void Build_Arabic_ContainsArabicPhrases()
    {
        var text = AnnouncementTextBuilder.Build("C003", "Guichet 1", "استشارة", "ar");

        Assert.Contains("نِداءٌ", text);
        Assert.Contains("الشباك", text);
        Assert.Contains("استشارة", text);
    }

    [Fact]
    public void Build_Arabic_StartsWithNidaa()
    {
        var text = AnnouncementTextBuilder.Build("A001", "Guichet 1", null, "ar");

        Assert.StartsWith("نِداءٌ", text);
    }

    // ── Langue tamazight ─────────────────────────────────────────────

    [Fact]
    public void Build_Tamazight_UsesFrenchCounterPronunciation()
    {
        var text = AnnouncementTextBuilder.Build("A001", "Guichet 3", "Service", "tz");

        Assert.Contains("Ticket", text);
        Assert.Contains("Guichet trois", text);
    }

    // ── Cas limites ──────────────────────────────────────────────────

    [Fact]
    public void Build_NullServiceName_OmitsServicePart()
    {
        var text = AnnouncementTextBuilder.Build("A001", "Guichet 1", null, "fr");

        Assert.DoesNotContain("service ", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ticket", text);
    }

    [Fact]
    public void Build_EmptyServiceName_OmitsServicePart()
    {
        var text = AnnouncementTextBuilder.Build("A001", "Guichet 1", "   ", "fr");

        Assert.DoesNotContain("service   ", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_NullLang_DefaultsToFrench()
    {
        var text = AnnouncementTextBuilder.Build("A001", "Guichet 1", "Consultation", null);

        Assert.Contains("veuillez vous présenter", text);
    }

    [Fact]
    public void Build_UnknownLang_DefaultsToFrench()
    {
        var text = AnnouncementTextBuilder.Build("A001", "Guichet 1", null, "xx");

        Assert.Contains("veuillez vous présenter", text);
    }

    // ── StripArabicDiacritics ────────────────────────────────────────

    [Fact]
    public void StripArabicDiacritics_RemovesTashkeel()
    {
        // "سِتّة" = "ستة" once diacritics are stripped
        var input = "سِتّة";
        var stripped = AnnouncementTextBuilder.StripArabicDiacritics(input);

        Assert.DoesNotContain('ِ', stripped); // kasra (ِ)
        Assert.DoesNotContain('ّ', stripped); // shadda (ّ)
        Assert.Contains('س', stripped);
        Assert.Contains('ت', stripped);
    }

    [Fact]
    public void StripArabicDiacritics_NullInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, AnnouncementTextBuilder.StripArabicDiacritics(null));
    }

    [Fact]
    public void StripArabicDiacritics_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, AnnouncementTextBuilder.StripArabicDiacritics(""));
    }

    [Fact]
    public void StripArabicDiacritics_LatinText_Unchanged()
    {
        const string latin = "Hello World 123";
        Assert.Equal(latin, AnnouncementTextBuilder.StripArabicDiacritics(latin));
    }

    [Fact]
    public void StripArabicDiacritics_MixedText_OnlyStripsArabicMarks()
    {
        var input = "Ticket سِتّة A001";
        var stripped = AnnouncementTextBuilder.StripArabicDiacritics(input);

        Assert.Contains("Ticket", stripped);
        Assert.Contains("A001", stripped);
        Assert.DoesNotContain('ِ', stripped);
    }
}
