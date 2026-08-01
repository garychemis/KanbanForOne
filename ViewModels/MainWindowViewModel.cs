using System.ComponentModel;
using System.Reflection;
using KanbanForOne.Services;

namespace KanbanForOne.ViewModels;

/// <summary>
/// 主窗口壳：视图导航、全局搜索/日期过滤、顶部通知，以及各子页 ViewModel 的组合与编排。
/// </summary>
public sealed class MainWindowViewModel : ObservableObject
{
    private static readonly Assembly AppAssembly = typeof(MainWindowViewModel).Assembly;

    private readonly DatabaseService _databaseService;
    private readonly NotificationService _notifications;
    private readonly WorkspaceFilterState _filter;
    private readonly BoardViewModel _board;
    private readonly CalendarViewModel _calendar;
    private readonly BackupViewModel _backup;
    private readonly WorkHourOptionsViewModel _workHourOptions;
    private CancellationTokenSource? _searchRefreshCancellation;
    private string _currentFilterLabel = "看板";

    public MainWindowViewModel(
        DatabaseService databaseService,
        NotificationService notifications,
        WorkspaceFilterState filter,
        BoardViewModel board,
        CalendarViewModel calendar,
        BackupViewModel backup,
        SettingsViewModel settings,
        WorkHourOptionsViewModel workHourOptions,
        WorkHourSummaryViewModel workHourSummary)
    {
        _databaseService = databaseService;
        _notifications = notifications;
        _filter = filter;
        _board = board;
        _calendar = calendar;
        _backup = backup;
        Settings = settings;
        WorkHourSummary = workHourSummary;
        _workHourOptions = workHourOptions;

        RelayCommand.UnhandledException -= OnCommandUnhandledException;
        RelayCommand.UnhandledException += OnCommandUnhandledException;

        _filter.PropertyChanged += OnFilterPropertyChanged;
        _notifications.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
        _backup.RestoreCompleted += InitializeCoreAsync;

        ChangeFilterCommand = new RelayCommand(ChangeFilterAsync);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        ClearDateFilterCommand = new RelayCommand(_ => ClearDateFilter(), _ => HasDateFilter);
    }

    public BoardViewModel Board => _board;

    public CalendarViewModel Calendar => _calendar;

    public BackupViewModel Backup => _backup;

    public SettingsViewModel Settings { get; }

    public WorkHourSummaryViewModel WorkHourSummary { get; }

    public RelayCommand ChangeFilterCommand { get; }

    public RelayCommand ClearSearchCommand { get; }

    public RelayCommand ClearDateFilterCommand { get; }

    public string SearchText
    {
        get => _filter.SearchText;
        set => _filter.SearchText = value;
    }

    public DateTime? DateFilterStart
    {
        get => _filter.DateFilterStart;
        set => _filter.DateFilterStart = value;
    }

    public DateTime? DateFilterEnd
    {
        get => _filter.DateFilterEnd;
        set => _filter.DateFilterEnd = value;
    }

    public bool HasDateFilter => _filter.HasDateFilter;

    public string CurrentFilterLabel
    {
        get => _currentFilterLabel;
        private set => SetProperty(ref _currentFilterLabel, value);
    }

    public string CurrentFilter => _filter.CurrentFilter;

    public bool IsArchiveFilter => _filter.IsArchiveFilter;

    public bool IsBoardViewVisible => _filter.CurrentFilter is not "Calendar" and not "WorkHourSummary" and not "Backup" and not "Settings";

    public bool IsCalendarViewVisible => _filter.CurrentFilter == "Calendar";

    public bool IsWorkHourSummaryViewVisible => _filter.CurrentFilter == "WorkHourSummary";

    public bool IsBackupViewVisible => _filter.CurrentFilter == "Backup";

    public bool IsSettingsViewVisible => _filter.CurrentFilter == "Settings";

    public string NotificationText => _notifications.NotificationText;

    public bool HasNotification => _notifications.HasNotification;

    public string AppVersion => AppAssembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion
        ?? AppAssembly.GetName().Version?.ToString()
        ?? string.Empty;

