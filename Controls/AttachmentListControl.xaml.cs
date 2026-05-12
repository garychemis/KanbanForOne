using System.Collections;
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
        new PropertyMetadata(null));

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

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
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
}
