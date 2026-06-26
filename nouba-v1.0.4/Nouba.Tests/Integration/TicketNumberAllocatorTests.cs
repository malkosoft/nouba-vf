using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Models;
using Nouba.Services;

namespace Nouba.Tests.Integration;

public class TicketNumberAllocatorTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private TicketNumberAllocator _allocator = null!;
    private string _dbPath = null!;
    private const string Day = "2024-01-15";

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"nouba_alloc_{Guid.NewGuid():N}.db");
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _db = new AppDbContext(opts);
        await _db.Database.EnsureCreatedAsync();
        _allocator = new TicketNumberAllocator();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        try { File.Delete(_dbPath); } catch { }
    }

    // IDs > 3 pour éviter les conflits avec les 3 services seed (HasData IDs 1,2,3)
    private ServiceType MakeService(int id = 10, string code = "A") => new()
    {
        Id = id, Name = "Test", Code = code, IsActive = true,
        DisplayOrder = id, ButtonColor = "#000", TextColor = "#fff"
    };

    // ── Format ───────────────────────────────────────────────────────

    [Fact]
    public async Task AllocateAsync_FirstTicket_ReturnsCode001()
    {
        var svc = MakeService(id: 10, code: "A");
        _db.Services.Add(svc);
        await _db.SaveChangesAsync();

        var number = await _allocator.AllocateAsync(_db, svc, Day);

        Assert.Equal("A001", number);
    }

    [Fact]
    public async Task AllocateAsync_FormatIsCodePlusThreeDigits()
    {
        var svc = MakeService(id: 11, code: "Q");
        _db.Services.Add(svc);
        await _db.SaveChangesAsync();

        var number = await _allocator.AllocateAsync(_db, svc, Day);

        Assert.Matches(@"^Q\d{3}$", number);
    }

    [Fact]
    public async Task AllocateAsync_EmptyCode_DefaultsToT()
    {
        var svc = MakeService(id: 12, code: "");
        _db.Services.Add(svc);
        await _db.SaveChangesAsync();

        var number = await _allocator.AllocateAsync(_db, svc, Day);

        Assert.StartsWith("T", number);
    }

    // ── Séquençage ───────────────────────────────────────────────────

    [Fact]
    public async Task AllocateAsync_SequentialCalls_IncrementByOne()
    {
        var svc = MakeService(id: 13, code: "P");
        _db.Services.Add(svc);
        await _db.SaveChangesAsync();

        var n1 = await _allocator.AllocateAsync(_db, svc, Day);
        _db.Tickets.Add(new Ticket { Number = n1, ServiceTypeId = svc.Id, TicketDay = Day, Status = TicketStatus.Waiting, CreatedAt = DateTime.Now });
        await _db.SaveChangesAsync();

        var n2 = await _allocator.AllocateAsync(_db, svc, Day);

        Assert.Equal("P001", n1);
        Assert.Equal("P002", n2);
    }

    [Fact]
    public async Task AllocateAsync_DifferentDays_EachStartsFromOne()
    {
        var svc = MakeService(id: 14, code: "V");
        _db.Services.Add(svc);
        _db.Tickets.Add(new Ticket { Number = "V001", ServiceTypeId = svc.Id, TicketDay = "2024-01-14", Status = TicketStatus.Finished, CreatedAt = DateTime.Now });
        _db.Tickets.Add(new Ticket { Number = "V002", ServiceTypeId = svc.Id, TicketDay = "2024-01-14", Status = TicketStatus.Finished, CreatedAt = DateTime.Now });
        await _db.SaveChangesAsync();

        var number = await _allocator.AllocateAsync(_db, svc, "2024-01-15");

        Assert.Equal("V001", number);
    }

    [Fact]
    public async Task AllocateAsync_DifferentServices_IndependentSequences()
    {
        var svcA = MakeService(id: 15, code: "R");
        var svcB = MakeService(id: 16, code: "S");
        _db.Services.AddRange(svcA, svcB);
        _db.Tickets.Add(new Ticket { Number = "R001", ServiceTypeId = 15, TicketDay = Day, Status = TicketStatus.Waiting, CreatedAt = DateTime.Now });
        _db.Tickets.Add(new Ticket { Number = "R002", ServiceTypeId = 15, TicketDay = Day, Status = TicketStatus.Waiting, CreatedAt = DateTime.Now });
        await _db.SaveChangesAsync();

        var nB = await _allocator.AllocateAsync(_db, svcB, Day);

        Assert.Equal("S001", nB);
    }

    // ── Séquençage multi-bornes (avec commit entre allocations) ──────
    // L'allocateur sérialise les requêtes par sémaphore mais ne fait pas l'insert.
    // Ce test simule 8 bornes qui chacune alloue + insère avant que la suivante alloue.

    [Fact]
    public async Task AllocateAsync_MultipleCalls_WithInserts_AllNumbersAreUnique()
    {
        var svc = MakeService(id: 20, code: "X");
        _db.Services.Add(svc);
        await _db.SaveChangesAsync();

        var results = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            await using var db2 = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite($"Data Source={_dbPath}").Options);

            var number = await _allocator.AllocateAsync(db2, svc, Day);
            db2.Tickets.Add(new Ticket { Number = number, ServiceTypeId = svc.Id, TicketDay = Day, Status = TicketStatus.Waiting, CreatedAt = DateTime.Now });
            await db2.SaveChangesAsync();
            results.Add(number);
        }

        Assert.Equal(8, results.Distinct().Count());
        Assert.Equal("X001", results[0]);
        Assert.Equal("X008", results[7]);
    }
}
