using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Models;

namespace Nouba.Services;

/// <summary>
/// Génère le prochain numéro de ticket de manière sûre.
/// Le verrou local réduit les collisions dans une instance, et l'index UNIQUE
/// en base (ServiceTypeId + TicketDay + Number) garantit définitivement l'absence
/// de doublon, même avec plusieurs bornes ou plusieurs processus.
/// </summary>
public sealed class TicketNumberAllocator
{
    private readonly Dictionary<int, SemaphoreSlim> _locks = new();
    private readonly object _locksGuard = new();

    private SemaphoreSlim GetLock(int serviceId)
    {
        lock (_locksGuard)
        {
            if (!_locks.TryGetValue(serviceId, out var sem))
            {
                sem = new SemaphoreSlim(1, 1);
                _locks[serviceId] = sem;
            }
            return sem;
        }
    }

    public async Task<string> AllocateAsync(AppDbContext db, ServiceType service, CancellationToken ct = default)
        => await AllocateAsync(db, service, DateTime.Today.ToString("yyyy-MM-dd"), ct);

    public async Task<string> AllocateAsync(AppDbContext db, ServiceType service, string ticketDay, CancellationToken ct = default)
    {
        var sem = GetLock(service.Id);
        await sem.WaitAsync(ct);
        try
        {
            var code = string.IsNullOrWhiteSpace(service.Code) ? "T" : service.Code.Trim().ToUpperInvariant();
            var existingNumbers = await db.Tickets
                .AsNoTracking()
                .Where(t => t.ServiceTypeId == service.Id && t.TicketDay == ticketDay && t.Number.StartsWith(code))
                .Select(t => t.Number)
                .ToListAsync(ct);

            var max = 0;
            foreach (var number in existingNumbers)
            {
                if (number.Length <= code.Length) continue;
                if (int.TryParse(number[code.Length..], out var value) && value > max)
                    max = value;
            }

            return $"{code}{(max + 1):000}";
        }
        finally
        {
            sem.Release();
        }
    }
}
