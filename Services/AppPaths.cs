using System.IO;

namespace KanbanForOne.Services;

public static class AppPaths
{
    public static string AppRoot { get; } = ResolveAppRoot();

    public static string ProjectRoot => AppRoot;

    public static string DataRoot => AppRoot;

    public static string AttachmentRoot => Path.Combine(AppRoot, "attachments");

    public static string BackupRoot => Path.Combine(AppRoot, "backups");

    public static string DatabasePath => Path.Combine(AppRoot, "Kanban41.db");

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
}
