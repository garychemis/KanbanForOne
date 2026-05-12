using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using KanbanForOne.Models;

namespace KanbanForOne.Controls;

public partial class TaskCardControl : UserControl
{
    public static readonly DependencyProperty OpenCommandProperty = DependencyProperty.Register(
        nameof(OpenCommand),
        typeof(ICommand),
        typeof(TaskCardControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty AttachFilesCommandProperty = DependencyProperty.Register(
        nameof(AttachFilesCommand),
        typeof(ICommand),
        typeof(TaskCardControl),
        new PropertyMetadata(null));

    private Point _dragStartPoint;
    private bool _isDraggingCard;

    public TaskCardControl()
    {
        InitializeComponent();
    }

    public ICommand? OpenCommand
    {
        get => (ICommand?)GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    public ICommand? AttachFilesCommand
    {
        get => (ICommand?)GetValue(AttachFilesCommandProperty);
        set => SetValue(AttachFilesCommandProperty, value);
    }

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
        _isDraggingCard = false;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || DataContext is not TaskItem task)
        {
            return;
        }

        var position = e.GetPosition(this);
        var movedFarEnough = Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance;

        if (!movedFarEnough)
        {
            return;
        }

        _isDraggingCard = true;
        var data = new DataObject();
        data.SetData(DragDropFormats.TaskCard, task);
        DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingCard || DataContext is not TaskItem task)
        {
            _isDraggingCard = false;
            return;
        }

        if (OpenCommand?.CanExecute(task) == true)
        {
            OpenCommand.Execute(task);
        }
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        LiftTransform.Y = -2;
        CardBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#CDD3DA")!;
        CardBorder.Effect = new DropShadowEffect
        {
            BlurRadius = 16,
            ShadowDepth = 6,
            Direction = 270,
            Opacity = 0.08
        };
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        ResetVisualState();
    }

    private void OnFileDragEnter(object sender, DragEventArgs e)
    {
        UpdateFileDragState(e);
    }

    private void OnFileDragOver(object sender, DragEventArgs e)
    {
        UpdateFileDragState(e);
    }

    private void OnFileDragLeave(object sender, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
        ResetVisualState();
    }

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (DataContext is TaskItem task && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = ((string[])e.Data.GetData(DataFormats.FileDrop)).Where(File.Exists).ToArray();
            var payload = new FileDropPayload(task, files);

            if (AttachFilesCommand?.CanExecute(payload) == true)
            {
                AttachFilesCommand.Execute(payload);
            }
        }

        DropHint.Visibility = Visibility.Collapsed;
        ResetVisualState();
        e.Handled = true;
    }

    private void UpdateFileDragState(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        e.Effects = DragDropEffects.Copy;
        DropHint.Visibility = Visibility.Visible;
        LiftTransform.Y = -2;
        CardBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#3B82F6")!;
        e.Handled = true;
    }

    private void ResetVisualState()
    {
        LiftTransform.Y = 0;
        CardBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#E6E8EB")!;
        CardBorder.Effect = new DropShadowEffect
        {
            BlurRadius = 12,
            ShadowDepth = 4,
            Direction = 270,
            Opacity = 0.05
        };
    }
}
