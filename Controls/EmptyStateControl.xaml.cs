using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KanbanForOne.Controls;

public partial class EmptyStateControl : UserControl
{
    public static readonly DependencyProperty IsSubtleProperty = DependencyProperty.Register(
        nameof(IsSubtle), typeof(bool), typeof(EmptyStateControl), new PropertyMetadata(false));

    public bool IsSubtle
    {
        get => (bool)GetValue(IsSubtleProperty);
        set => SetValue(IsSubtleProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(EmptyStateControl),
        new PropertyMetadata("暂无内容"));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(EmptyStateControl),
        new PropertyMetadata("添加一张卡片开始整理。"));

    public static readonly DependencyProperty ActionTextProperty = DependencyProperty.Register(
        nameof(ActionText), typeof(string), typeof(EmptyStateControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionCommandProperty = DependencyProperty.Register(
        nameof(ActionCommand), typeof(ICommand), typeof(EmptyStateControl), new PropertyMetadata(null));

    public EmptyStateControl()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string ActionText { get => (string)GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }
    public ICommand? ActionCommand { get => (ICommand?)GetValue(ActionCommandProperty); set => SetValue(ActionCommandProperty, value); }
    public bool HasAction => ActionCommand is not null && !string.IsNullOrWhiteSpace(ActionText);
}
