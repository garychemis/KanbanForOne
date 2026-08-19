using System.Collections.ObjectModel;
using KanbanForOne.Controls;
using KanbanForOne.Models;
using KanbanForOne.Services;
using KanbanForOne.Modules.DesignConditions.ViewModels;
using TaskStatus = KanbanForOne.Models.TaskStatus;

namespace KanbanForOne.ViewModels;

/// <summary>
/// 日历页：月历网格、当天任务与人工时、人工时录入对话框。
/// </summary>
public sealed class CalendarViewModel : ObservableObject
{
    private readonly BoardViewModel _board;
    private readonly WorkHourRepository _workHourRepository;
    private readonly WorkHourOptionsViewModel _workHourOptions;
    private readonly WorkHourSummaryViewModel _workHourSummary;
    private readonly NotificationService _notifications;
    private readonly WorkspaceFilterState _filter;
    private readonly DesignConditionCalendarSectionViewModel _designConditions;
    private DateTime _calendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _selectedCalendarDate = DateTime.Today;
    private string _calendarViewMode = "Month";
    private int _workHourLoadVersion;

    public CalendarViewModel(
        BoardViewModel board,
        WorkHourRepository workHourRepository,
        WorkHourOptionsViewModel workHourOptions,
        WorkHourSummaryViewModel workHourSummary,
        NotificationService notifications,
        WorkspaceFilterState filter,
        DesignConditionCalendarSectionViewModel designConditions)
    {
        _board = board;
        _workHourRepository = workHourRepository;
        _workHourOptions = workHourOptions;
        _workHourSummary = workHourSummary;
        _notifications = notifications;
        _filter = filter;
        _designConditions = designConditions;
        _designConditions.SummariesChanged += (_, _) => ApplyDesignConditionSummaries();

        _board.DataChanged += refreshCalendar =>
        {
            if (refreshCalendar)
            {
                RefreshTasks();
            }
        };

        PreviousCalendarMonthCommand = new RelayCommand(() => CalendarMonth = CalendarMonth.AddMonths(-1));
        NextCalendarMonthCommand = new RelayCommand(() => CalendarMonth = CalendarMonth.AddMonths(1));
        GoToTodayCommand = new RelayCommand(GoToToday);
        SelectCalendarDateCommand = new RelayCommand(SelectCalendarDate);
        CreateTaskForCalendarDateCommand = new RelayCommand(CreateTaskForCalendarDateAsync);
        MoveTaskToCalendarDateCommand = new RelayCommand(MoveTaskToCalendarDateAsync);
        SetCalendarViewModeCommand = new RelayCommand(SetCalendarViewMode);
        AddWorkHourCommand = new RelayCommand(AddWorkHourAsync);
        OpenWorkHourCommand = new RelayCommand(OpenWorkHourAsync);
    }

    public ObservableCollection<TaskItem> CalendarTasks { get; } = new();

    public ObservableCollection<CalendarDayItem> CalendarDays { get; } = new();

    public ObservableCollection<TaskItem> SelectedCalendarTasks { get; } = new();

    public ObservableCollection<WorkHourEntry> SelectedCalendarWorkHours { get; } = new();

    public DesignConditionCalendarSectionViewModel DesignConditions => _designConditions;

    public IReadOnlyList<string> CalendarWeekdayHeaders { get; } = ["周一", "周二", "周三", "周四", "周五", "周六", "周日"];

    public RelayCommand PreviousCalendarMonthCommand { get; }

    public RelayCommand NextCalendarMonthCommand { get; }

    public RelayCommand GoToTodayCommand { get; }

    public RelayCommand SelectCalendarDateCommand { get; }

    public RelayCommand CreateTaskForCalendarDateCommand { get; }

    public RelayCommand MoveTaskToCalendarDateCommand { get; }

    public RelayCommand SetCalendarViewModeCommand { get; }

    public RelayCommand AddWorkHourCommand { get; }

    public RelayCommand OpenWorkHourCommand { get; }

    /// <summary>点击日历任务卡片时打开 Spotlight（转发至看板）。</summary>
    public RelayCommand OpenTaskCommand => _board.OpenTaskCommand;

