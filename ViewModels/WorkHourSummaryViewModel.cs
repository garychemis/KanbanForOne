using System.Collections.ObjectModel;
using KanbanForOne.Models;
using KanbanForOne.Services;
using Microsoft.Win32;

namespace KanbanForOne.ViewModels;

public sealed class WorkHourSummaryViewModel : ObservableObject
{
    private readonly WorkHourRepository _repository;
    private readonly WorkHourExportService _exportService;
    private readonly Action<string> _notify;
    private bool _isMonthMode = true;
    private int _selectedYear = DateTime.Today.Year;
    private int _selectedMonth = DateTime.Today.Month;
    private DateTime? _rangeStartDate;
    private DateTime? _rangeEndDate;
    private DateTime _currentStartDate;
    private DateTime _currentEndDate;
    private bool _isLoading;
    private bool _isLoaded;
    private bool _isQueryDirty = true;
    private int _queryVersion;
    private IReadOnlyList<WorkHourSummaryItem> _allSummaries = [];
    private IReadOnlyList<string> _projectFilterOptions = [string.Empty];
    private IReadOnlyList<string> _disciplineFilterOptions = [string.Empty];
    private IReadOnlyList<string> _workActivityFilterOptions = [string.Empty];
    private string _selectedProjectFilter = string.Empty;
    private string _selectedDisciplineFilter = string.Empty;
    private string _selectedWorkActivityFilter = string.Empty;
    private bool _isRefreshingDimensionFilters;

    public WorkHourSummaryViewModel(
        WorkHourRepository repository,
        WorkHourExportService exportService,
        Action<string> notify)
    {
        _repository = repository;
        _exportService = exportService;
        _notify = notify;

        var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _rangeStartDate = firstOfMonth;
        _rangeEndDate = firstOfMonth.AddMonths(1).AddDays(-1);

        SetQueryModeCommand = new RelayCommand(SetQueryMode);
        PreviousMonthCommand = new RelayCommand(PreviousMonthAsync);
        NextMonthCommand = new RelayCommand(NextMonthAsync);
        GoToCurrentMonthCommand = new RelayCommand(GoToCurrentMonthAsync);
        RefreshCommand = new RelayCommand(RefreshAsync, _ => CanRefresh);
        ExportCommand = new RelayCommand(ExportAsync, _ => CanExport);
    }

    public ObservableCollection<WorkHourSummaryItem> Summaries { get; } = new();

    public IReadOnlyList<string> ProjectFilterOptions
    {
        get => _projectFilterOptions;
        private set => SetProperty(ref _projectFilterOptions, value);
    }

    public IReadOnlyList<string> DisciplineFilterOptions
    {
        get => _disciplineFilterOptions;
        private set => SetProperty(ref _disciplineFilterOptions, value);
    }

    public IReadOnlyList<string> WorkActivityFilterOptions
    {
        get => _workActivityFilterOptions;
        private set => SetProperty(ref _workActivityFilterOptions, value);
    }

    public IReadOnlyList<int> YearOptions { get; } = Enumerable.Range(1990, 111).ToArray();

    public IReadOnlyList<int> MonthOptions { get; } = Enumerable.Range(1, 12).ToArray();

    public RelayCommand SetQueryModeCommand { get; }

    public RelayCommand PreviousMonthCommand { get; }

    public RelayCommand NextMonthCommand { get; }

    public RelayCommand GoToCurrentMonthCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ExportCommand { get; }

    public string SelectedProjectFilter
    {
        get => _selectedProjectFilter;
        set => SetDimensionFilter(ref _selectedProjectFilter, value, nameof(SelectedProjectFilter));
    }

    public string SelectedDisciplineFilter
    {
        get => _selectedDisciplineFilter;
        set => SetDimensionFilter(ref _selectedDisciplineFilter, value, nameof(SelectedDisciplineFilter));
    }

    public string SelectedWorkActivityFilter
    {
        get => _selectedWorkActivityFilter;
        set => SetDimensionFilter(ref _selectedWorkActivityFilter, value, nameof(SelectedWorkActivityFilter));
    }

    public bool IsMonthMode
    {
        get => _isMonthMode;
        private set
        {
            if (SetProperty(ref _isMonthMode, value))
            {
                OnPropertyChanged(nameof(IsRangeMode));
                MarkQueryDirty();
            }
        }
    }

    public bool IsRangeMode => !IsMonthMode;

