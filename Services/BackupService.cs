using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Services;

public sealed record BackupResult(
    string BackupPath,
    int AttachmentFileCount,
    long BackupSizeBytes,
    DateTime CreatedAt);

public sealed record RestoreBackupResult(
    string SourceBackupPath,
    string ProtectiveBackupPath,
    int AttachmentFileCount,
    DateTime RestoredAt);

public sealed class BackupService
{
    private const int MaxArchiveEntries = 20_000;
    private const long MaxArchiveEntryBytes = 2L * 1024 * 1024 * 1024;
    private const long MaxArchiveExpandedBytes = 10L * 1024 * 1024 * 1024;
    private const long MaxOptionsFileBytes = 1024L * 1024;
    private readonly DatabaseService _database;

    public BackupService(DatabaseService database)
    {
        _database = database;
    }

    public async Task<BackupResult> CreateBackupAsync()
    {
        AppPaths.EnsureStorageLayout();

        // WAL 模式下未 checkpoint 的数据在 -wal 边车文件中，
        // 先合并回主数据库文件，确保 ZIP 备份包含全部数据。
        await _database.CheckpointAsync();

        var backupPath = CreateUniqueBackupPath();
        var attachmentCount = await Task.Run(() => CreateBackupArchive(backupPath));
        var sizeBytes = new FileInfo(backupPath).Length;

        return new BackupResult(backupPath, attachmentCount, sizeBytes, DateTime.Now);
    }

    public async Task<RestoreBackupResult> RestoreBackupAsync(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            throw new ArgumentException("请选择一个备份文件。", nameof(backupPath));
        }

        var sourceBackupPath = Path.GetFullPath(backupPath);

        if (!File.Exists(sourceBackupPath))
        {
            throw new FileNotFoundException("备份文件不存在。", sourceBackupPath);
        }

        AppPaths.EnsureStorageLayout();

        // WAL 模式下未 checkpoint 的数据在 -wal 边车文件中：
        // 1) 恢复前先把当前数据全部落盘，protective backup 才完整；
        // 2) 恢复过程会删除 -wal 边车，不先 checkpoint 会静默丢失已提交数据。
        await _database.CheckpointAsync();

