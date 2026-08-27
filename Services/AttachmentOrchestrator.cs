using KanbanForOne.Models;

namespace KanbanForOne.Services;

/// <summary>
/// 附件删除的事务编排：协调底层存储（文件暂存/提交/回滚）与数据库附件记录删除，
/// 在失败时尝试恢复文件与数据库附件记录。从 BoardViewModel 独立出来以便单元测试与复用。
/// </summary>
public sealed class AttachmentOrchestrator
{
    private readonly AttachmentStorageService _storage;
    private readonly AttachmentRepository _repository;

    public AttachmentOrchestrator(AttachmentStorageService storage, AttachmentRepository repository)
    {
        _storage = storage;
        _repository = repository;
    }

    /// <summary>暂存一组附件的文件删除（失败时回滚已暂存项），返回需提交的暂存列表。</summary>
    public IReadOnlyList<StagedAttachmentDelete> StageDeletes(IEnumerable<AttachmentItem> attachments)
    {
        var stagedDeletes = new List<StagedAttachmentDelete>();

        try
        {
            foreach (var attachment in attachments.ToArray())
            {
                stagedDeletes.Add(_storage.StageAttachmentFileForDelete(attachment));
            }

            return stagedDeletes;
        }
        catch
        {
            TryRollbackDeletes(stagedDeletes);
            throw;
        }
    }

    /// <summary>提交全部暂存删除，使文件实际移除。</summary>
    public void CommitDeletes(IEnumerable<StagedAttachmentDelete> stagedDeletes)
    {
        foreach (var stagedDelete in stagedDeletes)
        {
            _storage.CommitStagedDelete(stagedDelete);
        }
    }

    /// <summary>回滚全部暂存删除（逆序），全部成功返回 true。</summary>
    public bool TryRollbackDeletes(IEnumerable<StagedAttachmentDelete> stagedDeletes)
    {
        var succeeded = true;

        foreach (var stagedDelete in stagedDeletes.Reverse())
        {
            succeeded &= TryRollbackDelete(stagedDelete);
        }

        return succeeded;
    }

    /// <summary>单个已暂存删除：从存储回滚临时移走的文件，成功返回 true。</summary>
    public bool TryRollbackDelete(StagedAttachmentDelete stagedDelete)
    {
        try
        {
            _storage.RollbackStagedDelete(stagedDelete);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>失败后尝试恢复一批附件记录，全部成功返回 true。</summary>
    public async Task<bool> RestoreDeletedAttachmentRecordsAsync(IEnumerable<AttachmentItem> attachments)
    {
        try
        {
            await _repository.AddRangeAsync(attachments.ToArray());
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>失败后尝试恢复单个附件记录。</summary>
    public async Task<bool> RestoreAttachmentRecordAsync(AttachmentItem attachment)
        => await RestoreDeletedAttachmentRecordsAsync([attachment]);
}
