using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KanbanForOne.Models;

namespace KanbanForOne.Controls;

public partial class KanbanColumnControl : UserControl
{
    private int _lastDropIndex = -1;

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
        new PropertyMetadata(null));

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
        LayoutUpdated += (_, _) => UpdateEmptyState();
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

    private void OnDrop(object sender, DragEventArgs e)
    {
        var card = GetDraggedCard(e);

        if (card is null)
        {
            ResetDragState();
            return;
        }

        var payload = new CardDropPayload(card, ColumnKind, GetDropIndex(e));

        if (DropCommand?.CanExecute(payload) == true)
        {
            DropCommand.Execute(payload);
        }

        ResetDragState();
        e.Handled = true;
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
            ColumnBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#9BA7B4")!;
            ColumnBorder.Background = (Brush)new BrushConverter().ConvertFromString("#F0F3F6")!;
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
        ColumnBorder.BorderBrush = Brushes.Transparent;
        ColumnBorder.Background = (Brush)FindResource("ColumnBackgroundBrush");
        DropIndicator.Visibility = Visibility.Collapsed;
        DropIndicator.Margin = new Thickness(6, 2, 6, 0);
        _lastDropIndex = -1;
    }

    private void ShowDropIndicator(DragEventArgs e)
    {
        var dropIndex = GetDropIndex(e);

        if (DropIndicator.Visibility == Visibility.Visible && dropIndex == _lastDropIndex)
        {
            return;
        }

        var top = 2d;

        for (var index = 0; index < dropIndex; index++)
        {
            if (CardItems.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement container)
            {
                top += container.ActualHeight;
            }
        }

        _lastDropIndex = dropIndex;
        DropIndicator.Margin = new Thickness(6, Math.Max(2, top - 3), 6, 0);
        DropIndicator.Visibility = Visibility.Visible;
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

        for (var index = 0; index < count; index++)
        {
            if (CardItems.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container)
            {
                continue;
            }

            var position = e.GetPosition(container);

            if (position.Y < container.ActualHeight / 2)
            {
                return index;
            }
        }

        return count;
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
