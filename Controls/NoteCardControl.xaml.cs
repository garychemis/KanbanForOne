using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using KanbanForOne.Models;

namespace KanbanForOne.Controls;

public partial class NoteCardControl : UserControl
{
    public static readonly DependencyProperty OpenCommandProperty = DependencyProperty.Register(
        nameof(OpenCommand),
        typeof(ICommand),
        typeof(NoteCardControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty AttachFilesCommandProperty = DependencyProperty.Register(
        nameof(AttachFilesCommand),
        typeof(ICommand),
        typeof(NoteCardControl),
        new PropertyMetadata(null));

    private Point _dragStartPoint;
    private bool _isDraggingCard;

    public NoteCardControl()
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
        if (e.LeftButton != MouseButtonState.Pressed || DataContext is not NoteItem note)
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
        data.SetData(DragDropFormats.NoteCard, note);
        DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingCard || DataContext is not NoteItem note)
        {
            _isDraggingCard = false;
            return;
        }

        if (OpenCommand?.CanExecute(note) == true)
        {
            OpenCommand.Execute(note);
        }
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        LiftTransform.Y = -2;
        CardBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#E0CE76")!;
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
        if (DataContext is NoteItem note && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = ((string[])e.Data.GetData(DataFormats.FileDrop)).Where(File.Exists).ToArray();
            var payload = new FileDropPayload(note, files);

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
        CardBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#D6B63F")!;
        e.Handled = true;
    }

    private void ResetVisualState()
    {
        LiftTransform.Y = 0;
        CardBorder.BorderBrush = (Brush)FindResource("NoteCardBorderBrush");
        CardBorder.Effect = new DropShadowEffect
        {
            BlurRadius = 12,
            ShadowDepth = 4,
            Direction = 270,
            Opacity = 0.05
        };
    }
}
