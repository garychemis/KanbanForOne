using KanbanForOne.Modules.DesignConditions.Models;

namespace KanbanForOne.Modules.DesignConditions.Repositories;

/// <summary>设计条件数据访问抽象，便于在测试中注入可控实现（如模拟挂起/失效）。</summary>
public interface IDesignConditionRepository
{
    Task<IReadOnlyList<DesignConditionEntry>> GetAllAsync();
    Task<IReadOnlyList<DesignConditionEntry>> GetByIssuedDateAsync(DateTime date);
    Task<IReadOnlyList<DesignConditionEntry>> GetByIssuedDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IReadOnlyList<DesignConditionCalendarSummary>> GetCalendarSummariesAsync(DateTime startDate, DateTime endDate);
    Task UpsertAsync(DesignConditionEntry entry);
    Task SaveAggregateAsync(
        DesignConditionEntry entry,
        IEnumerable<Guid> deletedAttachmentIds,
        IEnumerable<DesignConditionAttachment> addedAttachments);
    Task DeleteAsync(Guid id);
}