    public int SelectedYear
    {
        get => _selectedYear;
        set
        {
            if (SetProperty(ref _selectedYear, value))
            {
                MarkQueryDirty();
                RefreshSelectedMonth();
            }
        }
    }

    public int SelectedMonth
    {
        get => _selectedMonth;
        set
        {
            var normalized = Math.Clamp(value, 1, 12);
            if (SetProperty(ref _selectedMonth, normalized))
            {
                MarkQueryDirty();
                RefreshSelectedMonth();
            }
        }
    }

    public DateTime? RangeStartDate
    {
        get => _rangeStartDate;
        set
        {
            if (SetProperty(ref _rangeStartDate, value?.Date))
            {
                MarkQueryDirty();
            }
        }
    }

    public DateTime? RangeEndDate
    {
        get => _rangeEndDate;
        set
        {
            if (SetProperty(ref _rangeEndDate, value?.Date))
            {
                MarkQueryDirty();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(CanRefresh));
                OnPropertyChanged(nameof(CanExport));
                OnPropertyChanged(nameof(CanUseDimensionFilters));
                RefreshCommand.RaiseCanExecuteChanged();
                ExportCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSummaries => Summaries.Count > 0;

    public bool HasUnfilteredSummaries => _allSummaries.Count > 0;

    public bool IsEmpty => !IsLoading && !HasSummaries;

    public bool HasActiveDimensionFilters => !string.IsNullOrEmpty(_selectedProjectFilter)
                                             || !string.IsNullOrEmpty(_selectedDisciplineFilter)
                                             || !string.IsNullOrEmpty(_selectedWorkActivityFilter);

    public bool CanUseDimensionFilters => !IsLoading && HasUnfilteredSummaries;

    public string EmptyStateTitle => HasUnfilteredSummaries && HasActiveDimensionFilters
        ? "当前筛选条件下没有匹配的人工时"
        : "当前范围没有人工时";

    public string EmptyStateDescription => HasUnfilteredSummaries && HasActiveDimensionFilters
        ? "将一个或多个筛选下拉框恢复为空白。"
        : "调整时间范围，然后重新应用条件。";

    public bool CanRefresh => !IsLoading && TryGetSelectedRange(out _, out _);

    public bool CanExport => !IsLoading && HasSummaries && !_isQueryDirty;

    public int TotalHourUnits => Summaries.Sum(item => item.TotalHourUnits);

    public string TotalHoursValueDisplay => WorkHourValueConverter.FormatHours(TotalHourUnits);

    public string TotalHoursDisplay => $"{WorkHourValueConverter.FormatHours(TotalHourUnits)} 小时";

    public int TotalEntryCount => Summaries.Sum(item => item.EntryCount);

    public int CombinationCount => Summaries.Count;

    public int MaxSummaryHourUnits => Math.Max(1, Summaries.Select(item => item.TotalHourUnits).DefaultIfEmpty(1).Max());

    public string CurrentDateRangeDisplay => _isLoaded
        ? $"{_currentStartDate:yyyy.MM.dd}~{_currentEndDate:yyyy.MM.dd}"
        : "尚未查询";

    public string CurrentRangeQueryDisplay
    {
        get
        {
            if (!_isLoaded)
            {
                return "当前汇总区间：尚未查询";
            }

            var pendingHint = _isQueryDirty ? " · 新区间尚未应用，点击应用后更新" : string.Empty;
            return $"当前汇总区间：{_currentStartDate:yyyy.MM.dd} — {_currentEndDate:yyyy.MM.dd}{pendingHint}";
        }
    }

    public async Task EnsureLoadedAsync()
    {
        if (!_isLoaded || _isQueryDirty)
        {
            await RefreshAsync(null);
        }
    }

    public void Invalidate()
    {
        MarkQueryDirty();
    }

    private void SetQueryMode(object? parameter)
    {
        if (parameter is not string mode || mode is not ("Month" or "Range"))
        {
            return;
        }

        var useMonthMode = mode == "Month";
        if (IsMonthMode == useMonthMode)
        {
            return;
        }

        IsMonthMode = useMonthMode;
        OnPropertyChanged(nameof(CurrentRangeQueryDisplay));
        if (useMonthMode)
        {
            _ = RefreshAsync(null);
        }
    }

    private void RefreshSelectedMonth()
    {
        if (IsMonthMode)
        {
            _ = RefreshAsync(null);
        }
    }

    private async Task PreviousMonthAsync(object? _)
    {
        SetSelectedMonth(new DateTime(SelectedYear, SelectedMonth, 1).AddMonths(-1));
        await RefreshAsync(null);
    }

    private async Task NextMonthAsync(object? _)
    {
        SetSelectedMonth(new DateTime(SelectedYear, SelectedMonth, 1).AddMonths(1));
        await RefreshAsync(null);
    }

    private async Task GoToCurrentMonthAsync(object? _)
    {
        SetSelectedMonth(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
        await RefreshAsync(null);
    }

    private void SetSelectedMonth(DateTime month)
    {
        _selectedYear = month.Year;
        _selectedMonth = month.Month;
        OnPropertyChanged(nameof(SelectedYear));
        OnPropertyChanged(nameof(SelectedMonth));
        MarkQueryDirty();
    }

    private async Task RefreshAsync(object? _)
    {
        if (!TryGetSelectedRange(out var startDate, out var endDate))
        {
            _notify("请选择有效的人工时汇总日期范围");
            return;
        }

        var version = ++_queryVersion;
        try
        {
            IsLoading = true;
            var summaries = await _repository.GetSummaryAsync(startDate, endDate);
            if (version != _queryVersion)
            {
                return;
            }

            _allSummaries = summaries;
            RefreshDimensionFilterOptions();
            ApplyDimensionFilters();
            _currentStartDate = startDate;
            _currentEndDate = endDate;
            _isLoaded = true;
            _isQueryDirty = false;
            NotifyQueryStateChanged();
        }
        catch (Exception ex)
        {
            _notify($"加载人工时汇总失败：{ex.Message}");
        }
        finally
        {
            if (version == _queryVersion)
            {
                IsLoading = false;
            }
        }
    }

    private async Task ExportAsync(object? _)
    {
        if (!CanExport)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".xlsx",
            Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
            FileName = $"人工时汇总_{_currentStartDate:yyyyMMdd}-{_currentEndDate:yyyyMMdd}.xlsx",
            Title = "导出人工时汇总"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            IsLoading = true;
            var summaries = Summaries.ToArray();
            var startDate = _currentStartDate;
            var endDate = _currentEndDate;
            var projectFilter = _selectedProjectFilter;
            var disciplineFilter = _selectedDisciplineFilter;
            var workActivityFilter = _selectedWorkActivityFilter;
            var details = (await _repository.GetByDateRangeAsync(startDate, endDate))
                .Where(entry => MatchesDimensionFilters(
                    entry.ProjectNumber,
                    entry.Discipline,
                    entry.WorkActivity,
                    projectFilter,
                    disciplineFilter,
                    workActivityFilter))
                .ToArray();
            await _exportService.ExportAsync(
                dialog.FileName,
                startDate,
                endDate,
                summaries,
                details);
            _notify($"人工时汇总已导出：{dialog.FileName}");
        }
        catch (Exception ex)
        {
            _notify($"导出人工时汇总失败：{ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool TryGetSelectedRange(out DateTime startDate, out DateTime endDate)
    {
        if (IsMonthMode)
        {
            startDate = new DateTime(SelectedYear, SelectedMonth, 1);
            endDate = startDate.AddMonths(1).AddDays(-1);
            return true;
        }

        startDate = RangeStartDate?.Date ?? default;
        endDate = RangeEndDate?.Date ?? default;
        return RangeStartDate.HasValue && RangeEndDate.HasValue && startDate <= endDate;
    }

    private void MarkQueryDirty()
    {
        if (!_isQueryDirty)
        {
            _isQueryDirty = true;
        }

        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CurrentRangeQueryDisplay));
        RefreshCommand.RaiseCanExecuteChanged();
        ExportCommand.RaiseCanExecuteChanged();
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(HasSummaries));
        OnPropertyChanged(nameof(HasUnfilteredSummaries));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasActiveDimensionFilters));
        OnPropertyChanged(nameof(CanUseDimensionFilters));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(TotalHourUnits));
        OnPropertyChanged(nameof(TotalHoursValueDisplay));
        OnPropertyChanged(nameof(TotalHoursDisplay));
        OnPropertyChanged(nameof(TotalEntryCount));
        OnPropertyChanged(nameof(CombinationCount));
        OnPropertyChanged(nameof(MaxSummaryHourUnits));
        OnPropertyChanged(nameof(CurrentDateRangeDisplay));
        OnPropertyChanged(nameof(CurrentRangeQueryDisplay));
        ExportCommand.RaiseCanExecuteChanged();
    }

    private void NotifyQueryStateChanged()
    {
        OnPropertyChanged(nameof(HasUnfilteredSummaries));
        OnPropertyChanged(nameof(CanUseDimensionFilters));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CurrentDateRangeDisplay));
        OnPropertyChanged(nameof(CurrentRangeQueryDisplay));
        ExportCommand.RaiseCanExecuteChanged();
    }

    private void SetDimensionFilter(ref string field, string? value, string propertyName)
    {
        if (_isRefreshingDimensionFilters)
        {
            return;
        }

        var normalized = value?.Trim() ?? string.Empty;
        if (!SetProperty(ref field, normalized, propertyName))
        {
            return;
        }

        RefreshDimensionFilterOptions();
        ApplyDimensionFilters();
    }

    private void RefreshDimensionFilterOptions()
    {
        _isRefreshingDimensionFilters = true;
        try
        {
            var attempt = 0;
            bool selectionChanged;
            IReadOnlyList<string> projectOptions;
            IReadOnlyList<string> disciplineOptions;
            IReadOnlyList<string> workActivityOptions;
            do
            {
                projectOptions = BuildDimensionFilterOptions(_allSummaries
                        .Where(item => MatchesDimension(item.Discipline, _selectedDisciplineFilter)
                                       && MatchesDimension(item.WorkActivity, _selectedWorkActivityFilter))
                        .Select(item => item.ProjectNumber));
                disciplineOptions = BuildDimensionFilterOptions(_allSummaries
                        .Where(item => MatchesDimension(item.ProjectNumber, _selectedProjectFilter)
                                       && MatchesDimension(item.WorkActivity, _selectedWorkActivityFilter))
                        .Select(item => item.Discipline));
                workActivityOptions = BuildDimensionFilterOptions(_allSummaries
                        .Where(item => MatchesDimension(item.ProjectNumber, _selectedProjectFilter)
                                       && MatchesDimension(item.Discipline, _selectedDisciplineFilter))
                        .Select(item => item.WorkActivity));

                selectionChanged = RestoreDimensionFilter(
                    ref _selectedProjectFilter,
                    projectOptions);
                selectionChanged |= RestoreDimensionFilter(
                    ref _selectedDisciplineFilter,
                    disciplineOptions);
                selectionChanged |= RestoreDimensionFilter(
                    ref _selectedWorkActivityFilter,
                    workActivityOptions);
                attempt++;
            } while (selectionChanged && attempt < 3);

            ProjectFilterOptions = projectOptions;
            DisciplineFilterOptions = disciplineOptions;
            WorkActivityFilterOptions = workActivityOptions;
        }
        finally
        {
            _isRefreshingDimensionFilters = false;
        }

        OnPropertyChanged(nameof(SelectedProjectFilter));
        OnPropertyChanged(nameof(SelectedDisciplineFilter));
        OnPropertyChanged(nameof(SelectedWorkActivityFilter));
    }

    private static bool RestoreDimensionFilter(ref string field, IReadOnlyList<string> options)
    {
        var current = field;
        var restored = options.FirstOrDefault(option => string.Equals(option, current, StringComparison.OrdinalIgnoreCase))
                       ?? string.Empty;
        if (string.Equals(field, restored, StringComparison.Ordinal))
        {
            return false;
        }

        field = restored;
        return true;
    }

    private void ApplyDimensionFilters()
    {
        var filtered = _allSummaries
            .Where(item => MatchesDimensionFilters(
                item.ProjectNumber,
                item.Discipline,
                item.WorkActivity,
                _selectedProjectFilter,
                _selectedDisciplineFilter,
                _selectedWorkActivityFilter))
            .ToArray();
        Replace(Summaries, filtered);
        NotifySummaryChanged();
    }

    private static IReadOnlyList<string> BuildDimensionFilterOptions(IEnumerable<string> values)
    {
        return [
            string.Empty,
            .. values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static bool MatchesDimensionFilters(
        string projectNumber,
        string discipline,
        string workActivity,
        string projectFilter,
        string disciplineFilter,
        string workActivityFilter)
    {
        return MatchesDimension(projectNumber, projectFilter)
               && MatchesDimension(discipline, disciplineFilter)
               && MatchesDimension(workActivity, workActivityFilter);
    }

    private static bool MatchesDimension(string value, string filter)
    {
        return string.IsNullOrEmpty(filter)
               || string.Equals(value, filter, StringComparison.OrdinalIgnoreCase);
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
