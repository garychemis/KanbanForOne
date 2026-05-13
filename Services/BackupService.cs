using System.IO;
using System.IO.Compression;
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
    public async Task<BackupResult> CreateBackupAsync()
    {
        AppPaths.EnsureStorageLayout();

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

        Directory.CreateDirectory(extractRoot);
        Directory.CreateDirectory(oldRoot);

        try
        {
            var attachmentCount = ExtractBackupArchive(sourceBackupPath, extractRoot);
            var restoredDatabasePath = Path.Combine(extractRoot, "Kanban41.db");
            var restoredAttachmentRoot = Path.Combine(extractRoot, "attachments");

            ValidateDatabaseFile(restoredDatabasePath);

            var protectiveBackupPath = CreateUniquePreRestoreBackupPath();
            CreateBackupArchive(protectiveBackupPath);

            SqliteConnection.ClearAllPools();
            var movedCurrentData = false;

            try
            {
                MoveCurrentDataAside(oldDatabasePath, oldAttachmentRoot);
                movedCurrentData = true;
                File.Copy(restoredDatabasePath, AppPaths.DatabasePath, overwrite: true);

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
            catch
            {
                if (movedCurrentData)
                {
                    RestoreMovedCurrentData(oldDatabasePath, oldAttachmentRoot);
                }

                throw;
            }
        }
        finally
        {
            DeleteDirectoryIfExists(stagingRoot);
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

    private static int ExtractBackupArchive(string backupPath, string destinationRoot)
    {
        var destinationFullPath = EnsureTrailingSeparator(Path.GetFullPath(destinationRoot));
        var attachmentCount = 0;
        var hasDatabase = false;

        using var archive = ZipFile.OpenRead(backupPath);

        foreach (var entry in archive.Entries)
        {
            var normalizedEntryName = entry.FullName.Replace('\\', '/');

            if (string.IsNullOrWhiteSpace(normalizedEntryName))
            {
                continue;
            }

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
            entry.ExtractToFile(targetPath, overwrite: true);

            if (string.Equals(normalizedEntryName, "Kanban41.db", StringComparison.OrdinalIgnoreCase))
            {
                hasDatabase = true;
            }
            else if (normalizedEntryName.StartsWith("attachments/", StringComparison.OrdinalIgnoreCase))
            {
                attachmentCount++;
            }
        }

        if (!hasDatabase)
        {
            throw new InvalidDataException("备份包中未找到 Kanban41.db。");
        }

        return attachmentCount;
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

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN ('Tasks', 'Notes', 'Attachments');
            """;

        var tableCount = Convert.ToInt32(command.ExecuteScalar());

        if (tableCount < 3)
        {
            throw new InvalidDataException("备份数据库结构不完整。");
        }
    }

    private static void MoveCurrentDataAside(string oldDatabasePath, string oldAttachmentRoot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(oldDatabasePath)!);
        DeleteDatabaseSidecarFiles();
        var databaseMoved = false;
        var attachmentsMoved = false;

        try
        {
            if (File.Exists(AppPaths.DatabasePath))
            {
                File.Move(AppPaths.DatabasePath, oldDatabasePath);
                databaseMoved = true;
            }

            if (Directory.Exists(AppPaths.AttachmentRoot))
            {
                Directory.Move(AppPaths.AttachmentRoot, oldAttachmentRoot);
                attachmentsMoved = true;
            }
        }
        catch
        {
            if (databaseMoved && File.Exists(oldDatabasePath) && !File.Exists(AppPaths.DatabasePath))
            {
                File.Move(oldDatabasePath, AppPaths.DatabasePath);
            }

            if (attachmentsMoved && Directory.Exists(oldAttachmentRoot) && !Directory.Exists(AppPaths.AttachmentRoot))
            {
                Directory.Move(oldAttachmentRoot, AppPaths.AttachmentRoot);
            }

            throw;
        }
    }

    private static void RestoreMovedCurrentData(string oldDatabasePath, string oldAttachmentRoot)
    {
        DeleteFileIfExists(AppPaths.DatabasePath);
        DeleteDatabaseSidecarFiles();
        DeleteDirectoryIfExists(AppPaths.AttachmentRoot);

        if (File.Exists(oldDatabasePath))
        {
            File.Move(oldDatabasePath, AppPaths.DatabasePath);
        }

        if (Directory.Exists(oldAttachmentRoot))
        {
            Directory.Move(oldAttachmentRoot, AppPaths.AttachmentRoot);
        }
        else
        {
            Directory.CreateDirectory(AppPaths.AttachmentRoot);
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
}
