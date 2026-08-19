using System.IO;
using System.IO.Compression;
using System.Text.Json;
using KanbanForOne.Modules.DesignConditions.Data;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Modules.DesignConditions.Services;

public sealed record DesignConditionBackupResult(string BackupPath, int AttachmentFileCount, long BackupSizeBytes, DateTime CreatedAt);
public sealed record DesignConditionRestoreResult(string SourcePath, string ProtectiveBackupPath, int AttachmentFileCount, DateTime RestoredAt);

public sealed class DesignConditionBackupService
{
    private const int BackupFormatVersion = 1;
    private const int CurrentDatabaseSchemaVersion = 2;
    private const int MaxArchiveEntries = 20_000;
    private const long MaxArchiveEntryBytes = 2L * 1024 * 1024 * 1024;
    private const long MaxArchiveExpandedBytes = 10L * 1024 * 1024 * 1024;
    private const long MaxManifestBytes = 1024L * 1024;
    private readonly DesignConditionDatabaseService _database;
    private readonly DesignConditionStorageOptions _paths;
    private readonly DesignConditionOperationCoordinator _operations;

    public DesignConditionBackupService(
        DesignConditionDatabaseService database,
        DesignConditionStorageOptions paths,
        DesignConditionOperationCoordinator operations)
    {
        _database = database;
        _paths = paths;
        _operations = operations;
    }

    public async Task<DesignConditionBackupResult> CreateBackupAsync(bool protective = false)
    {
        using var operation = await _operations.EnterAsync();
        return await CreateBackupCoreAsync(protective);
    }

    public async Task<DesignConditionRestoreResult> RestoreAsync(string sourcePath)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("设计条件备份文件不存在。", sourcePath);
        using var operation = await _operations.EnterAsync();
        _paths.EnsureDirectories();

        var protective = await CreateBackupCoreAsync(true);
        var restoreRoot = Path.Combine(_paths.ModuleRoot, ".restore", Guid.NewGuid().ToString("N"));
        var extractRoot = Path.Combine(restoreRoot, "extract");
        var oldRoot = Path.Combine(restoreRoot, "old");
        var oldDatabase = Path.Combine(oldRoot, "DesignConditions.db");
        var oldAttachments = Path.Combine(oldRoot, "attachments");
        Directory.CreateDirectory(extractRoot);
        var oldDatabaseMoved = false;
        var oldAttachmentsMoved = false;
        var replacementInstalled = false;

