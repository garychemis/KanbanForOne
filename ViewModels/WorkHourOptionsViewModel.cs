using System.Collections.ObjectModel;
using KanbanForOne.Services;

namespace KanbanForOne.ViewModels;

/// <summary>
/// 人工时专业/工作内容选项集合，供设置页管理与人工时录入对话框共用。
/// </summary>
public sealed class WorkHourOptionsViewModel : ObservableObject
{
    private readonly WorkHourOptionsService _service;
    private readonly NotificationService _notifications;
    private readonly IDialogService _dialogs;
    private string _newWorkDiscipline = string.Empty;
    private string _newWorkActivity = string.Empty;

    public WorkHourOptionsViewModel(
        WorkHourOptionsService service,
        NotificationService notifications,
        IDialogService dialogs)
    {
        _service = service;
        _notifications = notifications;
        _dialogs = dialogs;

        AddWorkDisciplineCommand = new RelayCommand(AddWorkDisciplineAsync, _ => !string.IsNullOrWhiteSpace(NewWorkDiscipline));
        RemoveWorkDisciplineCommand = new RelayCommand(RemoveWorkDisciplineAsync);
        AddWorkActivityCommand = new RelayCommand(AddWorkActivityAsync, _ => !string.IsNullOrWhiteSpace(NewWorkActivity));
        RemoveWorkActivityCommand = new RelayCommand(RemoveWorkActivityAsync);
    }

    public ObservableCollection<string> WorkDisciplines { get; } = new();

    public ObservableCollection<string> WorkActivities { get; } = new();

    public RelayCommand AddWorkDisciplineCommand { get; }

    public RelayCommand RemoveWorkDisciplineCommand { get; }

    public RelayCommand AddWorkActivityCommand { get; }

    public RelayCommand RemoveWorkActivityCommand { get; }

    public string NewWorkDiscipline
    {
        get => _newWorkDiscipline;
        set
        {
            if (SetProperty(ref _newWorkDiscipline, value))
            {
                AddWorkDisciplineCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewWorkActivity
    {
        get => _newWorkActivity;
        set
        {
            if (SetProperty(ref _newWorkActivity, value))
            {
                AddWorkActivityCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task LoadAsync()
    {
        var options = await _service.LoadAsync();
        CollectionHelper.Replace(WorkDisciplines, options.Disciplines);
        CollectionHelper.Replace(WorkActivities, options.WorkActivities);
    }

    /// <summary>人工时录入保存时自动补充选项（日历录入对话框调用）。</summary>
    public async Task AddOptionsFromEntryAsync(string discipline, string workActivity)
    {
        var options = await _service.AddDisciplineAsync(discipline);
        options = await _service.AddWorkActivityAsync(workActivity);
        CollectionHelper.Replace(WorkDisciplines, options.Disciplines);
        CollectionHelper.Replace(WorkActivities, options.WorkActivities);
    }

    private async Task AddWorkDisciplineAsync(object? _)
    {
        var value = NewWorkDiscipline.Trim();
        if (value.Length == 0)
        {
            return;
        }

        var options = await _service.AddDisciplineAsync(value);
        CollectionHelper.Replace(WorkDisciplines, options.Disciplines);
        NewWorkDiscipline = string.Empty;
        _notifications.Notify($"已添加专业：{value}");
    }

    private async Task AddWorkActivityAsync(object? _)
    {
        var value = NewWorkActivity.Trim();
        if (value.Length == 0)
        {
            return;
        }

        var options = await _service.AddWorkActivityAsync(value);
        CollectionHelper.Replace(WorkActivities, options.WorkActivities);
        NewWorkActivity = string.Empty;
        _notifications.Notify($"已添加工作内容：{value}");
    }

    private async Task RemoveWorkDisciplineAsync(object? parameter)
    {
        if (parameter is not string value ||
            !_dialogs.Confirm("移除专业选项", $"从下拉选项中移除“{value}”吗？历史人工时不会受到影响。", "移除"))
        {
            return;
        }

        var options = await _service.RemoveDisciplineAsync(value);
        CollectionHelper.Replace(WorkDisciplines, options.Disciplines);
        _notifications.Notify($"已移除专业选项：{value}");
    }

    private async Task RemoveWorkActivityAsync(object? parameter)
    {
        if (parameter is not string value ||
            !_dialogs.Confirm("移除工作内容", $"从下拉选项中移除“{value}”吗？历史人工时不会受到影响。", "移除"))
        {
            return;
        }

        var options = await _service.RemoveWorkActivityAsync(value);
        CollectionHelper.Replace(WorkActivities, options.WorkActivities);
        _notifications.Notify($"已移除工作内容选项：{value}");
    }
}
