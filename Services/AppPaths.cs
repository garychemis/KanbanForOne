using System.IO;

namespace KanbanForOne.Services;

public static class AppPaths
{
    public static string AppRoot { get; } = ResolveAppRoot();

    public static string ProjectRoot => AppRoot;

    public static string DataRoot => Path.Combine(AppRoot, "data");

    public static string DbRoot => Path.Combine(DataRoot, "db");

    public static string AttachmentRoot => Path.Combine(DataRoot, "attachments");

    public static string BackupRoot => Path.Combine(DataRoot, "backups");

    public static string DatabasePath => Path.Combine(DbRoot, "Kanban41.db");

    public static string WorkHourOptionsPath => Path.Combine(DataRoot, "workhour-options.json");

    public static void EnsureStorageLayout()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(DbRoot);

        MoveLegacyFileIfNeeded(Path.Combine(AppRoot, "Kanban41.db"), DatabasePath);
        MoveLegacyFileIfNeeded(Path.Combine(AppRoot, "Kanban41.db-wal"), $"{DatabasePath}-wal");
        MoveLegacyFileIfNeeded(Path.Combine(AppRoot, "Kanban41.db-shm"), $"{DatabasePath}-shm");
        MoveLegacyFileIfNeeded(Path.Combine(AppRoot, "Kanban41.db-journal"), $"{DatabasePath}-journal");
        MoveLegacyDirectoryIfNeeded(Path.Combine(AppRoot, "attachments"), AttachmentRoot);
        MoveLegacyDirectoryIfNeeded(Path.Combine(AppRoot, "backups"), BackupRoot);

        Directory.CreateDirectory(AttachmentRoot);
        Directory.CreateDirectory(BackupRoot);
    }

    private static string ResolveAppRoot()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var executableDirectory = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrWhiteSpace(executableDirectory))
            {
                return executableDirectory;
            }
        }

        return AppContext.BaseDirectory;
    }

    private static void MoveLegacyFileIfNeeded(string legacyPath, string currentPath)
    {
        if (!File.Exists(legacyPath) || File.Exists(currentPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(currentPath)!);
        File.Move(legacyPath, currentPath);
    }

    private static void MoveLegacyDirectoryIfNeeded(string legacyPath, string currentPath)
    {
        if (!Directory.Exists(legacyPath) || Directory.Exists(currentPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(currentPath)!);
        Directory.Move(legacyPath, currentPath);
    }
}
