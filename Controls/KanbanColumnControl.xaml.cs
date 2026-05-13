using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KanbanForOne.Models;

namespace KanbanForOne.Controls;

public partial class KanbanColumnControl : UserControl
{
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
        ResetDragState();
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
            return;
        }

        var isAllowed = (card is TaskItem && ColumnKind != KanbanColumnKind.Notes)
            || (card is NoteItem && ColumnKind == KanbanColumnKind.Notes);

        e.Effects = isAllowed ? DragDropEffects.Move : DragDropEffects.None;

        if (isAllowed)
        {
            ColumnBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#9BA7B4")!;
            ColumnBorder.Background = (Brush)new BrushConverter().ConvertFromString("#F0F3F6")!;
            ShowDropPlaceholder(e);
        }
        else
        {
            ColumnBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#FCA5A5")!;
            ColumnBorder.Background = (Brush)new BrushConverter().ConvertFromString("#FFF1F2")!;
            DropPlaceholder.Visibility = Visibility.Collapsed;
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
        DropPlaceholder.Visibility = Visibility.Collapsed;
        DropPlaceholder.Margin = new Thickness(0, 2, 0, 0);
    }

    private void ShowDropPlaceholder(DragEventArgs e)
    {
        var dropIndex = GetDropIndex(e);
        var top = 2d;

        for (var index = 0; index < dropIndex; index++)
        {
            if (CardItems.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement container)
            {
                top += container.ActualHeight;
            }
        }

        DropPlaceholder.Margin = new Thickness(0, top, 0, 0);
        DropPlaceholder.Visibility = Visibility.Visible;
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
