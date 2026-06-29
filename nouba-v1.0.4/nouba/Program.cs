using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Nouba.Data;
using Nouba.Hubs;
using Nouba.Infrastructure;
using Nouba.Services;
using Nouba.Security;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// Nouba PRO : écoute réseau LAN par défaut pour borne/TV/postes agents.
// Correctif v2.7.8 : certaines publications forçaient encore 127.0.0.1 via une ancienne config.
// Ici on choisit une URL finale claire, puis on la réapplique après Build() avec app.Urls.
// Priorité : argument --urls > variable NOUBA_URLS > DOTNET_URLS > ASPNETCORE_URLS > Nouba:Urls > LAN par défaut.
var rawArgs = Environment.GetCommandLineArgs();
string? cliUrls = null;
for (var i = 0; i < rawArgs.Length; i++)
{
    if (string.Equals(rawArgs[i], "--urls", StringComparison.OrdinalIgnoreCase) && i + 1 < rawArgs.Length)
    {
        cliUrls = rawArgs[i + 1];
        break;
    }
    if (rawArgs[i].StartsWith("--urls=", StringComparison.OrdinalIgnoreCase))
    {
        cliUrls = rawArgs[i].Substring("--urls=".Length);
        break;
    }
}

var noubaUrls = cliUrls
    ?? Environment.GetEnvironmentVariable("NOUBA_URLS")
    ?? Environment.GetEnvironmentVariable("DOTNET_URLS")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
    ?? builder.Configuration["Nouba:Urls"]
    ?? "http://0.0.0.0:5000";

// Sécurité produit : sauf demande explicite, ne pas laisser une ancienne configuration
// bloquer l'accès TV/téléphone en 127.0.0.1.
var allowLocalOnly = Environment.GetEnvironmentVariable("NOUBA_ALLOW_LOCAL_ONLY") == "1";
if (!allowLocalOnly && (noubaUrls.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
    || noubaUrls.Contains("localhost", StringComparison.OrdinalIgnoreCase)))
{
    noubaUrls = "http://0.0.0.0:5000";
}

builder.WebHost.UseUrls(noubaUrls);

var storagePaths = new AppStoragePaths();
storagePaths.EnsureCreated();

builder.Services.AddSingleton(storagePaths);
builder.Services.AddControllersWithViews();

// ── Compression réponses : ~70 % de bande sur le JSON Display State ──
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "image/svg+xml"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// ── Cache mémoire ASP.NET (utilisé pour ResponseCache + le cache UiSettings) ──
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 512L * 1024L * 1024L; // 512 MB
    options.MultipartHeadersLengthLimit = 64 * 1024;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBoundaryLengthLimit = 256;
    options.MemoryBufferThreshold = 1024 * 1024;
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 512L * 1024L * 1024L;
    // Garde-corps : abandonner les connexions silencieuses au-delà de 2 min.
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 512L * 1024L * 1024L;
});

// ── DbContext : SQLite + connexion partagée + WAL pour la concurrence lecture/écriture ──
// Cache=Shared autorise plusieurs connexions à voir le même fichier DB sans verrou exclusif.
// Mode=ReadWriteCreate ouvre/crée la DB si besoin.
var connStr = $"Data Source={storagePaths.DatabasePath};Cache=Shared;Foreign Keys=True;";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connStr));

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(45);
});

builder.Services.AddHostedService<DatabaseBackupService>();

// Cache UiSettings (singleton, invalidé par l'admin) + allocateur de numéros (singleton).
builder.Services.AddSingleton<UiSettingsCache>();
builder.Services.AddSingleton<TicketNumberAllocator>();
builder.Services.AddSingleton<LoginRateLimiter>();
builder.Services.AddSingleton<EscPosPrinter>();
builder.Services.AddSingleton<QrCodeService>();

// Monitoring imprimante (alerte défaut/manque papier)
builder.Services.AddSingleton<PrinterMonitor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PrinterMonitor>());

// Tunnel Cloudflare automatique — démarre cloudflared si présent, expose TunnelUrl
builder.Services.AddSingleton<CloudflareTunnelService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CloudflareTunnelService>());

// Service IA offline (résumés, chatbot, traductions). Scoped car dépend du DbContext.
builder.Services.AddScoped<NoubaAiService>();

// TTS « voix réaliste » Piper (offline). Singleton car sans état (cache fichier).
builder.Services.AddSingleton<PiperTtsService>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Name = "Nouba.Session";
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

