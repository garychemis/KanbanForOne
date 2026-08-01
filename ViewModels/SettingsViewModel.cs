using System.Reflection;
using KanbanForOne.Services;

namespace KanbanForOne.ViewModels;

/// <summary>
/// 设置页：数据位置、版本信息、更新日志，以及人工时选项管理（转发自 WorkHourOptionsViewModel）。
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private static readonly Assembly AppAssembly = typeof(SettingsViewModel).Assembly;

    private readonly WorkHourOptionsViewModel _workHourOptions;

    public SettingsViewModel(WorkHourOptionsViewModel workHourOptions)
    {
        _workHourOptions = workHourOptions;
    }

    public string DataDirectory => AppPaths.DataRoot;

    public string DatabasePath => AppPaths.DatabasePath;

    public string WorkHourOptionsPath => AppPaths.WorkHourOptionsPath;

    public string AttachmentDirectory => AppPaths.AttachmentRoot;

    public string BackupDirectory => AppPaths.BackupRoot;

    public string AppVersion => AppAssembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion
        ?? AppAssembly.GetName().Version?.ToString()
        ?? string.Empty;

    public string CopyrightText => AppAssembly
        .GetCustomAttribute<AssemblyCopyrightAttribute>()?
        .Copyright
        ?? string.Empty;

    public IReadOnlyList<ReleaseNoteEntry> ReleaseNotes => ReleaseNotesService.FromAssembly(AppAssembly);

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
