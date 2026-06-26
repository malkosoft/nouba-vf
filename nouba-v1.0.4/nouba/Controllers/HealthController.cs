using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nouba.Data;
using Nouba.Infrastructure;

namespace Nouba.Controllers;

/// <summary>
/// Endpoint public /health — vérifie la DB et l'espace disque.
/// Utilisable par un superviseur (Nagios, UptimeRobot, Windows Service Monitor…).
/// </summary>
[Route("health")]
[ApiController]
public class HealthController : ControllerBase
{
    private static readonly DateTime _startedAt = DateTime.UtcNow;

    private readonly AppDbContext _db;
    private readonly AppStoragePaths _paths;

    public HealthController(AppDbContext db, AppStoragePaths paths)
    {
        _db = db;
        _paths = paths;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dbOk = false;
        string dbError = string.Empty;

        try
        {
            dbOk = await _db.Database.CanConnectAsync(ct);
        }
        catch (Exception ex)
        {
            dbError = ex.Message;
        }

        var (diskFreeMb, diskTotalMb, diskWarn) = GetDiskInfo(_paths.DataRoot);

        var status = (!dbOk || diskWarn) ? (dbOk ? "warn" : "error") : "ok";
        var uptimeSeconds = (long)(DateTime.UtcNow - _startedAt).TotalSeconds;

        return Ok(new
        {
            status,
            version = LicenseInfo.Version,
            uptime_seconds = uptimeSeconds,
            db = new { ok = dbOk, error = dbError.Length > 0 ? dbError : null },
            disk = new { free_mb = diskFreeMb, total_mb = diskTotalMb, warn = diskWarn }
        });
    }

    internal static (long FreeMb, long TotalMb, bool Warn) GetDiskInfo(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path) ?? path);
            var freeMb  = drive.AvailableFreeSpace / 1_048_576;
            var totalMb = drive.TotalSize / 1_048_576;
            var warnLowAbsolute = freeMb < 500;
            var warnLowPercent  = totalMb > 0 && freeMb * 100 / totalMb < 5;
            return (freeMb, totalMb, warnLowAbsolute || warnLowPercent);
        }
        catch
        {
            return (0, 0, false);
        }
    }
}