    public DateTime CalendarMonth
    {
        get => _calendarMonth;
        private set
        {
            var normalized = new DateTime(value.Year, value.Month, 1);

            if (SetProperty(ref _calendarMonth, normalized))
            {
                if (SelectedCalendarDate.Year != normalized.Year || SelectedCalendarDate.Month != normalized.Month)
                {
                    _selectedCalendarDate = normalized;
                    OnPropertyChanged(nameof(SelectedCalendarDate));
                    OnPropertyChanged(nameof(SelectedCalendarDateDisplay));
                }

                OnPropertyChanged(nameof(CalendarMonthTitle));
                _board.Refresh();
                _ = LoadSelectedWorkHoursAsync();
                ObserveDesignConditionLoad(LoadDesignConditionCalendarAsync());
            }
        }
    }

    public string CalendarMonthTitle => CalendarMonth.ToString("yyyy年 M月");

    public DateTime SelectedCalendarDate
    {
        get => _selectedCalendarDate;
        private set
        {
            if (SetProperty(ref _selectedCalendarDate, value.Date))
            {
                OnPropertyChanged(nameof(SelectedCalendarDateDisplay));
                RefreshCalendarSelection();
                _ = LoadSelectedWorkHoursAsync();
                ObserveDesignConditionLoad(_designConditions.LoadSelectedDateAsync(value.Date));
            }
        }
    }

    public string SelectedCalendarDateDisplay => SelectedCalendarDate.ToString("yyyy年 M月 d日");

    public string CalendarViewMode
    {
        get => _calendarViewMode;
        private set
        {
            if (SetProperty(ref _calendarViewMode, value))
            {
                OnPropertyChanged(nameof(IsCalendarMonthMode));
                OnPropertyChanged(nameof(IsCalendarListMode));
            }
        }
    }

    public bool IsCalendarMonthMode => CalendarViewMode == "Month";

    public bool IsCalendarListMode => CalendarViewMode == "List";

    public int CalendarTaskCount => CalendarTasks.Count;

    public bool HasCalendarTasks => CalendarTaskCount > 0;

    public int SelectedCalendarTaskCount => SelectedCalendarTasks.Count;

    public bool HasSelectedCalendarTasks => SelectedCalendarTaskCount > 0;

    public int SelectedCalendarWorkHourCount => SelectedCalendarWorkHours.Count;

    public bool HasSelectedCalendarWorkHours => SelectedCalendarWorkHourCount > 0;

    public int SelectedCalendarWorkHourUnits => SelectedCalendarWorkHours.Sum(entry => entry.HourUnits);

    public string SelectedCalendarWorkHourTotalDisplay => $"{WorkHourValueConverter.FormatHours(SelectedCalendarWorkHourUnits)}h";

    private IReadOnlyList<TaskItem>? _lastCalendarTasks;
    private IReadOnlyList<DateTime>? _lastTaskUpdatedAt;
    private DateTime _lastCalendarMonth;
    private DateTime _lastSelectedCalendarDate;

    /// <summary>应用启动/恢复后加载选中日期的人工时。</summary>
    public async Task InitializeAsync()
    {
        await LoadSelectedWorkHoursAsync();
        RefreshTasks();
        await LoadDesignConditionCalendarAsync();
    }

