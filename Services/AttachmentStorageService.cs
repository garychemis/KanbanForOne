using System.Diagnostics;
using System.IO;
using KanbanForOne.Models;

namespace KanbanForOne.Services;

public sealed class AttachmentStorageService
{
    private const int MaxFilesPerDrop = 10;
    private const long MaxSingleFileBytes = 200L * 1024 * 1024;
    private const long MaxBatchBytes = 1024L * 1024 * 1024;

    public string DataRoot => AppPaths.DataRoot;

    public async Task<IReadOnlyList<AttachmentItem>> CopyFilesAsync(
        AttachmentOwnerType ownerType,
        Guid ownerId,
        IReadOnlyList<string> sourcePaths)
    {
        var files = sourcePaths.Where(File.Exists).Take(MaxFilesPerDrop + 1).ToArray();

        if (files.Length == 0)
        {
            return [];
        }

        if (files.Length > MaxFilesPerDrop)
        {
            throw new InvalidOperationException($"单次最多保存 {MaxFilesPerDrop} 个附件。");
        }

        var fileInfos = files.Select(path => new FileInfo(path)).ToArray();

        if (fileInfos.Any(info => info.Length > MaxSingleFileBytes))
        {
            throw new InvalidOperationException("单个附件不能超过 200MB。");
        }

        if (fileInfos.Sum(info => info.Length) > MaxBatchBytes)
        {
            throw new InvalidOperationException("单次拖入附件总大小不能超过 1GB。");
        }

        var attachments = new List<AttachmentItem>();
        var folder = GetOwnerFolder(ownerType, ownerId);
        Directory.CreateDirectory(folder);

        var copiedPaths = new List<string>();

        try
        {
            foreach (var fileInfo in fileInfos)
            {
                var attachmentId = Guid.NewGuid();
                var safeName = BuildSafeFileName(fileInfo.Name);
                var storedName = $"{attachmentId:N}_{safeName}";
                var destination = Path.Combine(folder, storedName);

                await using (var source = File.Open(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                await using (var target = File.Create(destination))
                {
                    await source.CopyToAsync(target);
                }

                copiedPaths.Add(destination);

                attachments.Add(new AttachmentItem
                {
                    Id = attachmentId,
                    OwnerType = ownerType,
                    OwnerId = ownerId,
                    OriginalFileName = fileInfo.Name,
                    StoredFileName = storedName,
                    RelativePath = Path.GetRelativePath(DataRoot, destination),
                    FileExtension = fileInfo.Extension,
                    FileSizeBytes = fileInfo.Length,
                    CreatedAt = DateTime.Now,
                    SortOrder = attachments.Count
                });
            }
        }
        catch
        {
            foreach (var path in copiedPaths)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            throw;
        }

        return attachments;
    }

    public string GetAbsolutePath(AttachmentItem attachment)
    {
        var fullPath = Path.GetFullPath(Path.Combine(DataRoot, attachment.RelativePath));
        var root = Path.GetFullPath(DataRoot);

        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("附件路径不在程序数据目录内。");
        }

        return fullPath;
    }

    public Task DeleteAttachmentFileAsync(AttachmentItem attachment)
    {
        var path = GetAbsolutePath(attachment);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public StagedAttachmentDelete StageAttachmentFileForDelete(AttachmentItem attachment)
    {
        var originalPath = GetAbsolutePath(attachment);

        if (!File.Exists(originalPath))
        {
            return new StagedAttachmentDelete(originalPath, string.Empty);
        }

        var stagingFolder = Path.Combine(DataRoot, ".delete-staging");
        Directory.CreateDirectory(stagingFolder);

        var stagedPath = Path.Combine(stagingFolder, $"{Guid.NewGuid():N}_{Path.GetFileName(originalPath)}");
        File.Move(originalPath, stagedPath);
        return new StagedAttachmentDelete(originalPath, stagedPath);
    }

    public void CommitStagedDelete(StagedAttachmentDelete stagedDelete)
    {
        if (string.IsNullOrWhiteSpace(stagedDelete.StagedPath) || !File.Exists(stagedDelete.StagedPath))
        {
            return;
        }

        File.Delete(stagedDelete.StagedPath);
    }

    public void RollbackStagedDelete(StagedAttachmentDelete stagedDelete)
    {
        if (string.IsNullOrWhiteSpace(stagedDelete.StagedPath) || !File.Exists(stagedDelete.StagedPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(stagedDelete.OriginalPath)!);

        if (!File.Exists(stagedDelete.OriginalPath))
        {
            File.Move(stagedDelete.StagedPath, stagedDelete.OriginalPath);
        }
    }

    public void OpenAttachment(AttachmentItem attachment)
    {
        var path = GetAbsolutePath(attachment);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("附件文件不存在。", path);
        }

        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }

    public void RevealAttachment(AttachmentItem attachment)
    {
        var path = GetAbsolutePath(attachment);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("附件文件不存在。", path);
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
        {
            UseShellExecute = true
        });
    }

    private string GetOwnerFolder(AttachmentOwnerType ownerType, Guid ownerId)
    {
        var ownerFolder = ownerType == AttachmentOwnerType.Task ? "tasks" : "notes";
        return Path.Combine(AppPaths.AttachmentRoot, ownerFolder, ownerId.ToString("N"));
    }

    private static string BuildSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeChars = fileName.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        var safeName = new string(safeChars).Trim();
        return string.IsNullOrWhiteSpace(safeName) ? "attachment" : safeName;
    }
}

public sealed record StagedAttachmentDelete(string OriginalPath, string StagedPath);
