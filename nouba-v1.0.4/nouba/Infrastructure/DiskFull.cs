using Microsoft.Data.Sqlite;

namespace Nouba.Infrastructure;

/// <summary>
/// Détection « disque plein » indépendante d'ASP.NET (testable en isolation).
/// SQLite lève <c>SQLITE_FULL</c> (code 13, « database or disk is full ») quand
/// il ne peut plus écrire faute d'espace ; Windows lève <c>IOException</c> avec
/// ERROR_DISK_FULL (112) / ERROR_HANDLE_DISK_FULL (39) pour les autres écritures
/// (sauvegardes, uploads, journaux).
/// </summary>
public static class DiskFull
{
    private const int SQLITE_FULL = 13;
    private const int ERROR_DISK_FULL = 112;
    private const int ERROR_HANDLE_DISK_FULL = 39;

    /// <summary>Vrai si l'exception (ou une de ses causes) traduit un disque plein.</summary>
    public static bool IsDiskFull(Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is SqliteException se && se.SqliteErrorCode == SQLITE_FULL)
                return true;

            if (e is IOException io)
            {
                int code = io.HResult & 0xFFFF;
                if (code == ERROR_DISK_FULL || code == ERROR_HANDLE_DISK_FULL)
                    return true;
            }

            var m = e.Message;
            if (!string.IsNullOrEmpty(m) &&
                (m.Contains("disk is full", StringComparison.OrdinalIgnoreCase)
              || m.Contains("disk full", StringComparison.OrdinalIgnoreCase)
              || m.Contains("no space left", StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    /// <summary>Espace libre (Mo) sur le volume contenant <paramref name="path"/>, ou -1.</summary>
    public static long FreeMb(string? path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path ?? "."));
            if (string.IsNullOrEmpty(root)) return -1;
            return new DriveInfo(root).AvailableFreeSpace / (1024 * 1024);
        }
        catch { return -1; }
    }
}
