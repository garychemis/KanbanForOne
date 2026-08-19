using System.IO;
using KanbanForOne.Services;

namespace KanbanForOne.Modules.DesignConditions.Data;

public sealed class DesignConditionStorageOptions
{
    public DesignConditionStorageOptions(string? moduleRoot = null)
    {
        ModuleRoot = moduleRoot ?? Path.Combine(AppPaths.DataRoot, "design-conditions");
    }

    public string ModuleRoot { get; }

    public string DatabaseRoot => Path.Combine(ModuleRoot, "db");

    public string DatabasePath => Path.Combine(DatabaseRoot, "DesignConditions.db");

    public string AttachmentRoot => Path.Combine(ModuleRoot, "attachments");

    public string StagingRoot => Path.Combine(ModuleRoot, "staging");

    public string TrashRoot => Path.Combine(ModuleRoot, "trash");

    public string BackupRoot => Path.Combine(ModuleRoot, "backups");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DatabaseRoot);
        Directory.CreateDirectory(AttachmentRoot);
        Directory.CreateDirectory(StagingRoot);
        Directory.CreateDirectory(TrashRoot);
        Directory.CreateDirectory(BackupRoot);
    }
}
