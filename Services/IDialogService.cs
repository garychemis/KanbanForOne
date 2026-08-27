using KanbanForOne.Models;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Modules.DesignConditions.Services;
using KanbanForOne.Modules.DesignConditions.ViewModels;

namespace KanbanForOne.Services;

/// <summary>
/// 对话框抽象：ViewModel 通过本接口触发确认框与领域对话框，不再直接依赖 View 层实现，
/// 使核心交互可在无桌面会话下进行单元测试。实现类位于 View 适配层。
/// </summary>
public interface IDialogService
{
    /// <summary>通用确认框，返回用户是否确认。</summary>
    bool Confirm(string title, string message, string confirmText);

    /// <summary>选择归档目标分区（可新建），返回 null 表示取消。</summary>
    ArchiveSectionDialogResult? PickArchiveSection(IEnumerable<ArchiveSection> sections, ArchiveSection defaultSection);

    /// <summary>从现有归档分区中选择，返回 null 表示取消。</summary>
    ArchiveSectionDialogResult? PickExistingArchiveSection(IEnumerable<ArchiveSection> sections, ArchiveSection? selectedSection);

    /// <summary>人工时录入/编辑对话框，返回 null 表示取消。</summary>
    WorkHourEntryDialogResult? EditWorkHourEntry(WorkHourEntry? entry, DateTime defaultDate, IReadOnlyList<string> disciplines, IReadOnlyList<string> workActivities);

    /// <summary>设计条件编辑器对话框，返回用户动作。</summary>
    DesignConditionEditorAction ShowDesignConditionEditor(DesignConditionEditorViewModel editor, DesignConditionAttachmentStorageService storage);
}
