using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
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
    private AdornerLayer? _dragGhostLayer;
    private DragGhostAdorner? _dragGhostAdorner;

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
        if (_isDraggingCard)
        {
            return;
        }

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

        BeginDragGhost(e.GetPosition(CardBorder));
        ApplyDraggingVisualState();
        FlushDragVisualState();
        GiveFeedback += OnDragGiveFeedback;

        try
        {
            DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
        }
        finally
        {
            GiveFeedback -= OnDragGiveFeedback;
            RemoveDragGhost();
            CardDragDropSession.NotifyEnded();
            ResetVisualState();
        }
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
        if (_isDraggingCard)
        {
            return;
        }

        Cursor = Cursors.Hand;
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
        Cursor = Cursors.Hand;
        DragScaleTransform.ScaleX = 1;
        DragScaleTransform.ScaleY = 1;
        DragRotateTransform.Angle = 0;
        LiftTransform.Y = 0;
        CardBorder.Opacity = 1;
        CardContent.Opacity = 1;
        CardContent.Effect = null;
        CardBorder.BorderBrush = (Brush)FindResource("NoteCardBorderBrush");
        CardBorder.Effect = new DropShadowEffect
        {
            BlurRadius = 12,
            ShadowDepth = 4,
            Direction = 270,
            Opacity = 0.05
        };
    }

    private void ApplyDraggingVisualState()
    {
        Cursor = Cursors.SizeAll;
        DragScaleTransform.ScaleX = 1;
        DragScaleTransform.ScaleY = 1;
        DragRotateTransform.Angle = 0;
        LiftTransform.Y = 0;
        CardBorder.Opacity = 0.36;
        CardContent.Opacity = 1;
        CardContent.Effect = null;
        CardBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#C3A737")!;
        CardBorder.Effect = new DropShadowEffect
        {
            BlurRadius = 10,
            ShadowDepth = 2,
            Direction = 270,
            Opacity = 0.06
        };
    }

    private void BeginDragGhost(Point cursorOffset)
    {
        RemoveDragGhost();

        var adornedElement = Window.GetWindow(this)?.Content as UIElement ?? this;
        var layer = AdornerLayer.GetAdornerLayer(adornedElement);

        if (layer is null)
        {
            adornedElement = this;
            layer = AdornerLayer.GetAdornerLayer(this);
        }

        if (layer is null || CardBorder.ActualWidth <= 0 || CardBorder.ActualHeight <= 0)
        {
            return;
        }

        _dragGhostLayer = layer;
        _dragGhostAdorner = new DragGhostAdorner(adornedElement, CardBorder, cursorOffset);
        _dragGhostLayer.Add(_dragGhostAdorner);
        UpdateDragGhostPosition();
    }

    private void OnDragGiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        UpdateDragGhostPosition();
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    private void UpdateDragGhostPosition()
    {
        if (_dragGhostAdorner is not null &&
            DragGhostAdorner.TryGetCursorScreenPosition(out var screenPosition))
        {
            _dragGhostAdorner.UpdatePositionFromScreen(screenPosition);
        }
    }

    private void RemoveDragGhost()
    {
        if (_dragGhostLayer is not null && _dragGhostAdorner is not null)
        {
            _dragGhostLayer.Remove(_dragGhostAdorner);
        }

        _dragGhostLayer = null;
        _dragGhostAdorner = null;
    }

    private void FlushDragVisualState()
    {
        UpdateLayout();
        Dispatcher.Invoke(DispatcherPriority.Render, () => { });
    }
}