        try
        {
            var extraction = ExtractArchive(sourcePath, extractRoot);
            var restoredDatabase = Path.Combine(extractRoot, "DesignConditions.db");
            ValidateDatabase(restoredDatabase, extraction.Manifest.DatabaseSchemaVersion);

            _database.ClearPool();
            Directory.CreateDirectory(oldRoot);
            DeleteSidecars();
            if (File.Exists(_paths.DatabasePath))
            {
                File.Move(_paths.DatabasePath, oldDatabase);
                oldDatabaseMoved = true;
            }
            if (Directory.Exists(_paths.AttachmentRoot))
            {
                Directory.Move(_paths.AttachmentRoot, oldAttachments);
                oldAttachmentsMoved = true;
            }

            Directory.CreateDirectory(_paths.DatabaseRoot);
            replacementInstalled = true;
            File.Copy(restoredDatabase, _paths.DatabasePath, true);
            var restoredAttachments = Path.Combine(extractRoot, "attachments");
            if (Directory.Exists(restoredAttachments)) CopyDirectory(restoredAttachments, _paths.AttachmentRoot);
            else Directory.CreateDirectory(_paths.AttachmentRoot);
            await _database.InitializeAsync();

            TryDeleteDirectory(restoreRoot);
            return new DesignConditionRestoreResult(Path.GetFullPath(sourcePath), protective.BackupPath, extraction.AttachmentCount, DateTime.Now);
        }
        catch (Exception original)
        {
            var rollbackErrors = RollbackRestore(oldDatabase, oldAttachments, oldDatabaseMoved, oldAttachmentsMoved, replacementInstalled);
            if (rollbackErrors.Count == 0)
            {
                TryDeleteDirectory(restoreRoot);
                throw;
            }
            throw new AggregateException($"恢复失败且自动回滚未全部完成。保护备份：{protective.BackupPath}；恢复现场：{restoreRoot}",
                new[] { original }.Concat(rollbackErrors));
        }
    }

    private async Task<DesignConditionBackupResult> CreateBackupCoreAsync(bool protective)
    {
        _paths.EnsureDirectories();
        await _database.CheckpointAsync();
        _database.ClearPool();
        var prefix = protective ? "DesignConditions_PreRestore" : "DesignConditions_Backup";
        var path = UniqueBackupPath(prefix);
        try
        {
            var count = await Task.Run(() => CreateArchive(path));
            _ = InspectArchive(path);
            return new DesignConditionBackupResult(path, count, new FileInfo(path).Length, DateTime.Now);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    private int CreateArchive(string output)
    {
        using var archive = ZipFile.Open(output, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(_paths.DatabasePath, "DesignConditions.db", CompressionLevel.Optimal);
        var manifest = archive.CreateEntry("manifest.json");
        using (var writer = new StreamWriter(manifest.Open()))
        {
            writer.Write(JsonSerializer.Serialize(new DesignConditionBackupManifest("DesignConditions", BackupFormatVersion, CurrentDatabaseSchemaVersion, DateTime.Now)));
        }
        archive.CreateEntry("attachments/");
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(_paths.AttachmentRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(_paths.AttachmentRoot, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, $"attachments/{relative}", CompressionLevel.Optimal);
            count++;
        }
        return count;
    }

    private static ArchiveInspection ExtractArchive(string source, string destination)
    {
        var inspection = InspectArchive(source);
        EnsureFreeSpace(destination, checked(inspection.ExpandedBytes * 2));
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(source);
        foreach (var entry in archive.Entries)
        {
            var normalized = NormalizeEntry(entry.FullName);
            var target = Path.GetFullPath(Path.Combine(destination, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("备份包包含不安全的文件路径。");
            if (normalized.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, false);
        }
        return inspection;
    }

    private static ArchiveInspection InspectArchive(string source)
    {
        using var archive = ZipFile.OpenRead(source);
        if (archive.Entries.Count > MaxArchiveEntries) throw new InvalidDataException($"备份包条目超过 {MaxArchiveEntries} 个。");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expandedBytes = 0;
        var attachmentCount = 0;
        ZipArchiveEntry? manifestEntry = null;
        var hasDatabase = false;
        foreach (var entry in archive.Entries)
        {
            var normalized = NormalizeEntry(entry.FullName);
            if (!paths.Add(normalized)) throw new InvalidDataException($"备份包包含重复路径：{normalized}");
            if (entry.Length > MaxArchiveEntryBytes) throw new InvalidDataException($"备份条目过大：{normalized}");
            checked { expandedBytes += entry.Length; }
            if (expandedBytes > MaxArchiveExpandedBytes) throw new InvalidDataException("备份包解压后总大小超过 10GB。");
            if (normalized.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                if (entry.Length > MaxManifestBytes) throw new InvalidDataException("备份清单超过 1MB。");
                manifestEntry = entry;
            }
            else if (normalized.Equals("DesignConditions.db", StringComparison.OrdinalIgnoreCase)) hasDatabase = true;
            else if (normalized.Equals("attachments/", StringComparison.OrdinalIgnoreCase)) { }
            else if (normalized.StartsWith("attachments/", StringComparison.OrdinalIgnoreCase)) attachmentCount++;
            else throw new InvalidDataException($"备份包包含未知路径：{normalized}");
        }
        if (!hasDatabase || manifestEntry is null) throw new InvalidDataException("备份包缺少 DesignConditions.db 或 manifest.json。");
        using var reader = new StreamReader(manifestEntry.Open());
        var manifest = ReadManifest(reader.ReadToEnd());
        if (manifest.Module != "DesignConditions" || manifest.FormatVersion != BackupFormatVersion)
            throw new InvalidDataException("备份包类型或格式版本不受支持。");
        if (manifest.DatabaseSchemaVersion is < 1 or > CurrentDatabaseSchemaVersion)
            throw new InvalidDataException($"备份数据库版本 {manifest.DatabaseSchemaVersion} 不受支持。");
        return new ArchiveInspection(manifest, attachmentCount, expandedBytes);
    }

    private static DesignConditionBackupManifest ReadManifest(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            string? Text(params string[] names)
            {
                foreach (var name in names) if (root.TryGetProperty(name, out var value)) return value.GetString();
                return null;
            }
            int? Number(params string[] names)
            {
                foreach (var name in names) if (root.TryGetProperty(name, out var value)) return value.GetInt32();
                return null;
            }
            var module = Text("Module", "module") ?? string.Empty;
            var schema = Number("DatabaseSchemaVersion", "databaseSchemaVersion", "version") ?? 0;
            var format = Number("FormatVersion", "formatVersion") ?? 1;
            return new DesignConditionBackupManifest(module, format, schema, DateTime.MinValue);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("备份清单无效。", ex);
        }
    }

    private static string NormalizeEntry(string value)
    {
        var normalized = value.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (normalized.Length == 0
            || normalized.StartsWith('/')
            || Path.IsPathRooted(normalized)
            || segments.Any(segment => segment is "." or ".."))
            throw new InvalidDataException("备份包包含不安全的文件路径。");
        return normalized;
    }

    private static void ValidateDatabase(string path, int expectedVersion)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
        connection.Open();
        using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA quick_check";
        if (!string.Equals(Convert.ToString(check.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("设计条件备份数据库完整性检查失败。");
        using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version";
        if (Convert.ToInt32(version.ExecuteScalar()) != expectedVersion) throw new InvalidDataException("备份清单与数据库版本不一致。");
        using var schema = connection.CreateCommand();
        schema.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('DesignConditions','DesignConditionAttachments','DesignConditionOptions')";
        if (Convert.ToInt32(schema.ExecuteScalar()) != 3) throw new InvalidDataException("设计条件备份数据库结构不完整。");
        using var issuedDate = connection.CreateCommand();
        issuedDate.CommandText = "SELECT COUNT(*) FROM pragma_table_info('DesignConditions') WHERE name='IssuedDate'";
        if (Convert.ToInt32(issuedDate.ExecuteScalar()) != 1) throw new InvalidDataException("设计条件备份数据库缺少提出日期字段。");
    }

    private List<Exception> RollbackRestore(string oldDatabase, string oldAttachments, bool databaseMoved, bool attachmentsMoved, bool replacementInstalled)
    {
        var errors = new List<Exception>();
        _database.ClearPool();
        if (replacementInstalled)
        {
            Try(() => { if (File.Exists(_paths.DatabasePath)) File.Delete(_paths.DatabasePath); }, errors);
            Try(() => { if (Directory.Exists(_paths.AttachmentRoot)) Directory.Delete(_paths.AttachmentRoot, true); }, errors);
        }
        if (databaseMoved) Try(() => { Directory.CreateDirectory(_paths.DatabaseRoot); File.Move(oldDatabase, _paths.DatabasePath); }, errors);
        if (attachmentsMoved) Try(() => Directory.Move(oldAttachments, _paths.AttachmentRoot), errors);
        return errors;
    }

    private static void Try(Action action, ICollection<Exception> errors) { try { action(); } catch (Exception ex) { errors.Add(ex); } }
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
    private static void EnsureFreeSpace(string path, long required)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (root is not null && new DriveInfo(root).AvailableFreeSpace < required + 128L * 1024 * 1024)
            throw new IOException("磁盘剩余空间不足，无法安全恢复设计条件备份。");
    }

    private string UniqueBackupPath(string prefix)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        for (var index = 0; index < 100; index++)
        {
            var suffix = index == 0 ? "" : $"_{index:00}";
            var candidate = Path.Combine(_paths.BackupRoot, $"{prefix}_{stamp}{suffix}.zip");
            if (!File.Exists(candidate)) return candidate;
        }
        throw new IOException("无法创建唯一的设计条件备份文件名。");
    }

    private void DeleteSidecars()
    {
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            var path = _paths.DatabasePath + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private sealed record DesignConditionBackupManifest(string Module, int FormatVersion, int DatabaseSchemaVersion, DateTime CreatedAt);
    private sealed record ArchiveInspection(DesignConditionBackupManifest Manifest, int AttachmentCount, long ExpandedBytes);
}
