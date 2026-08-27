using KanbanForOne.Controls;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Modules.DesignConditions.Services;
using KanbanForOne.Modules.DesignConditions.ViewModels;
using KanbanForOne.Modules.DesignConditions.Views;
using KanbanForOne.Models;

namespace KanbanForOne.Services;

/// <summary>
/// <see cref="IDialogService"/> 的 WPF 实现：把对话框请求转发给 View 层对话框。
/// 本类位于 View 适配边界，是项目内唯一允许引用具体对话框的地方。
/// </summary>
public sealed class DialogService : IDialogService
{
    public bool Confirm(string title, string message, string confirmText)
        => DialogHelper.Confirm(title, message, confirmText);

    public ArchiveSectionDialogResult? PickArchiveSection(IEnumerable<ArchiveSection> sections, ArchiveSection defaultSection)
        => ArchiveSectionDialog.Show(DialogHelper.GetDialogOwner(), sections, defaultSection);

    public ArchiveSectionDialogResult? PickExistingArchiveSection(IEnumerable<ArchiveSection> sections, ArchiveSection? selectedSection)
        => ArchiveSectionPickerDialog.Show(DialogHelper.GetDialogOwner(), sections, selectedSection);

    public WorkHourEntryDialogResult? EditWorkHourEntry(
        WorkHourEntry? entry,
        DateTime defaultDate,
        IReadOnlyList<string> disciplines,
        IReadOnlyList<string> workActivities)
        => WorkHourEntryDialog.Show(DialogHelper.GetDialogOwner(), entry, defaultDate, disciplines, workActivities);

    public DesignConditionEditorAction ShowDesignConditionEditor(
        DesignConditionEditorViewModel editor,
        DesignConditionAttachmentStorageService storage)
        => DesignConditionEditorDialog.Show(DialogHelper.GetDialogOwner(), editor, storage);
}