var app = builder.Build();

// Re-application volontaire après Build() : cela neutralise les vieux appsettings publiés
// qui peuvent imposer http://127.0.0.1:5000. La console doit afficher 0.0.0.0:5000
// ou l'URL passée explicitement par --urls / NOUBA_URLS.
try
{
    app.Urls.Clear();
    foreach (var url in noubaUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        app.Urls.Add(url);
    }
}
catch { /* garde-fou : Kestrel utilisera UseUrls ci-dessus */ }

// WebRootPath peut être null si wwwroot n'est pas détecté selon le répertoire
// de lancement (ex. lancement du .dll hors dossier projet). On retombe alors
// sur ContentRoot/wwwroot et on ne crashe JAMAIS le démarrage pour ça.
var webRootPath = app.Environment.WebRootPath;
if (string.IsNullOrEmpty(webRootPath))
{
    webRootPath = Path.Combine(app.Environment.ContentRootPath ?? AppContext.BaseDirectory, "wwwroot");
}
try { Directory.CreateDirectory(Path.Combine(webRootPath, "images")); } catch { /* non bloquant */ }
storagePaths.EnsureCreated();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    DbMigrator.Apply(db);
    SeedData.Initialize(db);

    // Active le mode WAL : les lectures (Display polling) ne bloquent plus
    // les écritures (création de tickets / appels).
    try
    {
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        db.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;");
        db.Database.ExecuteSqlRaw("PRAGMA temp_store=MEMORY;");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Impossible d'activer WAL sur SQLite — fonctionnement nominal en mode journal classique.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Gestion des erreurs HTTP (404, 503, etc.) avec page d'erreur propre.
app.UseStatusCodePagesWithReExecute("/Home/Error");

app.UseResponseCompression();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

// app.UseHttpsRedirection(); // désactivé pour usage local/LAN offline

// Static files : cache long pour les assets versionnés.
var staticFileOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // 7 jours pour les images / CSS / JS du wwwroot. Les uploads sont gérés ailleurs.
        ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=604800";
    }
};
app.UseStaticFiles(staticFileOptions);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storagePaths.UploadsPath),
    RequestPath = "/uploads",
    ContentTypeProvider = new FileExtensionContentTypeProvider(),
    OnPrepareResponse = ctx =>
    {
        // Les uploads (logos, vidéos display) peuvent être mis en cache 1 jour.
        ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=86400";
    }
});

app.UseRouting();
app.UseSession();
app.UseResponseCaching();

// ── Vérification de licence ──────────────────────────────────────
// Toute requête vers une page applicative vérifie la licence.
// Les assets statiques et la page d'activation sont exempts.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "/";
    var exempt = path.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/css",     StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/js",      StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/images",  StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/lib",     StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/guide",   StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/License", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/Home/SetLanguage", StringComparison.OrdinalIgnoreCase)
              || path.StartsWith("/queueHub", StringComparison.OrdinalIgnoreCase);

    if (!exempt)
    {
        // Bypass dev (interne, pour la maquette). En production, NOUBA_DEV_BYPASS_LICENSE
        // n'est jamais défini → comportement strict habituel.
        var bypass = Environment.GetEnvironmentVariable("NOUBA_DEV_BYPASS_LICENSE") == "1";
        if (!bypass)
        {
            var paths = context.RequestServices.GetRequiredService<AppStoragePaths>();
            var status = LicenseManager.CheckLicense(paths);
            if (!status.IsValid)
            {
                context.Response.Redirect("/License/Activate");
                return;
            }
        }
    }
    await next();
});

app.MapHub<QueueHub>("/queueHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Borne}/{action=Index}/{id?}");

// Pré-chauffe TTS en arrière-plan : charge les modèles Piper (FR/EN/AR) dès
// le démarrage pour que la toute première annonce vocale soit instantanée et
// fiable. Ne bloque pas le démarrage du serveur et n'échoue jamais l'app.
_ = Task.Run(async () =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        var tts = scope.ServiceProvider.GetRequiredService<PiperTtsService>();
        await tts.WarmUpAsync(app.Lifetime.ApplicationStopping);
    }
    catch { /* la pré-chauffe est best-effort */ }
});

app.Run();

// Rend la classe Program accessible aux tests d'intégration (WebApplicationFactory)
public partial class Program { }
