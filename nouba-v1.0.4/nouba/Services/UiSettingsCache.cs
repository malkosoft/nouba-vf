using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Models;

namespace Nouba.Services;

/// <summary>
/// Cache mémoire pour UiSettings. Ces réglages changent rarement
/// (interface admin) mais sont lus à chaque poll Display (toutes les 3s)
/// et à chaque chargement de page Borne / Agent.
/// Invalidation explicite par l'AdminController après sauvegarde.
/// </summary>
public sealed class UiSettingsCache
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private UiSettings? _cached;

    public UiSettingsCache(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<UiSettings> GetAsync(CancellationToken ct = default)
    {
        var snapshot = _cached;
        if (snapshot is not null) return snapshot;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cached is not null) return _cached;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // AsNoTracking : aucun changement n'est fait sur cette entité par les lecteurs.
            _cached = await db.UiSettings.AsNoTracking().FirstAsync(ct);
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>À appeler après modification des UiSettings depuis l'admin.</summary>
    public void Invalidate() => _cached = null;
}
