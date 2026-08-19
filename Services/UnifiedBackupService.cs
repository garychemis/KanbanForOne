using System.IO;
using System.IO.Compression;
using System.Text.Json;
using KanbanForOne.Modules.DesignConditions.Services;

namespace KanbanForOne.Services;

/// <summary>
/// 将核心数据与设计条件数据编排为一个完整备份包，并负责整体恢复和失败回滚。
/// </summary>
public sealed class UnifiedBackupService
{
    private const string PackageType = "Kanban41FullBackup";
    private const int FormatVersion = 1;
    private const long MaxComponentBytes = 10L * 1024 * 1024 * 1024;
    private const long MaxManifestBytes = 1024L * 1024;

    private readonly BackupService _coreBackup;
    private readonly DesignConditionBackupService _designConditionBackup;

    public UnifiedBackupService(
        BackupService coreBackup,
        DesignConditionBackupService designConditionBackup)
    {
        _coreBackup = coreBackup;
        _designConditionBackup = designConditionBackup;
    }

    public Task<BackupResult> CreateBackupAsync()
    {
        return CreateBackupCoreAsync(protective: false);
    }

    internal static void ValidatePackage(string sourcePath)
    {
        _ = InspectPackage(sourcePath);
    }

    public async Task<RestoreBackupResult> RestoreBackupAsync(string backupPath)
    {
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            throw new ArgumentException("请选择一个完整备份文件。", nameof(backupPath));
        }

        var sourcePath = Path.GetFullPath(backupPath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("完整备份文件不存在。", sourcePath);
        }

        // 无效或旧版分模块备份在创建保护包前直接拒绝。
        ValidatePackage(sourcePath);

        // 在修改任何模块前创建包含全部当前数据的保护备份。
        var protective = await CreateBackupCoreAsync(protective: true);
        var restoreRoot = Path.Combine(AppPaths.DataRoot, ".full-restore", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(restoreRoot, "source");
        var rollbackRoot = Path.Combine(restoreRoot, "rollback");
        Directory.CreateDirectory(sourceRoot);

        var coreRestored = false;
        string? transientCoreProtection = null;
        string? transientDesignProtection = null;
        var cleanupTransientProtection = false;
        var cleanupRestoreRoot = true;

        try
        {
            var package = ExtractPackage(sourcePath, sourceRoot);

            var coreResult = await _coreBackup.RestoreBackupAsync(package.CoreBackupPath);
            coreRestored = true;
            transientCoreProtection = coreResult.ProtectiveBackupPath;

            var designResult = await _designConditionBackup.RestoreAsync(package.DesignConditionBackupPath);
            transientDesignProtection = designResult.ProtectiveBackupPath;

            cleanupTransientProtection = true;

            return new RestoreBackupResult(
                sourcePath,
                protective.BackupPath,
                coreResult.AttachmentFileCount + designResult.AttachmentFileCount,
                DateTime.Now);
        }
        catch (Exception original)
        {
            if (!coreRestored)
            {
                throw;
            }

            var rollbackErrors = new List<Exception>();
            try
            {
                Directory.CreateDirectory(rollbackRoot);
                var rollbackPackage = ExtractPackage(protective.BackupPath, rollbackRoot);

                try
                {
                    var designRollback = await _designConditionBackup.RestoreAsync(rollbackPackage.DesignConditionBackupPath);
                    TryDeleteFile(designRollback.ProtectiveBackupPath);
                }
                catch (Exception ex)
                {
                    rollbackErrors.Add(ex);
                }

                try
                {
                    var coreRollback = await _coreBackup.RestoreBackupAsync(rollbackPackage.CoreBackupPath);
                    TryDeleteFile(coreRollback.ProtectiveBackupPath);
                }
                catch (Exception ex)
                {
                    rollbackErrors.Add(ex);
                }
            }
            catch (Exception ex)
            {
                rollbackErrors.Add(ex);
            }

            if (rollbackErrors.Count == 0)
            {
                cleanupTransientProtection = true;
                throw new InvalidOperationException(
                    $"完整备份恢复失败，已自动回滚全部数据。保护备份：{protective.BackupPath}",
                    original);
            }

            cleanupRestoreRoot = false;
            throw new AggregateException(
                $"完整备份恢复失败，且整体回滚未全部完成。保护备份：{protective.BackupPath}；"
                + $"核心临时保护：{transientCoreProtection ?? "无"}；设计条件临时保护：{transientDesignProtection ?? "无"}；恢复现场：{restoreRoot}",
                new[] { original }.Concat(rollbackErrors));
        }
        finally
        {
            if (cleanupTransientProtection)
            {
                TryDeleteFile(transientCoreProtection);
                TryDeleteFile(transientDesignProtection);
            }
            if (cleanupRestoreRoot) TryDeleteDirectory(restoreRoot);
        }
    }

    private async Task<BackupResult> CreateBackupCoreAsync(bool protective)
    {
        AppPaths.EnsureStorageLayout();
        BackupResult? core = null;
        DesignConditionBackupResult? design = null;
        var outputPath = CreateUniquePackagePath(protective);

        try
        {
            core = await _coreBackup.CreateBackupAsync();
            design = await _designConditionBackup.CreateBackupAsync(protective);

            var manifest = new FullBackupManifest(
                PackageType,
                FormatVersion,
                DateTime.Now,
                core.AttachmentFileCount,
                design.AttachmentFileCount);

            using (var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create))
            {
                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(manifestEntry.Open()))
                {
                    writer.Write(JsonSerializer.Serialize(manifest));
                }

                archive.CreateEntryFromFile(core.BackupPath, "modules/core.zip", CompressionLevel.NoCompression);
                archive.CreateEntryFromFile(design.BackupPath, "modules/design-conditions.zip", CompressionLevel.NoCompression);
            }

