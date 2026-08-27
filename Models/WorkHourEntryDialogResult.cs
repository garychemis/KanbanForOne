namespace KanbanForOne.Models;

/// <summary>人工时录入对话框动作。</summary>
public enum WorkHourDialogAction
{
    None,
    Save,
    Delete
}

/// <summary>人工时录入结果（由 View 层对话框填充，ViewModel 层使用）。</summary>
public sealed record WorkHourEntryDialogResult(
    WorkHourDialogAction Action,
    Guid Id,
    DateTime WorkDate,
    string ProjectNumber,
    string Discipline,
    string WorkActivity,
    int HourUnits,
    string Remark,
    DateTime CreatedAt);
