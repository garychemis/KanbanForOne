using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KanbanForOne.Models;

namespace KanbanForOne.Controls;

public partial class KanbanColumnControl : UserControl
{
    private int _lastDropIndex = -1;
    private double _lastSortReferenceY = double.NaN;
    private readonly List<Rect> _dropContainerBounds = [];
    private int _dropBoundsCount = -1;
    private UIElement? _dropBoundsRelativeTo;
    private INotifyCollectionChanged? _itemsSourceNotifications;

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(KanbanColumnControl),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle),
        typeof(string),
        typeof(KanbanColumnControl),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(KanbanColumnControl),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate),
        typeof(DataTemplate),
        typeof(KanbanColumnControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty ColumnKindProperty = DependencyProperty.Register(
        nameof(ColumnKind),
        typeof(KanbanColumnKind),
        typeof(KanbanColumnControl),
        new PropertyMetadata(KanbanColumnKind.Todo));

    public static readonly DependencyProperty AddCommandProperty = DependencyProperty.Register(
        nameof(AddCommand),
        typeof(ICommand),
        typeof(KanbanColumnControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty AddCommandParameterProperty = DependencyProperty.Register(
        nameof(AddCommandParameter),
        typeof(object),
        typeof(KanbanColumnControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty DropCommandProperty = DependencyProperty.Register(
        nameof(DropCommand),
        typeof(ICommand),
        typeof(KanbanColumnControl),
        new PropertyMetadata(null));

    public KanbanColumnControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateEmptyState();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => InvalidateDropBounds()));
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public KanbanColumnKind ColumnKind
    {
        get => (KanbanColumnKind)GetValue(ColumnKindProperty);
        set => SetValue(ColumnKindProperty, value);
    }

    public ICommand? AddCommand
    {
        get => (ICommand?)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public object? AddCommandParameter
    {
        get => GetValue(AddCommandParameterProperty);
        set => SetValue(AddCommandParameterProperty, value);
    }

    public ICommand? DropCommand
    {
        get => (ICommand?)GetValue(DropCommandProperty);
        set => SetValue(DropCommandProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var control = (KanbanColumnControl)dependencyObject;

        if (control._itemsSourceNotifications is not null)
        {
            control._itemsSourceNotifications.CollectionChanged -= control.OnItemsSourceCollectionChanged;
        }

        control._itemsSourceNotifications = e.NewValue as INotifyCollectionChanged;

        if (control._itemsSourceNotifications is not null)
        {
            control._itemsSourceNotifications.CollectionChanged += control.OnItemsSourceCollectionChanged;
        }

        control.InvalidateDropBounds();
        control.ScheduleEmptyStateUpdate();
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateDropBounds();
        ScheduleEmptyStateUpdate();
    }

    private void ScheduleEmptyStateUpdate()
    {
        if (!IsLoaded)
        {
            return;
        }

        Dispatcher.BeginInvoke((Action)UpdateEmptyState, DispatcherPriority.Loaded);
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        UpdateDragState(e);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        UpdateDragState(e);
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (!IsPointerInsideColumn(e))
        {
            ResetDragState();
        }
    }

    private void OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (GetDraggedCard(e) is not null)
        {
            HandleCardDrop(e);
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        HandleCardDrop(e);
    }

    private void HandleCardDrop(DragEventArgs e)
    {
        var card = GetDraggedCard(e);

        if (card is null)
        {
            ResetDragState();
            return;
        }

        var targetIndex = _lastDropIndex >= 0 ? _lastDropIndex : GetDropIndex(e);
        var payload = new CardDropPayload(card, ColumnKind, targetIndex);

        if (DropCommand?.CanExecute(payload) == true)
        {
            DropCommand.Execute(payload);
        }

        ResetDragState();
        e.Handled = true;
    }

    private void OnAddButtonClick(object sender, RoutedEventArgs e)
    {
        var payload = new CardCreatePayload(ColumnKind, GetCreateAnchorBounds(), GetHostSize());

        if (AddCommand?.CanExecute(payload) == true)
        {
            AddCommand.Execute(payload);
        }

        e.Handled = true;
    }

    private Rect GetCreateAnchorBounds()
    {
        var host = Window.GetWindow(this)?.Content as Visual;

        if (host is null)
        {
            return new Rect(0, 0, ActualWidth, ActualHeight);
        }

        var origin = CardItems.TransformToAncestor(host).Transform(new Point(0, 0));
        var top = origin.Y;

        if (CardItems.Items.Count > 0 &&
            CardItems.ItemContainerGenerator.ContainerFromIndex(CardItems.Items.Count - 1) is FrameworkElement lastContainer)
        {
            var lastOrigin = lastContainer.TransformToAncestor(host).Transform(new Point(0, 0));
            top = lastOrigin.Y + lastContainer.ActualHeight;
        }

        return new Rect(
            origin.X,
            top,
            Math.Max(CardItems.ActualWidth, ActualWidth - 8),
            ColumnKind == KanbanColumnKind.Notes ? 88 : 92);
    }

    private Size GetHostSize()
    {
        return Window.GetWindow(this)?.Content is FrameworkElement host
            ? new Size(host.ActualWidth, host.ActualHeight)
            : Size.Empty;
    }

    private void UpdateDragState(DragEventArgs e)
    {
        var card = GetDraggedCard(e);

        if (card is null)
        {
            e.Effects = DragDropEffects.None;
            ResetDragState();
            e.Handled = true;
            return;
        }

        var isAllowed = (card is TaskItem && ColumnKind != KanbanColumnKind.Notes)
            || (card is NoteItem && ColumnKind == KanbanColumnKind.Notes);

        e.Effects = isAllowed ? DragDropEffects.Move : DragDropEffects.None;

        if (isAllowed)
        {
            ColumnBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#BFDBFE")!;
            ColumnBorder.Background = (Brush)new BrushConverter().ConvertFromString("#EAF3FF")!;
            ColumnDropZone.Background = (Brush)new BrushConverter().ConvertFromString("#EFF6FF")!;
            ColumnDropZone.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#93C5FD")!;
            ColumnDropZone.Opacity = 1;
            ShowDropIndicator(e);
        }
        else
        {
            ColumnBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#FCA5A5")!;
            ColumnBorder.Background = (Brush)new BrushConverter().ConvertFromString("#FFF1F2")!;
            DropIndicator.Visibility = Visibility.Collapsed;
        }

        e.Handled = true;
    }

    private object? GetDraggedCard(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DragDropFormats.TaskCard))
        {
            return e.Data.GetData(DragDropFormats.TaskCard);
        }

        if (e.Data.GetDataPresent(DragDropFormats.NoteCard))
        {
            return e.Data.GetData(DragDropFormats.NoteCard);
        }

        return null;
    }

    private void ResetDragState()
    {
        ColumnBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#55FFFFFF")!;
        ColumnBorder.Background = (Brush)FindResource("ColumnBackgroundBrush");
        ColumnDropZone.Background = Brushes.Transparent;
        ColumnDropZone.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#66CBD5E1")!;
        ColumnDropZone.Opacity = IsMouseOver ? 0.48 : 0.18;
        DropIndicator.Visibility = Visibility.Collapsed;
        DropIndicator.BeginAnimation(MarginProperty, null);
        DropIndicator.Margin = new Thickness(6, 2, 6, 0);
        _lastDropIndex = -1;
        _lastSortReferenceY = double.NaN;
        InvalidateDropBounds();
    }

    private void OnColumnMouseEnter(object sender, MouseEventArgs e)
    {
        if (DropIndicator.Visibility == Visibility.Visible)
        {
            return;
        }

        ColumnDropZone.Opacity = 0.48;
    }

    private void OnColumnMouseLeave(object sender, MouseEventArgs e)
    {
        if (DropIndicator.Visibility == Visibility.Visible)
        {
            return;
        }

        ColumnDropZone.Opacity = 0.18;
    }

    private void ShowDropIndicator(DragEventArgs e)
    {
        var dropIndex = GetDropIndex(e);

        if (DropIndicator.Visibility == Visibility.Visible && dropIndex == _lastDropIndex)
        {
            return;
        }

        _lastDropIndex = dropIndex;
        MoveDropIndicator(GetDropIndicatorTop(dropIndex));
    }

    private void MoveDropIndicator(double top)
    {
        var targetMargin = new Thickness(6, Math.Max(2, top), 6, 0);

        if (DropIndicator.Visibility != Visibility.Visible)
        {
            DropIndicator.BeginAnimation(MarginProperty, null);
            DropIndicator.Margin = targetMargin;
            DropIndicator.Visibility = Visibility.Visible;
            return;
        }

        var animation = new ThicknessAnimation
        {
            To = targetMargin,
            Duration = TimeSpan.FromMilliseconds(80),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        DropIndicator.BeginAnimation(MarginProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private bool IsPointerInsideColumn(DragEventArgs e)
    {
        var position = e.GetPosition(ColumnBorder);
        return position.X >= 0
            && position.Y >= 0
            && position.X <= ColumnBorder.ActualWidth
            && position.Y <= ColumnBorder.ActualHeight;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CardDragDropSession.Ended -= OnCardDragEnded;
        CardDragDropSession.Ended += OnCardDragEnded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CardDragDropSession.Ended -= OnCardDragEnded;
    }

    private void OnCardDragEnded(object? sender, EventArgs e)
    {
        ResetDragState();
    }

    private int GetDropIndex(DragEventArgs e)
    {
        var count = CardItems.Items.Count;

        if (count == 0)
        {
            return 0;
        }

        var relativeTo = GetDropIndicatorHost();
        EnsureDropBounds(relativeTo);
        var referenceY = GetSortReferenceY(e, relativeTo);
        var referenceDelta = double.IsNaN(_lastSortReferenceY) ? 0 : referenceY - _lastSortReferenceY;
        var isMovingDown = referenceDelta > 0.5;
        var isMovingUp = referenceDelta < -0.5;
        _lastSortReferenceY = referenceY;

        for (var index = 0; index < count; index++)
        {
            if (!TryGetCachedContainerBounds(index, out var bounds))
            {
                continue;
            }

            var centerY = bounds.Top + bounds.Height / 2;
            var earlySwitchBias = Math.Min(22, bounds.Height * 0.18);
            var threshold = centerY;

            if (isMovingDown)
            {
                threshold -= earlySwitchBias;
            }
            else if (isMovingUp)
            {
                threshold += earlySwitchBias;
            }

            if (referenceY < threshold)
            {
                return index;
            }
        }

        return count;
    }

    private double GetSortReferenceY(DragEventArgs e, UIElement relativeTo)
    {
        var pointerY = e.GetPosition(relativeTo).Y;

        if (GetDragMetrics(e) is not { CardHeight: > 0 } metrics)
        {
            return pointerY;
        }

        return pointerY + metrics.CardHeight / 2 - metrics.CursorOffsetY;
    }

    private CardDragMetrics? GetDragMetrics(DragEventArgs e)
    {
        return e.Data.GetDataPresent(DragDropFormats.CardDragMetrics)
            ? e.Data.GetData(DragDropFormats.CardDragMetrics) as CardDragMetrics
            : null;
    }

    private double GetDropIndicatorTop(int dropIndex)
    {
        var count = CardItems.Items.Count;

        if (count == 0)
        {
            return 4;
        }

        var relativeTo = GetDropIndicatorHost();
        EnsureDropBounds(relativeTo);

        if (dropIndex <= 0 && TryGetCachedContainerBounds(0, out var firstBounds))
        {
            return firstBounds.Top - 2;
        }

        if (dropIndex >= count && TryGetCachedContainerBounds(count - 1, out var lastBounds))
        {
            return lastBounds.Bottom - 4;
        }

        if (TryGetCachedContainerBounds(dropIndex - 1, out var previousBounds) &&
            TryGetCachedContainerBounds(dropIndex, out var nextBounds))
        {
            var gapCenter = (previousBounds.Bottom + nextBounds.Top) / 2;
            return gapCenter - 2;
        }

        return 2;
    }

    private UIElement GetDropIndicatorHost()
    {
        return DropIndicator.Parent as UIElement ?? CardItems;
    }

    private void EnsureDropBounds(UIElement relativeTo)
    {
        var count = CardItems.Items.Count;

        if (_dropBoundsRelativeTo == relativeTo && _dropBoundsCount == count)
        {
            return;
        }

        _dropContainerBounds.Clear();
        _dropBoundsRelativeTo = relativeTo;
        _dropBoundsCount = count;

        for (var index = 0; index < count; index++)
        {
            _dropContainerBounds.Add(TryGetContainerBounds(index, relativeTo, out var bounds)
                ? bounds
                : Rect.Empty);
        }
    }

    private void InvalidateDropBounds()
    {
        _dropBoundsCount = -1;
        _dropBoundsRelativeTo = null;
        _dropContainerBounds.Clear();
    }

    private bool TryGetCachedContainerBounds(int index, out Rect bounds)
    {
        bounds = Rect.Empty;

        if (index < 0 || index >= _dropContainerBounds.Count)
        {
            return false;
        }

        bounds = _dropContainerBounds[index];
        return bounds.Height > 0;
    }

    private bool TryGetContainerBounds(int index, UIElement relativeTo, out Rect bounds)
    {
        bounds = Rect.Empty;

        if (index < 0 ||
            index >= CardItems.Items.Count ||
            CardItems.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container ||
            container.ActualHeight <= 0)
        {
            return false;
        }

        var origin = container.TranslatePoint(new Point(0, 0), relativeTo);
        bounds = new Rect(origin, new Size(container.ActualWidth, container.ActualHeight));
        return true;
    }

    private void UpdateEmptyState()
    {
        if (!IsLoaded)
        {
            return;
        }

        ColumnEmptyState.Visibility = CardItems.Items.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
