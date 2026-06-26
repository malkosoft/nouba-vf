using System.Net;

namespace Nouba.Tests.Api;

/// <summary>
/// Tests d'authentification : protection des routes, CSRF, redirections.
/// </summary>
public class AuthTests : IClassFixture<NoubaWebAppFactory>
{
    private readonly HttpClient _client;

    public AuthTests(NoubaWebAppFactory factory)
    {
        _client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false
        });
    }

    // ── Routes publiques ─────────────────────────────────────────────

    [Fact]
    public async Task BorneIndex_AnonymousAccess_Returns200()
    {
        var resp = await _client.GetAsync("/Borne");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task DisplayIndex_AnonymousAccess_Returns200()
    {
        var resp = await _client.GetAsync("/Display");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task AgentLoginPage_AnonymousAccess_Returns200()
    {
        var resp = await _client.GetAsync("/Agent/Login");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task AdminLoginPage_AnonymousAccess_Returns200()
    {
        var resp = await _client.GetAsync("/Admin/Login");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Routes protégées → redirect login ────────────────────────────

    [Fact]
    public async Task AdminIndex_WithoutAuth_RedirectsToLogin()
    {
        var resp = await _client.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Contains("Login", resp.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task AgentIndex_WithoutAuth_RedirectsToLogin()
    {
        var resp = await _client.GetAsync("/Agent");

        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Contains("Login", resp.Headers.Location?.ToString() ?? "");
    }

    // ── Protection CSRF ──────────────────────────────────────────────

    [Fact]
    public async Task AdminLogin_PostWithoutCsrf_Returns400()
    {
        var resp = await _client.PostAsync("/Admin/Login",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("login", "admin"),
                new KeyValuePair<string, string>("password", "admin")
            }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AgentLogin_PostWithoutCsrf_Returns400()
    {
        var resp = await _client.PostAsync("/Agent/Login",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Login", "boualem"),
                new KeyValuePair<string, string>("Password", "boualem")
            }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task BorneCreateTicket_PostWithoutCsrf_Returns400()
    {
        var resp = await _client.PostAsync("/Borne/CreateTicket",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("serviceId", "1"),
                new KeyValuePair<string, string>("lang", "fr")
            }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Routes inconnues ─────────────────────────────────────────────

    [Fact]
    public async Task UnknownRoute_Returns404Or302()
    {
        var resp = await _client.GetAsync("/PageQuiNexistePas");

        Assert.True(
            resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Redirect,
            $"Expected 404 or 302, got {resp.StatusCode}");
    }

    // ── En-têtes de sécurité ─────────────────────────────────────────

    [Fact]
    public async Task BorneIndex_HasSecurityHeaders()
    {
        var resp = await _client.GetAsync("/Borne");

        Assert.True(resp.Headers.TryGetValues("X-Content-Type-Options", out var values));
        Assert.Contains("nosniff", values);
    }

    [Fact]
    public async Task DisplayState_HasXFrameOptions()
    {
        var resp = await _client.GetAsync("/Display/State");

        Assert.True(resp.Headers.TryGetValues("X-Frame-Options", out var values));
        Assert.Contains("SAMEORIGIN", values);
    }
}
