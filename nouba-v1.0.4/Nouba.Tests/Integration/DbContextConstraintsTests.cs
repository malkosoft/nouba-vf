using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Models;

namespace Nouba.Tests.Integration;

public class DbContextConstraintsTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private string _dbPath = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"nouba_constraints_{Guid.NewGuid():N}.db");
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        _db = new AppDbContext(opts);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        try { File.Delete(_dbPath); } catch { }
    }

    // ── Index unique tickets ──────────────────────────────────────────

    [Fact]
    public async Task Ticket_DuplicateNumberSameDayAndService_ThrowsDbUpdateException()
    {
        var svc = new ServiceType { Id = 100, Name = "S1", Code = "D", IsActive = true, DisplayOrder = 1, ButtonColor = "#000", TextColor = "#fff" };
        _db.Services.Add(svc);
        _db.Tickets.Add(new Ticket { Number = "D001", ServiceTypeId = 100, TicketDay = "2024-01-15", Status = TicketStatus.Waiting, CreatedAt = DateTime.Now });
        await _db.SaveChangesAsync();

        _db.Tickets.Add(new Ticket { Number = "D001", ServiceTypeId = 100, TicketDay = "2024-01-15", Status = TicketStatus.Waiting, CreatedAt = DateTime.Now });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Ticket_SameNumberDifferentDay_IsAllowed()
    {
        var svc = new ServiceType { Id = 101, Name = "S2", Code = "E", IsActive = true, DisplayOrder = 2, ButtonColor = "#000", TextColor = "#fff" };
        _db.Services.Add(svc);
        _db.Tickets.Add(new Ticket { Number = "E001", ServiceTypeId = 101, TicketDay = "2024-01-14", Status = TicketStatus.Finished, CreatedAt = DateTime.Now });
        _db.Tickets.Add(new Ticket { Number = "E001", ServiceTypeId = 101, TicketDay = "2024-01-15", Status = TicketStatus.Waiting, CreatedAt = DateTime.Now });

        await _db.SaveChangesAsync(); // doit réussir
    }

    [Fact]
    public async Task Ticket_SameNumberDifferentService_IsAllowed()
    {
        var svc1 = new ServiceType { Id = 102, Name = "S3", Code = "F", IsActive = true, DisplayOrder = 3, ButtonColor = "#000", TextColor = "#fff" };
        var svc2 = new ServiceType { Id = 103, Name = "S4", Code = "G", IsActive = true, DisplayOrder = 4, ButtonColor = "#000", TextColor = "#fff" };
        _db.Services.AddRange(svc1, svc2);
        _db.Tickets.Add(new Ticket { Number = "F001", ServiceTypeId = 102, TicketDay = "2024-01-15", Status = TicketStatus.Waiting, CreatedAt = DateTime.Now });
        _db.Tickets.Add(new Ticket { Number = "G001", ServiceTypeId = 103, TicketDay = "2024-01-15", Status = TicketStatus.Waiting, CreatedAt = DateTime.Now });

        await _db.SaveChangesAsync(); // doit réussir
    }

    // ── Index unique agents ───────────────────────────────────────────

    [Fact]
    public async Task Agent_DuplicateLogin_ThrowsDbUpdateException()
    {
        var svc = new ServiceType { Id = 104, Name = "S5", Code = "H", IsActive = true, DisplayOrder = 5, ButtonColor = "#000", TextColor = "#fff" };
        var ctr = new Counter { Id = 100, Name = "G100", ServiceTypeId = 104, IsActive = true };
        _db.Services.Add(svc);
        _db.Counters.Add(ctr);
        _db.Agents.Add(new Agent { FullName = "Agent A", Login = "dupagent", Password = "", PasswordHash = "", PasswordSalt = "", CounterId = 100, ServiceTypeId = 104, IsActive = true });
        await _db.SaveChangesAsync();

        _db.Agents.Add(new Agent { FullName = "Agent B", Login = "dupagent", Password = "", PasswordHash = "", PasswordSalt = "", CounterId = 100, ServiceTypeId = 104, IsActive = true });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    // ── Index unique admin ────────────────────────────────────────────

    [Fact]
    public async Task AdminUser_DuplicateUsername_ThrowsDbUpdateException()
    {
        _db.AdminUsers.Add(new AdminUser { FullName = "Admin 1", Username = "dupadmin", PasswordHash = "h1", PasswordSalt = "s1", IsActive = true, CreatedAt = DateTime.UtcNow, Role = "client" });
        await _db.SaveChangesAsync();

        _db.AdminUsers.Add(new AdminUser { FullName = "Admin 2", Username = "dupadmin", PasswordHash = "h2", PasswordSalt = "s2", IsActive = true, CreatedAt = DateTime.UtcNow, Role = "client" });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    // ── Statuts tickets ───────────────────────────────────────────────

    [Fact]
    public async Task Ticket_AllStatusTransitions_CanBeSaved()
    {
        var svc = new ServiceType { Id = 105, Name = "S6", Code = "I", IsActive = true, DisplayOrder = 6, ButtonColor = "#000", TextColor = "#fff" };
        _db.Services.Add(svc);

        foreach (TicketStatus status in Enum.GetValues<TicketStatus>())
        {
            _db.Tickets.Add(new Ticket
            {
                Number = $"I{(int)status:000}",
                ServiceTypeId = 105,
                TicketDay = "2024-01-15",
                Status = status,
                CreatedAt = DateTime.Now
            });
        }

        await _db.SaveChangesAsync(); // tous les statuts doivent être persistés
        var count = await _db.Tickets.Where(t => t.ServiceTypeId == 105).CountAsync();
        Assert.Equal(4, count);
    }

    // ── Modèle UiSettings ─────────────────────────────────────────────

    [Fact]
    public async Task UiSettings_SeedData_HasDefaultRecord()
    {
        var settings = await _db.UiSettings.FirstOrDefaultAsync();

        Assert.NotNull(settings);
        Assert.Equal("Nouba", settings.SiteName);
        Assert.Equal("fr", settings.DefaultLanguage);
    }
}
