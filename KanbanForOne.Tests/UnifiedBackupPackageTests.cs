using System.IO;
using System.IO.Compression;
using System.Text;
using KanbanForOne.Services;

namespace KanbanForOne.Tests;

public sealed class UnifiedBackupPackageTests
{
    [Fact]
    public void Complete_package_manifest_and_both_modules_are_accepted()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var path = Path.Combine(testRoot, "complete.zip");
            CreatePackage(path, includeDesignConditions: true);

            UnifiedBackupService.ValidatePackage(path);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void Package_missing_a_module_is_rejected()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var path = Path.Combine(testRoot, "incomplete.zip");
            CreatePackage(path, includeDesignConditions: false);

            var exception = Assert.Throws<InvalidDataException>(() => UnifiedBackupService.ValidatePackage(path));
            Assert.Contains("完整备份包", exception.Message);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static void CreatePackage(string path, bool includeDesignConditions)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(
            archive,
            "manifest.json",
            """
            {"PackageType":"Kanban41FullBackup","FormatVersion":1,"CreatedAt":"2026-08-19T00:00:00","CoreAttachmentCount":2,"DesignConditionAttachmentCount":3}
            """);
        WriteEntry(archive, "modules/core.zip", "core");
        if (includeDesignConditions)
        {
            WriteEntry(archive, "modules/design-conditions.zip", "design");
        }
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes);
    }

    private static string CreateTestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "KanbanForOne.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
