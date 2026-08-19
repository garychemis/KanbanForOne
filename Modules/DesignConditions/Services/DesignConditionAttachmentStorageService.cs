using System.Diagnostics;
using System.IO;
using KanbanForOne.Modules.DesignConditions.Data;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Services;

namespace KanbanForOne.Modules.DesignConditions.Services;

public sealed class DesignConditionAttachmentStorageService
{
    private const int MaxFilesPerBatch = 10;
    private const long MaxSingleFileBytes = 200L * 1024 * 1024;
    private const long MaxBatchBytes = 1024L * 1024 * 1024;
    private readonly DesignConditionStorageOptions _paths;

    public DesignConditionAttachmentStorageService(DesignConditionStorageOptions paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyList<DesignConditionAttachment>> CopyFilesAsync(
        Guid conditionId,
        IReadOnlyList<string> sourcePaths,
        int startingSortOrder = 0)
    {
        var files = sourcePaths.Where(File.Exists).Take(MaxFilesPerBatch + 1).Select(path => new FileInfo(path)).ToArray();
        if (files.Length == 0)
        {
            return [];
        }
        if (files.Length > MaxFilesPerBatch)
        {
            throw new InvalidOperationException($"单次最多添加 {MaxFilesPerBatch} 个条件文件。");
        }
        if (files.Any(file => file.Length > MaxSingleFileBytes))
        {
            throw new InvalidOperationException("单个条件文件不能超过 200MB。");
        }
        if (files.Sum(file => file.Length) > MaxBatchBytes)
        {
            throw new InvalidOperationException("单次添加的条件文件总大小不能超过 1GB。");
        }

        _paths.EnsureDirectories();
        var folder = Path.Combine(_paths.AttachmentRoot, conditionId.ToString("N"));
        var stagingFolder = Path.Combine(_paths.StagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingFolder);
        var stagedFiles = new List<(string Staged, string Target)>();
        var movedFiles = new List<string>();
        var result = new List<DesignConditionAttachment>();
        try
        {
            foreach (var file in files)
            {
                var id = Guid.NewGuid();
                var safeName = SafeName(file.Name);
                var storedName = $"{id:N}_{safeName}";
                var target = Path.Combine(folder, storedName);
                var stagedTarget = Path.Combine(stagingFolder, storedName);
                await using (var source = File.Open(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                await using (var destination = File.Create(stagedTarget))
                {
                    await source.CopyToAsync(destination);
                }
                stagedFiles.Add((stagedTarget, target));
                result.Add(new DesignConditionAttachment
                {
                    Id = id,
                    DesignConditionId = conditionId,
                    OriginalFileName = file.Name,
                    StoredFileName = storedName,
                    RelativePath = Path.GetRelativePath(_paths.ModuleRoot, target),
                    FileExtension = file.Extension,
                    FileSizeBytes = file.Length,
                    CreatedAt = DateTime.Now,
                    SortOrder = startingSortOrder + result.Count
                });
            }
            Directory.CreateDirectory(folder);
            foreach (var (staged, target) in stagedFiles)
            {
                File.Move(staged, target);
                movedFiles.Add(target);
            }
        }
        catch
        {
            foreach (var path in movedFiles.Where(File.Exists)) File.Delete(path);
            throw;
        }
        finally
        {
            if (Directory.Exists(stagingFolder)) Directory.Delete(stagingFolder, true);
        }
        return result;
    }

    public string GetAbsolutePath(DesignConditionAttachment attachment)
    {
        var root = Path.GetFullPath(_paths.ModuleRoot) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(_paths.ModuleRoot, attachment.RelativePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("条件文件路径不在设计条件数据目录内。");
        }
        return path;
    }

    public void CleanupStaging()
    {
        _paths.EnsureDirectories();
        foreach (var directory in Directory.EnumerateDirectories(_paths.StagingRoot))
        {
            try { Directory.Delete(directory, true); } catch { }
        }
        foreach (var file in Directory.EnumerateFiles(_paths.StagingRoot))
        {
            try { File.Delete(file); } catch { }
        }
    }

    public void Open(DesignConditionAttachment attachment)
    {
        var path = GetAbsolutePath(attachment);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("条件文件不存在。", path);
        }
        AttachmentOpenSafety.EnsureSafeToOpen(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public void Reveal(DesignConditionAttachment attachment)
    {
        var path = GetAbsolutePath(attachment);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("条件文件不存在。", path);
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    public DesignConditionStagedDelete StageDelete(DesignConditionAttachment attachment)
    {
        var original = GetAbsolutePath(attachment);
        if (!File.Exists(original))
        {
            return new DesignConditionStagedDelete(original, string.Empty);
        }
        _paths.EnsureDirectories();
        var staged = Path.Combine(_paths.TrashRoot, $"{Guid.NewGuid():N}_{Path.GetFileName(original)}");
        File.Move(original, staged);
        return new DesignConditionStagedDelete(original, staged);
    }

    public DesignConditionStagedDelete StageConditionFolderDelete(Guid conditionId)
    {
        var original = Path.Combine(_paths.AttachmentRoot, conditionId.ToString("N"));
        if (!Directory.Exists(original))
        {
            return new DesignConditionStagedDelete(original, string.Empty, true);
        }
        _paths.EnsureDirectories();
        var staged = Path.Combine(_paths.TrashRoot, $"condition_{Guid.NewGuid():N}");
        Directory.Move(original, staged);
        return new DesignConditionStagedDelete(original, staged, true);
    }

    public static void CommitDelete(DesignConditionStagedDelete staged)
    {
        if (string.IsNullOrWhiteSpace(staged.StagedPath))
        {
            return;
        }
        if (staged.IsDirectory && Directory.Exists(staged.StagedPath))
        {
            Directory.Delete(staged.StagedPath, true);
        }
        else if (!staged.IsDirectory && File.Exists(staged.StagedPath))
        {
            File.Delete(staged.StagedPath);
        }
    }

    public static void RollbackDelete(DesignConditionStagedDelete staged)
    {
        if (string.IsNullOrWhiteSpace(staged.StagedPath))
        {
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(staged.OriginalPath)!);
        if (staged.IsDirectory && Directory.Exists(staged.StagedPath) && !Directory.Exists(staged.OriginalPath))
        {
            Directory.Move(staged.StagedPath, staged.OriginalPath);
        }
        else if (!staged.IsDirectory && File.Exists(staged.StagedPath) && !File.Exists(staged.OriginalPath))
        {
            File.Move(staged.StagedPath, staged.OriginalPath);
        }
    }

    private static string SafeName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var value = new string(fileName.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return value.Length == 0 ? "condition-file" : value;
    }
}

public sealed record DesignConditionStagedDelete(string OriginalPath, string StagedPath, bool IsDirectory = false);
