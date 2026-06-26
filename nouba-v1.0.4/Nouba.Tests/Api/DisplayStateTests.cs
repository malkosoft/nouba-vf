using System.Net;
using System.Text.Json;

namespace Nouba.Tests.Api;

/// <summary>
/// Tests de l'API Display/State : structure JSON, ETag, types des champs.
/// </summary>
public class DisplayStateTests : IClassFixture<NoubaWebAppFactory>
{
    private readonly HttpClient _client;

    public DisplayStateTests(NoubaWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Réponse de base ───────────────────────────────────────────────

    [Fact]
    public async Task DisplayState_Returns200()
    {
        var resp = await _client.GetAsync("/Display/State");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task DisplayState_ContentTypeIsJson()
    {
        var resp = await _client.GetAsync("/Display/State");
        Assert.Equal("application/json", resp.Content.Headers.ContentType?.MediaType);
    }

    // ── Cache ETag ────────────────────────────────────────────────────

    [Fact]
    public async Task DisplayState_HasETagHeader()
    {
        var resp = await _client.GetAsync("/Display/State");
        Assert.NotNull(resp.Headers.ETag);
    }

    [Fact]
    public async Task DisplayState_SameETag_Returns304NotModified()
    {
        var first = await _client.GetAsync("/Display/State");
        var etag = first.Headers.ETag!.ToString();

        var req = new HttpRequestMessage(HttpMethod.Get, "/Display/State");
        req.Headers.Add("If-None-Match", etag);
        var second = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    // ── Structure JSON ────────────────────────────────────────────────

    [Fact]
    public async Task DisplayState_HasActiveCalls()
    {
        var json = await GetStateJson();
        Assert.True(json.TryGetProperty("activeCalls", out var activeCalls));
        Assert.Equal(JsonValueKind.Array, activeCalls.ValueKind);
    }

    [Fact]
    public async Task DisplayState_HasHistory()
    {
        var json = await GetStateJson();
        Assert.True(json.TryGetProperty("history", out var history));
        Assert.Equal(JsonValueKind.Array, history.ValueKind);
    }

    [Fact]
    public async Task DisplayState_HasSettings()
    {
        var json = await GetStateJson();
        Assert.True(json.TryGetProperty("settings", out _));
    }

    [Fact]
    public async Task DisplayState_Settings_HasWowSoundChoice()
    {
        var json = await GetStateJson();
        var settings = json.GetProperty("settings");
        Assert.True(settings.TryGetProperty("wowSoundChoice", out _));
    }

    [Fact]
    public async Task DisplayState_Settings_HasWowDurationMs()
    {
        var json = await GetStateJson();
        var settings = json.GetProperty("settings");
        Assert.True(settings.TryGetProperty("wowDurationMs", out _));
    }

    // ── Fix précision Int64 (bug wow double) ──────────────────────────

    [Fact]
    public async Task DisplayState_ActiveCalls_CalledAtTicksIsString()
    {
        // calledAtTicks doit être sérialisé en STRING (pas number) pour éviter
        // la perte de précision JavaScript sur les Int64 > Number.MAX_SAFE_INTEGER.
        var json = await GetStateJson();
        var activeCalls = json.GetProperty("activeCalls");

        foreach (var call in activeCalls.EnumerateArray())
        {
            if (call.TryGetProperty("calledAtTicks", out var ticks))
            {
                Assert.Equal(JsonValueKind.String, ticks.ValueKind);
                // Vérifier que c'est bien un nombre représenté en string
                Assert.True(long.TryParse(ticks.GetString(), out _),
                    "calledAtTicks doit être parseable en Int64");
            }
        }
    }

    // ── API Borne/Counts ──────────────────────────────────────────────

    [Fact]
    public async Task BorneCounts_Returns200()
    {
        var resp = await _client.GetAsync("/Borne/Counts");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task BorneCounts_ContentTypeIsJson()
    {
        var resp = await _client.GetAsync("/Borne/Counts");
        Assert.Equal("application/json", resp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BorneCounts_StructureHasServiceIds()
    {
        var resp = await _client.GetAsync("/Borne/Counts");
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            Assert.True(int.TryParse(prop.Name, out _), $"Key '{prop.Name}' should be a numeric service ID");
            Assert.True(prop.Value.TryGetProperty("w", out _), "Each entry should have 'w' (waiting count)");
            Assert.True(prop.Value.TryGetProperty("e", out _), "Each entry should have 'e' (estimated minutes)");
        }
    }

    // ── Helper ────────────────────────────────────────────────────────

    private async Task<JsonElement> GetStateJson()
    {
        var resp = await _client.GetAsync("/Display/State");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }
}
