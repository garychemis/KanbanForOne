namespace KanbanForOne.ViewModels;

/// <summary>
/// 全局搜索、日期过滤与当前视图的共享状态。
/// 主窗口持有此实例并订阅其 PropertyChanged，负责在变化时编排各视图的刷新。
/// </summary>
public sealed class WorkspaceFilterState : ObservableObject
{
    private string _searchText = string.Empty;
    private string _normalizedSearchText = string.Empty;
    private DateTime? _dateFilterStart;
    private DateTime? _dateFilterEnd;
    private string _currentFilter = "Board";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _normalizedSearchText = _searchText.Trim();
                OnPropertyChanged(nameof(NormalizedSearchText));
            }
        }
    }

    public string NormalizedSearchText => _normalizedSearchText;

    public DateTime? DateFilterStart
    {
        get => _dateFilterStart;
        set => SetProperty(ref _dateFilterStart, value?.Date);
    }

    public DateTime? DateFilterEnd
    {
        get => _dateFilterEnd;
        set => SetProperty(ref _dateFilterEnd, value?.Date);
    }

    public bool HasDateFilter => DateFilterStart.HasValue || DateFilterEnd.HasValue;

    public string CurrentFilter
    {
        get => _currentFilter;
        set
        {
            if (SetProperty(ref _currentFilter, value))
            {
                OnPropertyChanged(nameof(IsArchiveFilter));
            }
        }
    }

    public bool IsArchiveFilter => _currentFilter == "Archived";
}
