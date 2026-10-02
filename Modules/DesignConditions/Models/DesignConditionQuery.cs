namespace KanbanForOne.Modules.DesignConditions.Models;

/// <summary>IssuedDate 保留既有成员名，V4 起表示相应记录的归档日期（提出日或接受日）。</summary>
public sealed record DesignConditionCalendarSummary(DateTime IssuedDate, int RecordCount, int DrawingCount);
