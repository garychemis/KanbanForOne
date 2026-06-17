using KanbanForOne.Models;
using TaskStatus = KanbanForOne.Models.TaskStatus;

namespace KanbanForOne.Services;

public static class TaskOverdueService
{
    public static bool IsOverdue(TaskItem task, DateTime today)
    {
        if (task.IsArchived || task.EndDate is null || !IsUnfinishedStatus(task.Status))
        {
            return false;
        }

        var rangeEnd = task.StartDate is null
            ? task.EndDate.Value.Date
            : Max(task.StartDate.Value.Date, task.EndDate.Value.Date);

        return today.Date > rangeEnd;
    }

    public static bool IsUnfinishedStatus(TaskStatus status)
    {
        return status is TaskStatus.Todo or TaskStatus.Doing or TaskStatus.Blocked;
    }

    private static DateTime Max(DateTime first, DateTime second)
    {
        return first >= second ? first : second;
    }
}
