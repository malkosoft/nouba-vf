using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nouba.Data;

namespace Nouba.Tests.Api;

/// <summary>
/// Factory WebApplicationFactory pour les tests d'intégration HTTP.
/// Remplace la DB SQLite de production par une DB temporaire de test et
/// bypasse la vérification de licence via la variable d'environnement.
/// </summary>
public class NoubaWebAppFactory : WebApplicationFactory<Program>
{
    private string? _dbPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"nouba_apitest_{Guid.NewGuid():N}.db");

        // Bypass licence et contrainte d'URL LAN pour les tests
        Environment.SetEnvironmentVariable("NOUBA_DEV_BYPASS_LICENSE", "1");
        Environment.SetEnvironmentVariable("NOUBA_ALLOW_LOCAL_ONLY", "1");

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Supprimer la registration DbContext de production
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // DB SQLite temporaire isolée pour les tests
            services.AddDbContext<AppDbContext>(opts =>
                opts.UseSqlite($"Data Source={_dbPath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _dbPath != null)
        {
            try { File.Delete(_dbPath); } catch { }
            try { File.Delete(_dbPath + "-shm"); } catch { }
            try { File.Delete(_dbPath + "-wal"); } catch { }
        }
    }
}
