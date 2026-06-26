using System.Runtime.InteropServices;

namespace Nouba.Infrastructure;

public sealed class AppStoragePaths
{
    public string DataRoot { get; }
    public string DatabasePath { get; }
    public string UploadsPath { get; }
    public string BackupsPath { get; }

    public AppStoragePaths()
    {
        var dataRootOverride = Environment.GetEnvironmentVariable("NOUBA_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(dataRootOverride))
        {
            DataRoot = Path.GetFullPath(dataRootOverride);
            DatabasePath = Path.Combine(DataRoot, "nouba.db");
            UploadsPath = Path.Combine(DataRoot, "uploads");
            BackupsPath = Path.Combine(DataRoot, "backups");
            return;
        }

        var root = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(root))
        {
            root = AppContext.BaseDirectory;
        }

        DataRoot = Path.Combine(root, "Nouba");
        DatabasePath = Path.Combine(DataRoot, "nouba.db");
        UploadsPath = Path.Combine(DataRoot, "uploads");
        BackupsPath = Path.Combine(DataRoot, "backups");
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(UploadsPath);
        Directory.CreateDirectory(BackupsPath);
    }
}
