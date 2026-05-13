using System.Collections.ObjectModel;
using System.Globalization;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Models;

public sealed class CalendarDayItem : ObservableObject
{
    private bool _isSelected;
    private int _totalTaskCount;
    private int _overflowCount;

    public DateTime Date { get; init; }

    public bool IsToday { get; init; }

    public bool IsCurrentMonth { get; init; }

    public string DayNumber => Date.Day.ToString(CultureInfo.InvariantCulture);

    public ObservableCollection<CalendarTaskChip> VisibleTaskChips { get; } = new();

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public int TotalTaskCount
    {
        get => _totalTaskCount;
        set
        {
            if (SetProperty(ref _totalTaskCount, value))
            {
                OnPropertyChanged(nameof(HasTasks));
            }
        }
    }

    public bool HasTasks => TotalTaskCount > 0;

    public int OverflowCount
    {
        get => _overflowCount;
        set
        {
            if (SetProperty(ref _overflowCount, value))
            {
                OnPropertyChanged(nameof(HasOverflow));
                OnPropertyChanged(nameof(OverflowText));
            }
        }
    }

    public bool HasOverflow => OverflowCount > 0;

    public string OverflowText => $"+{OverflowCount}";
}

public sealed class CalendarTaskChip
{
    public required TaskItem Task { get; init; }

    public DateTime Date { get; init; }

    public bool IsMultiDay { get; init; }

    public bool StartsOnDate { get; init; }

    public bool EndsOnDate { get; init; }

    public string Title => Task.Title;

    public TaskStatus Status => Task.Status;

    public TaskPriority Priority => Task.Priority;

    public string RangeMarker
    {
        get
        {
            if (!IsMultiDay)
            {
                return string.Empty;
            }

            return (StartsOnDate, EndsOnDate) switch
            {
                (true, false) => ">",
                (false, true) => "<",
                _ => "--"
            };
        }
    }
}