        return await Task.Run(() => RestoreBackup(sourceBackupPath));
    }

    private static string CreateUniqueBackupPath()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(AppPaths.BackupRoot, $"Kanban41_Backup_{timestamp}.zip");

        if (!File.Exists(backupPath))
        {
            return backupPath;
        }

        for (var index = 1; index < 100; index++)
        {
            var candidate = Path.Combine(AppPaths.BackupRoot, $"Kanban41_Backup_{timestamp}_{index:00}.zip");

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("无法创建唯一的备份文件名。");
    }

    private static int CreateBackupArchive(string backupPath)
    {
        SqliteConnection.ClearAllPools();

        using var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create);

        if (!File.Exists(AppPaths.DatabasePath))
        {
            throw new FileNotFoundException("数据库文件不存在，无法创建备份。", AppPaths.DatabasePath);
        }

        archive.CreateEntryFromFile(AppPaths.DatabasePath, "Kanban41.db", CompressionLevel.Optimal);
        if (File.Exists(AppPaths.WorkHourOptionsPath))
        {
            archive.CreateEntryFromFile(AppPaths.WorkHourOptionsPath, "workhour-options.json", CompressionLevel.Optimal);
        }
        archive.CreateEntry("attachments/");

        if (!Directory.Exists(AppPaths.AttachmentRoot))
        {
            return 0;
        }

        var attachmentCount = 0;

        foreach (var filePath in Directory.EnumerateFiles(AppPaths.AttachmentRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(AppPaths.DataRoot, filePath).Replace('\\', '/');
            archive.CreateEntryFromFile(filePath, relativePath, CompressionLevel.Optimal);
            attachmentCount++;
        }

        return attachmentCount;
    }

    private static RestoreBackupResult RestoreBackup(string sourceBackupPath)
    {
        var stagingRoot = Path.Combine(AppPaths.DataRoot, ".restore-staging", Guid.NewGuid().ToString("N"));
        var extractRoot = Path.Combine(stagingRoot, "extract");
        var oldRoot = Path.Combine(stagingRoot, "old");
        var oldDatabasePath = Path.Combine(oldRoot, "db", "Kanban41.db");
        var oldAttachmentRoot = Path.Combine(oldRoot, "attachments");
        var oldWorkHourOptionsPath = Path.Combine(oldRoot, "workhour-options.json");

        Directory.CreateDirectory(extractRoot);
        Directory.CreateDirectory(oldRoot);

        var cleanupStaging = true;
        try
        {
            var attachmentCount = ExtractBackupArchive(sourceBackupPath, extractRoot);
            var restoredDatabasePath = Path.Combine(extractRoot, "Kanban41.db");
            var restoredAttachmentRoot = Path.Combine(extractRoot, "attachments");
            var restoredWorkHourOptionsPath = Path.Combine(extractRoot, "workhour-options.json");

            ValidateDatabaseFile(restoredDatabasePath);
            ValidateWorkHourOptionsFile(restoredWorkHourOptionsPath);

            var protectiveBackupPath = CreateUniquePreRestoreBackupPath();
            CreateBackupArchive(protectiveBackupPath);

            SqliteConnection.ClearAllPools();
            var moveState = new CoreMoveState();
            var replacementInstalled = false;

            try
            {
                MoveCurrentDataAside(oldDatabasePath, oldAttachmentRoot, oldWorkHourOptionsPath, moveState);
                replacementInstalled = true;
                File.Copy(restoredDatabasePath, AppPaths.DatabasePath, overwrite: true);

                if (File.Exists(restoredWorkHourOptionsPath))
                {
                    File.Copy(restoredWorkHourOptionsPath, AppPaths.WorkHourOptionsPath, overwrite: true);
                }

                if (Directory.Exists(restoredAttachmentRoot))
                {
                    CopyDirectory(restoredAttachmentRoot, AppPaths.AttachmentRoot);
                }
                else
                {
                    Directory.CreateDirectory(AppPaths.AttachmentRoot);
                }

                DeleteDirectoryIfExists(oldRoot);
                return new RestoreBackupResult(sourceBackupPath, protectiveBackupPath, attachmentCount, DateTime.Now);
            }
            catch (Exception original)
            {
                var rollbackErrors = RestoreMovedCurrentData(
                    oldDatabasePath,
                    oldAttachmentRoot,
                    oldWorkHourOptionsPath,
                    moveState,
                    replacementInstalled);

                if (rollbackErrors.Count > 0)
                {
                    cleanupStaging = false;
                    throw new AggregateException(
                        $"核心数据恢复失败，且自动回滚未全部完成。恢复现场：{stagingRoot}",
                        new[] { original }.Concat(rollbackErrors));
                }

                throw;
            }
        }
        finally
        {
            if (cleanupStaging)
            {
                DeleteDirectoryIfExists(stagingRoot);
            }
        }
    }

    private static string CreateUniquePreRestoreBackupPath()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(AppPaths.BackupRoot, $"Kanban41_PreRestore_{timestamp}.zip");

        if (!File.Exists(backupPath))
        {
            return backupPath;
        }

        for (var index = 1; index < 100; index++)
        {
            var candidate = Path.Combine(AppPaths.BackupRoot, $"Kanban41_PreRestore_{timestamp}_{index:00}.zip");

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("无法创建恢复前保护备份文件名。");
    }

    internal static int ValidateBackupPackage(string backupPath)
    {
        return InspectBackupArchive(backupPath).AttachmentCount;
    }

    private static int ExtractBackupArchive(string backupPath, string destinationRoot)
    {
        var inspection = InspectBackupArchive(backupPath);
        EnsureFreeSpace(destinationRoot, checked(inspection.ExpandedBytes * 2));
        var destinationFullPath = EnsureTrailingSeparator(Path.GetFullPath(destinationRoot));

        using var archive = ZipFile.OpenRead(backupPath);

        foreach (var entry in archive.Entries)
        {
            var normalizedEntryName = NormalizeArchiveEntry(entry.FullName);

            var targetPath = Path.GetFullPath(Path.Combine(
                destinationRoot,
                normalizedEntryName.Replace('/', Path.DirectorySeparatorChar)));

            if (!targetPath.StartsWith(destinationFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("备份包包含不安全的文件路径。");
            }

            if (normalizedEntryName.EndsWith('/'))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, overwrite: false);
        }

        return inspection.AttachmentCount;
    }

    private static CoreArchiveInspection InspectBackupArchive(string backupPath)
    {
        using var archive = ZipFile.OpenRead(backupPath);
        if (archive.Entries.Count > MaxArchiveEntries)
        {
            throw new InvalidDataException($"核心备份包条目超过 {MaxArchiveEntries} 个。");
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasDatabase = false;
        var attachmentCount = 0;
        long expandedBytes = 0;

        foreach (var entry in archive.Entries)
        {
            var normalized = NormalizeArchiveEntry(entry.FullName);
            if (!paths.Add(normalized))
            {
                throw new InvalidDataException($"核心备份包包含重复路径：{normalized}");
            }

            if (entry.Length > MaxArchiveEntryBytes)
            {
                throw new InvalidDataException($"核心备份条目过大：{normalized}");
            }

            checked { expandedBytes += entry.Length; }
            if (expandedBytes > MaxArchiveExpandedBytes)
            {
                throw new InvalidDataException("核心备份包解压后总大小超过 10GB。");
            }

            if (normalized.Equals("Kanban41.db", StringComparison.OrdinalIgnoreCase))
            {
                hasDatabase = true;
            }
            else if (normalized.Equals("workhour-options.json", StringComparison.OrdinalIgnoreCase)
                     || normalized.Equals("attachments/", StringComparison.OrdinalIgnoreCase))
            {
                // 已知的可选配置或附件根目录。
                if (normalized.Equals("workhour-options.json", StringComparison.OrdinalIgnoreCase)
                    && entry.Length > MaxOptionsFileBytes)
                {
                    throw new InvalidDataException("核心备份中的人工时选项配置超过 1MB。");
                }
            }
            else if (normalized.StartsWith("attachments/", StringComparison.OrdinalIgnoreCase))
            {
                if (!normalized.EndsWith('/')) attachmentCount++;
            }
            else
            {
                throw new InvalidDataException($"核心备份包包含未知路径：{normalized}");
            }
        }

        if (!hasDatabase)
        {
            throw new InvalidDataException("核心备份包中未找到 Kanban41.db。");
        }

        return new CoreArchiveInspection(attachmentCount, expandedBytes);
    }

    private static string NormalizeArchiveEntry(string value)
    {
        var normalized = value.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.StartsWith('/')
            || Path.IsPathRooted(normalized)
            || segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException("核心备份包包含不安全的文件路径。");
        }

        return normalized;
    }

    private static void ValidateDatabaseFile(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        };

        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        using (var integrity = connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA quick_check";
            if (!string.Equals(Convert.ToString(integrity.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("核心备份数据库完整性检查失败。");
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN ('Tasks', 'Notes', 'Attachments', 'ArchiveSections', 'AppSettings', 'WorkHourEntries');
            """;

        var tableCount = Convert.ToInt32(command.ExecuteScalar());

        if (tableCount != 6)
        {
            throw new InvalidDataException("备份数据库结构不完整。");
        }

        using (var version = connection.CreateCommand())
        {
            version.CommandText = "PRAGMA user_version";
            if (Convert.ToInt32(version.ExecuteScalar()) != DatabaseService.CurrentSchemaVersion)
            {
                throw new InvalidDataException("核心备份数据库版本与当前应用不匹配。");
            }
        }

        foreach (var (table, requiredColumns) in DatabaseService.RequiredColumns)
        {
            using var columns = connection.CreateCommand();
            columns.CommandText = $"PRAGMA table_info(\"{table}\")";
            using var reader = columns.ExecuteReader();
            var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read()) actual.Add(reader.GetString(1));
            if (requiredColumns.Any(column => !actual.Contains(column)))
            {
                throw new InvalidDataException($"核心备份数据库的 {table} 表字段不完整。");
            }
        }
    }

    private static void ValidateWorkHourOptionsFile(string path)
    {
        if (!File.Exists(path)) return;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !IsOptionalStringArray(document.RootElement, "Disciplines")
                || !IsOptionalStringArray(document.RootElement, "WorkActivities"))
            {
                throw new InvalidDataException("核心备份中的人工时选项配置无效。");
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("核心备份中的人工时选项配置无效。", ex);
        }
    }

    private static bool IsOptionalStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value)) return true;
        return value.ValueKind == JsonValueKind.Array
               && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String);
    }

    private static void MoveCurrentDataAside(
        string oldDatabasePath,
        string oldAttachmentRoot,
        string oldWorkHourOptionsPath,
        CoreMoveState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(oldDatabasePath)!);
        DeleteDatabaseSidecarFiles();

        if (File.Exists(AppPaths.DatabasePath))
        {
            File.Move(AppPaths.DatabasePath, oldDatabasePath);
            state.DatabaseMoved = true;
        }

        if (Directory.Exists(AppPaths.AttachmentRoot))
        {
            Directory.Move(AppPaths.AttachmentRoot, oldAttachmentRoot);
            state.AttachmentsMoved = true;
        }

        if (File.Exists(AppPaths.WorkHourOptionsPath))
        {
            File.Move(AppPaths.WorkHourOptionsPath, oldWorkHourOptionsPath);
            state.WorkHourOptionsMoved = true;
        }
    }

    private static List<Exception> RestoreMovedCurrentData(
        string oldDatabasePath,
        string oldAttachmentRoot,
        string oldWorkHourOptionsPath,
        CoreMoveState state,
        bool replacementInstalled)
    {
        var errors = new List<Exception>();
        SqliteConnection.ClearAllPools();

        if (replacementInstalled)
        {
            TryRollback(() => DeleteFileIfExists(AppPaths.DatabasePath), errors);
            TryRollback(DeleteDatabaseSidecarFiles, errors);
            TryRollback(() => DeleteDirectoryIfExists(AppPaths.AttachmentRoot), errors);
            TryRollback(() => DeleteFileIfExists(AppPaths.WorkHourOptionsPath), errors);
        }

        if (state.DatabaseMoved)
        {
            TryRollback(() => File.Move(oldDatabasePath, AppPaths.DatabasePath), errors);
        }

        if (state.AttachmentsMoved)
        {
            TryRollback(() => Directory.Move(oldAttachmentRoot, AppPaths.AttachmentRoot), errors);
        }
        else if (replacementInstalled)
        {
            TryRollback(() => Directory.CreateDirectory(AppPaths.AttachmentRoot), errors);
        }

        if (state.WorkHourOptionsMoved)
        {
            TryRollback(() => File.Move(oldWorkHourOptionsPath, AppPaths.WorkHourOptionsPath), errors);
        }

        return errors;
    }

    private static void TryRollback(Action action, ICollection<Exception> errors)
    {
        try { action(); }
        catch (Exception ex) { errors.Add(ex); }
    }

    private static void EnsureFreeSpace(string path, long requiredBytes)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (root is not null && new DriveInfo(root).AvailableFreeSpace < requiredBytes + 128L * 1024 * 1024)
        {
            throw new IOException("磁盘剩余空间不足，无法安全恢复核心数据备份。");
        }
    }

    private static void DeleteDatabaseSidecarFiles()
    {
        DeleteFileIfExists($"{AppPaths.DatabasePath}-wal");
        DeleteFileIfExists($"{AppPaths.DatabasePath}-shm");
        DeleteFileIfExists($"{AppPaths.DatabasePath}-journal");
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, file);
            var targetPath = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file, targetPath, overwrite: true);
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private sealed class CoreMoveState
    {
        public bool DatabaseMoved { get; set; }
        public bool AttachmentsMoved { get; set; }
        public bool WorkHourOptionsMoved { get; set; }
    }

    private sealed record CoreArchiveInspection(int AttachmentCount, long ExpandedBytes);
}
