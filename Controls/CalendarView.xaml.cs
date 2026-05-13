using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanForOne.Models;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Controls;

public partial class CalendarView : UserControl
{
    private Point _taskDragStartPoint;
    private TaskItem? _pressedTask;
    private bool _isDraggingTask;
    private bool _suppressTaskClick;

    public CalendarView()
    {
        InitializeComponent();
    }

    private void OnDayPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CalendarDayItem day } ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            if (viewModel.CreateTaskForCalendarDateCommand.CanExecute(day))
            {
                viewModel.CreateTaskForCalendarDateCommand.Execute(day);
            }

            e.Handled = true;
            return;
        }

        if (viewModel.SelectCalendarDateCommand.CanExecute(day))
        {
            viewModel.SelectCalendarDateCommand.Execute(day);
        }
    }

    private void OnDayDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDraggedTask(e) is null ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnDayDrop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CalendarDayItem day } ||
            DataContext is not MainWindowViewModel viewModel ||
            GetDraggedTask(e) is not { } task)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var payload = new CalendarTaskDateDropPayload(task, day.Date);

        if (viewModel.MoveTaskToCalendarDateCommand.CanExecute(payload))
        {
            viewModel.MoveTaskToCalendarDateCommand.Execute(payload);
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnTaskPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressedTask = TaskFromDataContext((sender as FrameworkElement)?.DataContext);
        _taskDragStartPoint = e.GetPosition(this);
        _isDraggingTask = false;
        _suppressTaskClick = false;
        e.Handled = true;
    }

    private void OnTaskMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedTask is null || e.LeftButton != MouseButtonState.Pressed || _isDraggingTask)
        {
            return;
        }

        var position = e.GetPosition(this);
        var movedFarEnough = Math.Abs(position.X - _taskDragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(position.Y - _taskDragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance;

        if (!movedFarEnough)
        {
            return;
        }

        _isDraggingTask = true;
        _suppressTaskClick = true;

        var data = new DataObject();
        data.SetData(DragDropFormats.TaskCard, _pressedTask);

        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        }
        finally
        {
            _isDraggingTask = false;
            _pressedTask = null;
        }

        e.Handled = true;
    }

    private void OnTaskMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var task = _pressedTask ?? TaskFromDataContext((sender as FrameworkElement)?.DataContext);
        _pressedTask = null;

        if (_suppressTaskClick)
        {
            _suppressTaskClick = false;
            e.Handled = true;
            return;
        }

        if (task is not null &&
            DataContext is MainWindowViewModel viewModel &&
            viewModel.OpenTaskCommand.CanExecute(task))
        {
            viewModel.OpenTaskCommand.Execute(task);
        }

        e.Handled = true;
    }

    private static TaskItem? GetDraggedTask(DragEventArgs e)
    {
        return e.Data.GetDataPresent(DragDropFormats.TaskCard)
            ? e.Data.GetData(DragDropFormats.TaskCard) as TaskItem
            : null;
    }

    private static TaskItem? TaskFromDataContext(object? dataContext)
    {
        return dataContext switch
        {
            TaskItem task => task,
            CalendarTaskChip chip => chip.Task,
            _ => null
        };
    }
}
