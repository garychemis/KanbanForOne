using KanbanForOne.Services;

namespace KanbanForOne.Models;

public sealed record WorkHourSummaryItem(
    string ProjectNumber,
    string Discipline,
    string WorkActivity,
    int TotalHourUnits,
    int EntryCount)
{
    public decimal TotalHours => WorkHourValueConverter.FromUnits(TotalHourUnits);

    public string TotalHoursDisplay => $"{WorkHourValueConverter.FormatHours(TotalHourUnits)} 小时";

    public string CombinationDisplay => $"{ProjectNumber} / {Discipline} / {WorkActivity}";
}