    /// <summary>看板任务变化后刷新日历任务列表与网格（由 Board.DataChanged 触发）。</summary>
    public void RefreshTasks()
    {
        var calendarTasks = _board.AllTasks
            .Where(task => !task.IsArchived && (task.StartDate.HasValue || task.EndDate.HasValue))
            .Where(PassesTaskSearch)
            .OrderBy(task => task.StartDate ?? task.EndDate)
            .ThenBy(task => task.EndDate ?? task.StartDate)
            .ThenBy(task => task.SortOrder)
            .ToArray();

        CollectionHelper.Replace(CalendarTasks, calendarTasks);

        // 任务序列、内容（UpdatedAt）、月份与选中日期均未变化时跳过 42 天网格重建，
        // 避免无意义刷新触发 O(42×N) 重算；任务任何内容变化（标题/状态/优先级等）
        // 都会使 UpdatedAt 变化，从而保证月历芯片（快照对象）仍能刷新。
        var tasksUnchanged = _lastCalendarTasks is not null
            && _lastTaskUpdatedAt is not null
            && _lastCalendarTasks.Count == calendarTasks.Length
            && calendarTasks.SequenceEqual(_lastCalendarTasks)
            && calendarTasks.Select(task => task.UpdatedAt).SequenceEqual(_lastTaskUpdatedAt);
        var layoutUnchanged = tasksUnchanged
            && _lastCalendarMonth == CalendarMonth
            && _lastSelectedCalendarDate == SelectedCalendarDate;

        if (!layoutUnchanged)
        {
            RefreshCalendar(calendarTasks);
        }

        _lastCalendarTasks = calendarTasks;
        _lastTaskUpdatedAt = calendarTasks.Select(task => task.UpdatedAt).ToArray();
        _lastCalendarMonth = CalendarMonth;
        _lastSelectedCalendarDate = SelectedCalendarDate;

        OnPropertyChanged(nameof(CalendarTaskCount));
        OnPropertyChanged(nameof(HasCalendarTasks));
        OnPropertyChanged(nameof(SelectedCalendarTaskCount));
        OnPropertyChanged(nameof(HasSelectedCalendarTasks));
    }

    private void RefreshCalendar(IReadOnlyList<TaskItem> calendarTasks)
    {
        const int visibleChipLimit = 3;
        var firstOfMonth = CalendarMonth;
        var gridStart = firstOfMonth.AddDays(-(((int)firstOfMonth.DayOfWeek + 6) % 7));
        var days = new List<CalendarDayItem>(42);

        for (var offset = 0; offset < 42; offset++)
        {
            var date = gridStart.AddDays(offset);
            var day = new CalendarDayItem
            {
                Date = date,
                IsToday = date == DateTime.Today,
                IsCurrentMonth = date.Month == CalendarMonth.Month && date.Year == CalendarMonth.Year,
                IsSelected = date == SelectedCalendarDate
            };

            var chips = calendarTasks
                .Where(task => IsTaskActiveOnDate(task, date))
                .OrderBy(task => TaskDateHelper.DateRange(task).Start)
                .ThenBy(task => TaskDateHelper.DateRange(task).End)
                .ThenBy(task => task.SortOrder)
                .Select(task => CreateCalendarTaskChip(task, date))
                .ToArray();

            day.TotalTaskCount = chips.Length;
            day.OverflowCount = Math.Max(0, chips.Length - visibleChipLimit);

            foreach (var chip in chips.Take(visibleChipLimit))
            {
                day.VisibleTaskChips.Add(chip);
            }

            days.Add(day);
        }

        CollectionHelper.Replace(CalendarDays, days);
        ApplyDesignConditionSummaries();
        RefreshCalendarSelection();
    }

    private void RefreshCalendarSelection()
    {
        foreach (var day in CalendarDays)
        {
            day.IsSelected = day.Date == SelectedCalendarDate;
        }

        CollectionHelper.Replace(
            SelectedCalendarTasks,
            CalendarTasks
                .Where(task => IsTaskActiveOnDate(task, SelectedCalendarDate))
                .OrderBy(task => TaskDateHelper.DateRange(task).Start)
                .ThenBy(task => TaskDateHelper.DateRange(task).End)
                .ThenBy(task => task.SortOrder));

        OnPropertyChanged(nameof(SelectedCalendarTaskCount));
        OnPropertyChanged(nameof(HasSelectedCalendarTasks));
    }

    private static CalendarTaskChip CreateCalendarTaskChip(TaskItem task, DateTime date)
    {
        var range = TaskDateHelper.DateRange(task);
        var isMultiDay = range.Start != range.End;

        return new CalendarTaskChip
        {
            Task = task,
            Date = date,
            IsMultiDay = isMultiDay,
            StartsOnDate = range.Start == date,
            EndsOnDate = range.End == date
        };
    }

