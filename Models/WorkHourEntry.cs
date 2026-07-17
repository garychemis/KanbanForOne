using KanbanForOne.Services;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Models;

public sealed class WorkHourEntry : ObservableObject
{
    private DateTime _workDate = DateTime.Today;
    private string _projectNumber = string.Empty;
    private string _discipline = string.Empty;
    private string _workActivity = string.Empty;
    private int _hourUnits;
    private string _remark = string.Empty;
    private DateTime _createdAt = DateTime.Now;
    private DateTime _updatedAt = DateTime.Now;

    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime WorkDate
    {
        get => _workDate;
        set => SetProperty(ref _workDate, value.Date);
    }

    public string ProjectNumber
    {
        get => _projectNumber;
        set => SetProperty(ref _projectNumber, value);
    }

    public string Discipline
    {
        get => _discipline;
        set => SetProperty(ref _discipline, value);
    }

    public string WorkActivity
    {
        get => _workActivity;
        set => SetProperty(ref _workActivity, value);
    }

    public int HourUnits
    {
        get => _hourUnits;
        set
        {
            if (SetProperty(ref _hourUnits, value))
            {
                OnPropertyChanged(nameof(Hours));
                OnPropertyChanged(nameof(HoursDisplay));
            }
        }
    }

    public decimal Hours => WorkHourValueConverter.FromUnits(HourUnits);

    public string HoursDisplay => $"{WorkHourValueConverter.FormatHours(HourUnits)} 小时";

    public string Remark
    {
        get => _remark;
        set
        {
            if (SetProperty(ref _remark, value))
            {
                OnPropertyChanged(nameof(HasRemark));
            }
        }
    }

    public bool HasRemark => !string.IsNullOrWhiteSpace(Remark);

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set => SetProperty(ref _updatedAt, value);
    }
}
