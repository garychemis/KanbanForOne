using KanbanForOne.Models;
using KanbanForOne.Services;
using TaskStatus = KanbanForOne.Models.TaskStatus;

namespace KanbanForOne.Tests;

public sealed class TaskOverdueServiceTests
{
    private static readonly DateTime Today = new(2026, 6, 17);

    [Theory]
    [InlineData(TaskStatus.Todo)]
    [InlineData(TaskStatus.Doing)]
    [InlineData(TaskStatus.Blocked)]
    public void IsOverdue_returns_true_for_unfinished_task_after_end_date(TaskStatus status)
    {
        var task = new TaskItem
        {
            Status = status,
            StartDate = Today.AddDays(-7),
            EndDate = Today.AddDays(-1)
        };

        var isOverdue = TaskOverdueService.IsOverdue(task, Today);

        Assert.True(isOverdue);
    }

    [Fact]
    public void IsOverdue_returns_false_when_today_is_inside_date_range()
    {
        var task = new TaskItem
        {
            Status = TaskStatus.Doing,
            StartDate = Today.AddDays(-1),
            EndDate = Today.AddDays(1)
        };

        var isOverdue = TaskOverdueService.IsOverdue(task, Today);

        Assert.False(isOverdue);
    }

    [Theory]
    [InlineData(TaskStatus.Done)]
    public void IsOverdue_returns_false_for_completed_tasks(TaskStatus status)
    {
        var task = new TaskItem
        {
            Status = status,
            StartDate = Today.AddDays(-7),
            EndDate = Today.AddDays(-1)
        };

        var isOverdue = TaskOverdueService.IsOverdue(task, Today);

        Assert.False(isOverdue);
    }

    [Fact]
    public void IsOverdue_returns_false_for_tasks_without_an_end_date()
    {
        var task = new TaskItem
        {
            Status = TaskStatus.Todo,
            StartDate = Today.AddDays(-7),
            EndDate = null
        };

        var isOverdue = TaskOverdueService.IsOverdue(task, Today);

        Assert.False(isOverdue);
    }

    [Fact]
    public void IsOverdue_returns_false_for_archived_tasks()
    {
        var task = new TaskItem
        {
            Status = TaskStatus.Todo,
            StartDate = Today.AddDays(-7),
            EndDate = Today.AddDays(-1),
            IsArchived = true
        };

        var isOverdue = TaskOverdueService.IsOverdue(task, Today);

        Assert.False(isOverdue);
    }

    [Fact]
    public void IsOverdue_uses_normalized_date_range_when_dates_are_reversed()
    {
        var task = new TaskItem
        {
            Status = TaskStatus.Todo,
            StartDate = Today.AddDays(7),
            EndDate = Today.AddDays(-1)
        };

        var isOverdue = TaskOverdueService.IsOverdue(task, Today);

        Assert.False(isOverdue);
    }

    [Fact]
    public void TaskItem_IsOverdue_uses_current_date_without_stored_state()
    {
        var task = new TaskItem
        {
            Status = TaskStatus.Todo,
            StartDate = DateTime.Today.AddDays(-7),
            EndDate = DateTime.Today.AddDays(-1)
        };

        Assert.True(task.IsOverdue);
    }

    [Fact]
    public void TaskItem_notifies_IsOverdue_when_status_or_end_date_changes()
    {
        var task = new TaskItem
        {
            Status = TaskStatus.Todo,
            EndDate = DateTime.Today.AddDays(1)
        };
        var changedProperties = new List<string?>();
        task.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        task.EndDate = DateTime.Today.AddDays(-1);
        task.Status = TaskStatus.Done;

        Assert.Contains(nameof(TaskItem.IsOverdue), changedProperties);
    }
}
