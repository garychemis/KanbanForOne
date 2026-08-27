namespace KanbanForOne.Models;

/// <summary>归档分区选择结果（由 View 层对话框填充，ViewModel 层使用）。</summary>
public sealed record ArchiveSectionDialogResult(Guid? SectionId, string? NewSectionName, Guid? DeleteSectionId = null);