            _ = InspectPackage(outputPath);
            return new BackupResult(
                outputPath,
                core.AttachmentFileCount + design.AttachmentFileCount,
                new FileInfo(outputPath).Length,
                manifest.CreatedAt);
        }
        catch
        {
            TryDeleteFile(outputPath);
            throw;
        }
        finally
        {
            if (core is not null) TryDeleteFile(core.BackupPath);
            if (design is not null) TryDeleteFile(design.BackupPath);
        }
    }

    private static ExtractedPackage ExtractPackage(string sourcePath, string destinationRoot)
    {
        var manifest = InspectPackage(sourcePath);
        var destination = Path.GetFullPath(destinationRoot);
        Directory.CreateDirectory(destination);

        var corePath = Path.Combine(destination, "core.zip");
        var designPath = Path.Combine(destination, "design-conditions.zip");

        using var archive = ZipFile.OpenRead(sourcePath);
        var requiredBytes = checked(
            archive.GetEntry("modules/core.zip")!.Length
            + archive.GetEntry("modules/design-conditions.zip")!.Length);
        EnsureFreeSpace(destination, requiredBytes);
        ExtractKnownEntry(archive, "modules/core.zip", corePath);
        ExtractKnownEntry(archive, "modules/design-conditions.zip", designPath);

        return new ExtractedPackage(manifest, corePath, designPath);
    }

    private static FullBackupManifest InspectPackage(string sourcePath)
    {
        using var archive = ZipFile.OpenRead(sourcePath);
        if (archive.Entries.Count != 3)
        {
            throw new InvalidDataException("完整备份包结构无效。应包含清单和两个模块数据包。");
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var normalized = NormalizePackageEntry(entry.FullName);
            if (!paths.Add(normalized))
            {
                throw new InvalidDataException($"完整备份包包含重复路径：{normalized}");
            }

            if (normalized is not ("manifest.json" or "modules/core.zip" or "modules/design-conditions.zip"))
            {
                throw new InvalidDataException($"完整备份包包含未知路径：{normalized}");
            }

            if (entry.Length > MaxComponentBytes)
            {
                throw new InvalidDataException($"完整备份组件过大：{normalized}");
            }
        }

        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidDataException("完整备份包缺少 manifest.json。");
        if (manifestEntry.Length > MaxManifestBytes)
        {
            throw new InvalidDataException("完整备份清单超过 1MB。");
        }
        var coreEntry = archive.GetEntry("modules/core.zip")
            ?? throw new InvalidDataException("完整备份包缺少核心数据。");
        var designEntry = archive.GetEntry("modules/design-conditions.zip")
            ?? throw new InvalidDataException("完整备份包缺少设计条件数据。");
        if (coreEntry.Length == 0 || designEntry.Length == 0)
        {
            throw new InvalidDataException("完整备份包包含空的模块数据包。");
        }

        FullBackupManifest? manifest;
        try
        {
            using var reader = new StreamReader(manifestEntry.Open());
            manifest = JsonSerializer.Deserialize<FullBackupManifest>(reader.ReadToEnd());
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("完整备份清单无效。", ex);
        }

        if (manifest is null
            || manifest.PackageType != PackageType
            || manifest.FormatVersion != FormatVersion
            || manifest.CoreAttachmentCount < 0
            || manifest.DesignConditionAttachmentCount < 0)
        {
            throw new InvalidDataException("完整备份类型或格式版本不受支持。");
        }

        return manifest;
    }

    private static string NormalizePackageEntry(string value)
    {
        var normalized = value.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.StartsWith('/')
            || Path.IsPathRooted(normalized)
            || segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException("完整备份包包含不安全的文件路径。");
        }
        return normalized;
    }

    private static void ExtractKnownEntry(ZipArchive archive, string entryName, string outputPath)
    {
        var entry = archive.GetEntry(entryName)
            ?? throw new InvalidDataException($"完整备份包缺少 {entryName}。");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        entry.ExtractToFile(outputPath, overwrite: false);
    }

    private static void EnsureFreeSpace(string path, long requiredBytes)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (root is not null && new DriveInfo(root).AvailableFreeSpace < requiredBytes + 128L * 1024 * 1024)
        {
            throw new IOException("磁盘剩余空间不足，无法安全展开完整备份包。");
        }
    }

    private static string CreateUniquePackagePath(bool protective)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var prefix = protective ? "Kanban41_FullPreRestore" : "Kanban41_FullBackup";

        for (var index = 0; index < 100; index++)
        {
            var suffix = index == 0 ? string.Empty : $"_{index:00}";
            var candidate = Path.Combine(AppPaths.BackupRoot, $"{prefix}_{timestamp}{suffix}.zip");
            if (!File.Exists(candidate)) return candidate;
        }

        throw new IOException("无法创建唯一的完整备份文件名。");
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 临时组件清理失败不应覆盖备份或恢复结果。
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 失败现场会在异常消息中保留；成功流程中的临时目录可在下次启动时清理。
        }
    }

    private sealed record FullBackupManifest(
        string PackageType,
        int FormatVersion,
        DateTime CreatedAt,
        int CoreAttachmentCount,
        int DesignConditionAttachmentCount);

    private sealed record ExtractedPackage(
        FullBackupManifest Manifest,
        string CoreBackupPath,
        string DesignConditionBackupPath);
}
