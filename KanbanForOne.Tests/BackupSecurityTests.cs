using System.IO;
using System.IO.Compression;
using System.Text;
using KanbanForOne.Services;

namespace KanbanForOne.Tests;

public sealed class BackupSecurityTests
{
    [Fact]
    public void Core_backup_accepts_only_known_paths_and_counts_attachment_files()
    {
        var path = CreateArchive(archive =>
        {
            WriteEntry(archive, "Kanban41.db", "db");
            archive.CreateEntry("attachments/");
            WriteEntry(archive, "attachments/tasks/file.txt", "content");
            WriteEntry(archive, "workhour-options.json", "{\"Disciplines\":[],\"WorkActivities\":[]}");
        });

        try { Assert.Equal(1, BackupService.ValidateBackupPackage(path)); }
        finally { DeleteArchiveRoot(path); }
    }

    [Theory]
    [InlineData("unexpected.txt")]
    [InlineData("../Kanban41.db")]
    [InlineData("/Kanban41.db")]
    public void Core_backup_rejects_unknown_or_unsafe_paths(string entryName)
    {
        var path = CreateArchive(archive =>
        {
            WriteEntry(archive, "Kanban41.db", "db");
            WriteEntry(archive, entryName, "bad");
        });

        try { Assert.Throws<InvalidDataException>(() => BackupService.ValidateBackupPackage(path)); }
        finally { DeleteArchiveRoot(path); }
    }

    [Fact]
    public void Core_backup_rejects_duplicate_paths_case_insensitively()
    {
        var path = CreateArchive(archive =>
        {
            WriteEntry(archive, "Kanban41.db", "db");
            WriteEntry(archive, "KANBAN41.DB", "duplicate");
        });

        try { Assert.Throws<InvalidDataException>(() => BackupService.ValidateBackupPackage(path)); }
        finally { DeleteArchiveRoot(path); }
    }

    [Theory]
    [InlineData("unsafe.exe")]
    [InlineData("shortcut.url")]
    [InlineData("control.cpl")]
    [InlineData("script.ps1")]
    public void Executable_and_script_attachments_are_blocked(string fileName)
    {
        Assert.True(AttachmentOpenSafety.IsBlocked(fileName));
        Assert.Throws<InvalidOperationException>(() => AttachmentOpenSafety.EnsureSafeToOpen(fileName));
    }

    [Theory]
    [InlineData("drawing.dwg")]
    [InlineData("document.pdf")]
    [InlineData("table.xlsx")]
    public void Normal_document_attachments_remain_openable(string fileName)
    {
        Assert.False(AttachmentOpenSafety.IsBlocked(fileName));
        AttachmentOpenSafety.EnsureSafeToOpen(fileName);
    }

    private static string CreateArchive(Action<ZipArchive> populate)
    {
        var root = Path.Combine(Path.GetTempPath(), "KanbanForOne.BackupSecurity.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "backup.zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        populate(archive);
        return path;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes);
    }

    private static void DeleteArchiveRoot(string path)
    {
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }
}
