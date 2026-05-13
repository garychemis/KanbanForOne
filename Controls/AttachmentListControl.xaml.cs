using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KanbanForOne.Controls;

public partial class AttachmentListControl : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(AttachmentListControl),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty HasItemsProperty = DependencyProperty.Register(
        nameof(HasItems),
        typeof(bool),
        typeof(AttachmentListControl),
        new PropertyMetadata(false));

    public static readonly DependencyProperty OpenCommandProperty = DependencyProperty.Register(
        nameof(OpenCommand),
        typeof(ICommand),
        typeof(AttachmentListControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty RevealCommandProperty = DependencyProperty.Register(
        nameof(RevealCommand),
        typeof(ICommand),
        typeof(AttachmentListControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty DeleteCommandProperty = DependencyProperty.Register(
        nameof(DeleteCommand),
        typeof(ICommand),
        typeof(AttachmentListControl),
        new PropertyMetadata(null));

    public AttachmentListControl()
    {
        InitializeComponent();
    }

    private INotifyCollectionChanged? _itemsSourceNotifications;

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public bool HasItems
    {
        get => (bool)GetValue(HasItemsProperty);
        private set => SetValue(HasItemsProperty, value);
    }

    public ICommand? OpenCommand
    {
        get => (ICommand?)GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    public ICommand? RevealCommand
    {
        get => (ICommand?)GetValue(RevealCommandProperty);
        set => SetValue(RevealCommandProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        var control = (AttachmentListControl)dependencyObject;

        if (control._itemsSourceNotifications is not null)
        {
            control._itemsSourceNotifications.CollectionChanged -= control.OnItemsSourceCollectionChanged;
        }

        control._itemsSourceNotifications = e.NewValue as INotifyCollectionChanged;

        if (control._itemsSourceNotifications is not null)
        {
            control._itemsSourceNotifications.CollectionChanged += control.OnItemsSourceCollectionChanged;
        }

        control.UpdateHasItems();
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateHasItems();
            return;
        }

        Dispatcher.Invoke(UpdateHasItems);
    }

    private void UpdateHasItems()
    {
        HasItems = ItemsSource switch
        {
            ICollection collection => collection.Count > 0,
            IEnumerable enumerable => enumerable.Cast<object>().Any(),
            _ => false
        };
    }
}
