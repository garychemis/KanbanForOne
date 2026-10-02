using KanbanForOne.Modules.DesignConditions.Models;

namespace KanbanForOne.Modules.DesignConditions.Repositories;

/// <summary>设计条件数据访问抽象，便于在测试中注入可控实现（如模拟挂起/失效）。</summary>
public interface IDesignConditionRepository
{
    Task<IReadOnlyList<DesignConditionEntry>> GetAllAsync();
    /// <summary>保留旧方法名；按归档日期查询，提出取提出日，接受取接受日。</summary>
    Task<IReadOnlyList<DesignConditionEntry>> GetByIssuedDateAsync(DateTime date);
    /// <summary>保留旧方法名；日期范围包含两端，采用记录各自的归档日期。</summary>
    Task<IReadOnlyList<DesignConditionEntry>> GetByIssuedDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IReadOnlyList<DesignConditionCalendarSummary>> GetCalendarSummariesAsync(DateTime startDate, DateTime endDate);
    Task UpsertAsync(DesignConditionEntry entry);
    Task SaveAggregateAsync(
        DesignConditionEntry entry,
        IEnumerable<Guid> deletedAttachmentIds,
        IEnumerable<DesignConditionAttachment> addedAttachments);
    Task DeleteAsync(Guid id);
}
