namespace KanbanForOne.Models;

/// <summary>
/// 任务日期范围计算的共享辅助，供看板与日历共用。
/// </summary>
public static class TaskDateHelper
{
    /// <summary>返回任务的有效日期范围（Start 不晚于 End，缺失的一侧用另一侧补齐）。</summary>
    public static (DateTime Start, DateTime End) DateRange(TaskItem task)
    {
        var start = (task.StartDate ?? task.EndDate)!.Value.Date;
        var end = (task.EndDate ?? task.StartDate)!.Value.Date;

        return start <= end
            ? (start, end)
            : (end, start);
    }
}