    private static bool IsTaskActiveOnDate(TaskItem task, DateTime date)
    {
        if (task.StartDate is null && task.EndDate is null)
        {
            return false;
        }

        var range = TaskDateHelper.DateRange(task);
        return range.Start <= date.Date && date.Date <= range.End;
    }

    private bool PassesTaskSearch(TaskItem task)
    {
        if (string.IsNullOrWhiteSpace(_filter.NormalizedSearchText))
        {
            return true;
        }

        var query = _filter.NormalizedSearchText;
        return task.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || task.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || task.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase))
            || task.Attachments.Any(attachment => attachment.OriginalFileName.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private Task AddWorkHourAsync(object? _)
    {
        return ShowWorkHourDialogAsync(null);
    }

    private Task OpenWorkHourAsync(object? parameter)
    {
        return parameter is WorkHourEntry entry
            ? ShowWorkHourDialogAsync(entry)
            : Task.CompletedTask;
    }

    private async Task ShowWorkHourDialogAsync(WorkHourEntry? entry)
    {
        var result = WorkHourEntryDialog.Show(
            DialogHelper.GetDialogOwner(),
            entry,
            SelectedCalendarDate,
            _workHourOptions.WorkDisciplines,
            _workHourOptions.WorkActivities);

        if (result is null || result.Action == WorkHourDialogAction.None)
        {
            return;
        }

        if (result.Action == WorkHourDialogAction.Delete)
        {
            await _workHourRepository.DeleteAsync(result.Id);
            _workHourSummary.Invalidate();
            await LoadSelectedWorkHoursAsync();
            _notifications.Notify("已删除人工时记录");
            return;
        }

        var otherUnits = await _workHourRepository.GetTotalUnitsByDateAsync(
            result.WorkDate,
            entry?.Id);
        if (otherUnits + result.HourUnits > WorkHourValueConverter.MaximumEntryUnits &&
            !ConfirmDialog.Show(
                DialogHelper.GetDialogOwner(),
                "当天工时超过 24 小时",
                $"保存后 {result.WorkDate:yyyy/M/d} 的总工时将超过 24 小时，是否继续？",
                "继续保存"))
        {
            _notifications.Notify("已取消保存人工时");
            return;
        }

        var now = DateTime.Now;
        var savedEntry = new WorkHourEntry
        {
            Id = result.Id,
            WorkDate = result.WorkDate,
            ProjectNumber = WorkHourValueConverter.NormalizeProjectNumber(result.ProjectNumber),
            Discipline = result.Discipline.Trim(),
            WorkActivity = result.WorkActivity.Trim(),
            HourUnits = result.HourUnits,
            Remark = result.Remark.Trim(),
            CreatedAt = result.CreatedAt,
            UpdatedAt = now
        };

        await _workHourOptions.AddOptionsFromEntryAsync(savedEntry.Discipline, savedEntry.WorkActivity);
        await _workHourRepository.UpsertAsync(savedEntry);
        _workHourSummary.Invalidate();

        if (savedEntry.WorkDate != SelectedCalendarDate)
        {
            SelectCalendarDate(savedEntry.WorkDate);
        }
        else
        {
            await LoadSelectedWorkHoursAsync();
        }

        _notifications.Notify(entry is null ? "已添加人工时" : "已保存人工时");
    }

    private async Task LoadSelectedWorkHoursAsync()
    {
        var version = ++_workHourLoadVersion;
        var selectedDate = SelectedCalendarDate;
        try
        {
            var entries = await _workHourRepository.GetByDateAsync(selectedDate);
            if (version != _workHourLoadVersion || selectedDate != SelectedCalendarDate)
            {
                return;
            }

            CollectionHelper.Replace(SelectedCalendarWorkHours, entries);
            NotifySelectedWorkHourSummaryChanged();
        }
        catch (Exception ex)
        {
            if (version == _workHourLoadVersion)
            {
                _notifications.Notify($"加载人工时失败：{ex.Message}");
            }
        }
    }

    private void NotifySelectedWorkHourSummaryChanged()
    {
        OnPropertyChanged(nameof(SelectedCalendarWorkHourCount));
        OnPropertyChanged(nameof(HasSelectedCalendarWorkHours));
        OnPropertyChanged(nameof(SelectedCalendarWorkHourUnits));
        OnPropertyChanged(nameof(SelectedCalendarWorkHourTotalDisplay));
    }

    private void GoToToday()
    {
        var today = DateTime.Today;
        _calendarMonth = new DateTime(today.Year, today.Month, 1);
        _selectedCalendarDate = today;
        OnPropertyChanged(nameof(CalendarMonth));
        OnPropertyChanged(nameof(CalendarMonthTitle));
        OnPropertyChanged(nameof(SelectedCalendarDate));
        OnPropertyChanged(nameof(SelectedCalendarDateDisplay));
        _board.Refresh();
        _ = LoadSelectedWorkHoursAsync();
        ObserveDesignConditionLoad(LoadDesignConditionCalendarAsync());
    }

    private void SelectCalendarDate(object? parameter)
    {
        var date = CalendarDateFromParameter(parameter);

        if (date is null)
        {
            return;
        }

        SelectCalendarDate(date.Value);
    }

    private void SelectCalendarDate(DateTime date)
    {
        var targetDate = date.Date;
        var targetMonth = new DateTime(targetDate.Year, targetDate.Month, 1);
        var monthChanged = targetMonth != CalendarMonth;

        _selectedCalendarDate = targetDate;
        OnPropertyChanged(nameof(SelectedCalendarDate));
        OnPropertyChanged(nameof(SelectedCalendarDateDisplay));

        if (monthChanged)
        {
            _calendarMonth = targetMonth;
            OnPropertyChanged(nameof(CalendarMonth));
            OnPropertyChanged(nameof(CalendarMonthTitle));
            _board.Refresh();
            _ = LoadSelectedWorkHoursAsync();
            ObserveDesignConditionLoad(LoadDesignConditionCalendarAsync());
            return;
        }

        RefreshCalendarSelection();
        _ = LoadSelectedWorkHoursAsync();
        ObserveDesignConditionLoad(_designConditions.LoadSelectedDateAsync(targetDate));
    }

    private void SetCalendarViewMode(object? parameter)
    {
        if (parameter is not string mode || mode is not ("Month" or "List"))
        {
            return;
        }

        CalendarViewMode = mode;
    }

    private async Task CreateTaskForCalendarDateAsync(object? parameter)
    {
        var date = CalendarDateFromParameter(parameter) ?? SelectedCalendarDate;
        SelectCalendarDate(date);
        await _board.CreateTaskForCalendarDateAsync(date);
    }

    private async Task MoveTaskToCalendarDateAsync(object? parameter)
    {
        if (parameter is not CalendarTaskDateDropPayload payload)
        {
            return;
        }

        await _board.MoveTaskToCalendarDateAsync(payload.Task, payload.Date);
        SelectCalendarDate(payload.Date);
    }

    private static DateTime? CalendarDateFromParameter(object? parameter)
    {
        return parameter switch
        {
            DateTime date => date.Date,
            CalendarDayItem day => day.Date,
            _ => null
        };
    }

    private async Task LoadDesignConditionCalendarAsync()
    {
        var gridStart = CalendarMonth.AddDays(-(((int)CalendarMonth.DayOfWeek + 6) % 7));
        await _designConditions.InitializeAsync(gridStart, gridStart.AddDays(41), SelectedCalendarDate);
    }

    private void ObserveDesignConditionLoad(Task operation) => _ = ObserveDesignConditionLoadCoreAsync(operation);

    private async Task ObserveDesignConditionLoadCoreAsync(Task operation)
    {
        try { await operation; }
        catch (Exception ex) { _notifications.Notify($"加载日历设计条件失败：{ex.Message}"); }
    }

    private void ApplyDesignConditionSummaries()
    {
        foreach (var day in CalendarDays)
        {
            var summary = _designConditions.GetSummary(day.Date);
            day.DesignConditionCount = summary?.RecordCount ?? 0;
            day.DesignConditionDrawingCount = summary?.DrawingCount ?? 0;
        }
    }
}
