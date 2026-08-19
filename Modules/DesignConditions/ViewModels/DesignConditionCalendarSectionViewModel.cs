using System.Collections.ObjectModel;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Modules.DesignConditions.ViewModels;

public sealed class DesignConditionCalendarSectionViewModel : ObservableObject
{
    private readonly DesignConditionViewModel _module;
    private readonly NotificationService _notifications;
    private readonly Dictionary<DateTime, DesignConditionCalendarSummary> _summaries = [];
    private int _rangeVersion;
    private int _dateVersion;

    public DesignConditionCalendarSectionViewModel(DesignConditionViewModel module, NotificationService notifications)
    {
        _module = module;
        _notifications = notifications;
        _module.DataChanged += (_, _) => _ = RefreshSafelyAsync();
        AddCommand = new RelayCommand(AddAsync);
        OpenCommand = new RelayCommand(Open);
    }

    public event EventHandler? SummariesChanged;
    public ObservableCollection<DesignConditionEntry> SelectedEntries { get; } = new();
    public RelayCommand AddCommand { get; }
    public RelayCommand OpenCommand { get; }
    public DateTime SelectedDate { get; private set; } = DateTime.Today;
    public DateTime RangeStart { get; private set; }
    public DateTime RangeEnd { get; private set; }
    public int SelectedRecordCount => SelectedEntries.Count;
    public int SelectedDrawingCount => SelectedEntries.Sum(item => item.DrawingCount);
    public bool HasSelectedEntries => SelectedEntries.Count > 0;

    public DesignConditionCalendarSummary? GetSummary(DateTime date) => _summaries.GetValueOrDefault(date.Date);

    public async Task InitializeAsync(DateTime rangeStart, DateTime rangeEnd, DateTime selectedDate)
    {
        await LoadRangeAsync(rangeStart, rangeEnd);
        await LoadSelectedDateAsync(selectedDate);
    }

    public async Task LoadRangeAsync(DateTime start, DateTime end)
    {
        var version = ++_rangeVersion;
        RangeStart = start.Date;
        RangeEnd = end.Date;
        var summaries = await _module.GetCalendarSummariesAsync(RangeStart, RangeEnd);
        if (version != _rangeVersion) return;
        _summaries.Clear();
        foreach (var summary in summaries) _summaries[summary.IssuedDate.Date] = summary;
        SummariesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task LoadSelectedDateAsync(DateTime date)
    {
        var version = ++_dateVersion;
        SelectedDate = date.Date;
        var entries = await _module.GetByIssuedDateAsync(SelectedDate);
        if (version != _dateVersion || SelectedDate != date.Date) return;
        CollectionHelper.Replace(SelectedEntries, entries.OrderBy(item => item.ProjectNumber).ThenBy(item => item.ConditionName));
        OnPropertyChanged(nameof(SelectedRecordCount));
        OnPropertyChanged(nameof(SelectedDrawingCount));
        OnPropertyChanged(nameof(HasSelectedEntries));
    }

    private async Task RefreshAsync()
    {
        if (RangeEnd >= RangeStart) await LoadRangeAsync(RangeStart, RangeEnd);
        await LoadSelectedDateAsync(SelectedDate);
    }

    private async Task RefreshSafelyAsync()
    {
        try { await RefreshAsync(); }
        catch (Exception ex) { _notifications.Notify($"刷新日历设计条件失败：{ex.Message}"); }
    }

    private Task AddAsync(object? _) => _module.CreateForDateAsync(SelectedDate);

    private void Open(object? parameter)
    {
        if (parameter is DesignConditionEntry entry && _module.OpenCommand.CanExecute(entry)) _module.OpenCommand.Execute(entry);
    }
}
