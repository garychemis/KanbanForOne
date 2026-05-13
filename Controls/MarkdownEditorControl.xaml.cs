using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace KanbanForOne.Controls;

public partial class MarkdownEditorControl : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(MarkdownEditorControl),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty EditorBackgroundProperty = DependencyProperty.Register(
        nameof(EditorBackground),
        typeof(Brush),
        typeof(MarkdownEditorControl),
        new PropertyMetadata(BrushFrom("#FFFFFF")));

    public static readonly DependencyProperty EditorBorderBrushProperty = DependencyProperty.Register(
        nameof(EditorBorderBrush),
        typeof(Brush),
        typeof(MarkdownEditorControl),
        new PropertyMetadata(BrushFrom("#ECECEC")));

    public static readonly DependencyProperty EditorForegroundProperty = DependencyProperty.Register(
        nameof(EditorForeground),
        typeof(Brush),
        typeof(MarkdownEditorControl),
        new PropertyMetadata(BrushFrom("#242424")));

    public static readonly DependencyProperty EditorMinHeightProperty = DependencyProperty.Register(
        nameof(EditorMinHeight),
        typeof(double),
        typeof(MarkdownEditorControl),
        new PropertyMetadata(236d));

    public static readonly DependencyProperty EditorCornerRadiusProperty = DependencyProperty.Register(
        nameof(EditorCornerRadius),
        typeof(CornerRadius),
        typeof(MarkdownEditorControl),
        new PropertyMetadata(new CornerRadius(11)));

    public static readonly DependencyProperty EditorPaddingProperty = DependencyProperty.Register(
        nameof(EditorPadding),
        typeof(Thickness),
        typeof(MarkdownEditorControl),
        new PropertyMetadata(new Thickness(14)));

    public static readonly DependencyProperty EditorBorderThicknessProperty = DependencyProperty.Register(
        nameof(EditorBorderThickness),
        typeof(Thickness),
        typeof(MarkdownEditorControl),
        new PropertyMetadata(new Thickness(1)));

    public MarkdownEditorControl()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush EditorBackground
    {
        get => (Brush)GetValue(EditorBackgroundProperty);
        set => SetValue(EditorBackgroundProperty, value);
    }

    public Brush EditorBorderBrush
    {
        get => (Brush)GetValue(EditorBorderBrushProperty);
        set => SetValue(EditorBorderBrushProperty, value);
    }

    public Brush EditorForeground
    {
        get => (Brush)GetValue(EditorForegroundProperty);
        set => SetValue(EditorForegroundProperty, value);
    }

    public double EditorMinHeight
    {
        get => (double)GetValue(EditorMinHeightProperty);
        set => SetValue(EditorMinHeightProperty, value);
    }

    public CornerRadius EditorCornerRadius
    {
        get => (CornerRadius)GetValue(EditorCornerRadiusProperty);
        set => SetValue(EditorCornerRadiusProperty, value);
    }

    public Thickness EditorPadding
    {
        get => (Thickness)GetValue(EditorPaddingProperty);
        set => SetValue(EditorPaddingProperty, value);
    }

    public Thickness EditorBorderThickness
    {
        get => (Thickness)GetValue(EditorBorderThicknessProperty);
        set => SetValue(EditorBorderThicknessProperty, value);
    }

    private void OnEditorHostPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Editor.Visibility == Visibility.Visible)
        {
            return;
        }

        EnterEditMode();
        e.Handled = true;
    }

    private void OnEditorLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ExitEditMode();
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            ExitEditMode();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        ExitEditMode();
        e.Handled = true;
    }

    private void EnterEditMode()
    {
        RenderedView.Visibility = Visibility.Collapsed;
        Editor.Visibility = Visibility.Visible;

        Dispatcher.BeginInvoke(() =>
        {
            Editor.Focus();
            Keyboard.Focus(Editor);
            Editor.CaretIndex = Editor.Text.Length;
        }, DispatcherPriority.Input);
    }

    private void ExitEditMode()
    {
        Editor.Visibility = Visibility.Collapsed;
        RenderedView.Visibility = Visibility.Visible;
    }

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}
