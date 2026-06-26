using Nouba.Infrastructure;

namespace Nouba.Services;

public sealed class DatabaseBackupService : BackgroundService
{
    private readonly AppStoragePaths _paths;
    private readonly ILogger<DatabaseBackupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(6);

    public DatabaseBackupService(AppStoragePaths paths, ILogger<DatabaseBackupService> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SafeBackupAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            await SafeBackupAsync();
        }
    }

    private Task SafeBackupAsync()
    {
        try
        {
            if (!File.Exists(_paths.DatabasePath))
            {
                return Task.CompletedTask;
            }

            _paths.EnsureCreated();
            var fileName = $"nouba_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var destination = Path.Combine(_paths.BackupsPath, fileName);
            File.Copy(_paths.DatabasePath, destination, overwrite: true);

            var backups = new DirectoryInfo(_paths.BackupsPath)
                .GetFiles("nouba_*.db")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(20)
                .ToList();

            foreach (var backup in backups)
            {
                backup.Delete();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup Nouba impossible.");
        }

        return Task.CompletedTask;
    }
}
