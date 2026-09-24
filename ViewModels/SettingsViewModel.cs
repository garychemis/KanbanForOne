using KanbanForOne.Services;
using KanbanForOne.Modules.DesignConditions.Data;

namespace KanbanForOne.ViewModels;

/// <summary>
/// 设置页：数据位置与人工时选项管理（转发自 WorkHourOptionsViewModel）。
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly WorkHourOptionsViewModel _workHourOptions;
    private readonly DesignConditionStorageOptions _designConditionPaths;

    public SettingsViewModel(WorkHourOptionsViewModel workHourOptions, DesignConditionStorageOptions designConditionPaths, ThemeService theme)
    {
        _workHourOptions = workHourOptions;
        _designConditionPaths = designConditionPaths;
        Theme = theme;
    }

    public ThemeService Theme { get; }

    public string DataDirectory => AppPaths.DataRoot;

    public string DatabasePath => AppPaths.DatabasePath;

    public string WorkHourOptionsPath => AppPaths.WorkHourOptionsPath;

    public string AttachmentDirectory => AppPaths.AttachmentRoot;

    public string BackupDirectory => AppPaths.BackupRoot;

    public string DesignConditionDatabasePath => _designConditionPaths.DatabasePath;

    public string DesignConditionAttachmentDirectory => _designConditionPaths.AttachmentRoot;

    public System.Collections.ObjectModel.ObservableCollection<string> WorkDisciplines => _workHourOptions.WorkDisciplines;

    public System.Collections.ObjectModel.ObservableCollection<string> WorkActivities => _workHourOptions.WorkActivities;

    public string NewWorkDiscipline
    {
        get => _workHourOptions.NewWorkDiscipline;
        set => _workHourOptions.NewWorkDiscipline = value;
    }

    public string NewWorkActivity
    {
        get => _workHourOptions.NewWorkActivity;
        set => _workHourOptions.NewWorkActivity = value;
    }

    public RelayCommand AddWorkDisciplineCommand => _workHourOptions.AddWorkDisciplineCommand;

    public RelayCommand RemoveWorkDisciplineCommand => _workHourOptions.RemoveWorkDisciplineCommand;

    public RelayCommand AddWorkActivityCommand => _workHourOptions.AddWorkActivityCommand;

    public RelayCommand RemoveWorkActivityCommand => _workHourOptions.RemoveWorkActivityCommand;
}