    public string CopyrightText => AppAssembly
        .GetCustomAttribute<AssemblyCopyrightAttribute>()?
        .Copyright
        ?? string.Empty;

    public async Task InitializeAsync()
    {
        await InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            await _databaseService.InitializeAsync();
            WorkHourSummary.Invalidate();
            await _workHourOptions.LoadAsync();
            await _board.InitializeWorkspaceAsync();
            await _calendar.InitializeAsync();
        }
        catch (Exception ex)
        {
            _notifications.Notify($"初始化失败：{ex.Message}");
        }
        finally
        {
            _board.Refresh();
        }
    }

    private void OnFilterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkspaceFilterState.SearchText))
        {
            ScheduleSearchRefresh();
            return;
        }

        if (e.PropertyName is nameof(WorkspaceFilterState.DateFilterStart) or nameof(WorkspaceFilterState.DateFilterEnd))
        {
            OnPropertyChanged(nameof(HasDateFilter));
            ClearDateFilterCommand.RaiseCanExecuteChanged();
            RefreshAll();
        }
    }

    private void RefreshAll()
    {
        _board.Refresh();
    }

    private void ClearDateFilter()
    {
        if (!_filter.HasDateFilter)
        {
            return;
        }

        _filter.DateFilterStart = null;
        _filter.DateFilterEnd = null;
        OnPropertyChanged(nameof(HasDateFilter));
        ClearDateFilterCommand.RaiseCanExecuteChanged();
    }

    private async Task ChangeFilterAsync(object? parameter)
    {
        var filter = parameter as string ?? "Board";
        var previousFilter = _filter.CurrentFilter;

        if (!_board.ConfirmDiscardSpotlightChanges())
        {
            return;
        }

        _filter.CurrentFilter = filter;
        OnPropertyChanged(nameof(CurrentFilter));
        OnPropertyChanged(nameof(IsArchiveFilter));
        _board.CloseSpotlightForNavigation();
        CurrentFilterLabel = filter switch
        {
            "AllTasks" => "全部任务",
            "Today" => "今日任务",
            "Calendar" => "日历",
            "WorkHourSummary" => "人工时汇总",
            "High" => "高优先级",
            "Overdue" => "超期未完成",
            "WithAttachments" => "有附件",
            "Archived" => "归档",
            "Backup" => "数据备份",
            "Settings" => "设置",
            _ => "看板"
        };

        OnPropertyChanged(nameof(IsBoardViewVisible));
        OnPropertyChanged(nameof(IsCalendarViewVisible));
        OnPropertyChanged(nameof(IsWorkHourSummaryViewVisible));
        OnPropertyChanged(nameof(IsBackupViewVisible));
        OnPropertyChanged(nameof(IsSettingsViewVisible));

        try
        {
            if (filter == "Archived")
            {
                await _board.LoadArchiveSectionsAsync();
                _board.PrepareArchiveView();
            }
            else if (previousFilter == "Archived")
            {
                await _board.LoadActiveWorkspaceAsync();
            }

            if (filter == "WorkHourSummary")
            {
                await WorkHourSummary.EnsureLoadedAsync();
            }
        }
        catch (Exception ex)
        {
            _notifications.Notify($"切换视图失败：{ex.Message}");
        }

        _board.Refresh();
    }

    private void OnCommandUnhandledException(Exception exception)
    {
        _notifications.Notify($"操作失败：{exception.Message}");
    }

    private void ScheduleSearchRefresh()
    {
        _searchRefreshCancellation?.Cancel();
        _searchRefreshCancellation?.Dispose();

        var cancellation = new CancellationTokenSource();
        _searchRefreshCancellation = cancellation;
        _ = RefreshSearchAfterDelayAsync(cancellation);
    }

    private async Task RefreshSearchAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(180, cancellation.Token);

            if (!cancellation.IsCancellationRequested)
            {
                RefreshAll();
            }
        }
        catch (OperationCanceledException)
        {
            // A newer search input superseded this refresh.
        }
        finally
        {
            if (ReferenceEquals(_searchRefreshCancellation, cancellation))
            {
                _searchRefreshCancellation = null;
            }

            cancellation.Dispose();
        }
    }
}
