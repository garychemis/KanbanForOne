using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KanbanForOne.Controls;

public partial class TagChipEditorControl : UserControl
{
    private readonly TextBlock _placeholder = new()
    {
        Text = "+ 添加标签",
        Margin = new Thickness(2, 0, 7, 5),
        FontSize = 12,
        Foreground = BrushFrom("#94A3B8"),
        VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false
    };

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(TagChipEditorControl),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnTextChanged));

    public static readonly DependencyProperty EditorBackgroundProperty = DependencyProperty.Register(
        nameof(EditorBackground),
        typeof(Brush),
        typeof(TagChipEditorControl),
        new PropertyMetadata(BrushFrom("#FFFFFF")));

    public static readonly DependencyProperty EditorBorderBrushProperty = DependencyProperty.Register(
        nameof(EditorBorderBrush),
        typeof(Brush),
        typeof(TagChipEditorControl),
        new PropertyMetadata(BrushFrom("#E5E7EB")));

    public static readonly DependencyProperty ChipBackgroundProperty = DependencyProperty.Register(
        nameof(ChipBackground),
        typeof(Brush),
        typeof(TagChipEditorControl),
        new PropertyMetadata(BrushFrom("#F3F5F7")));

    public static readonly DependencyProperty ChipForegroundProperty = DependencyProperty.Register(
        nameof(ChipForeground),
        typeof(Brush),
        typeof(TagChipEditorControl),
        new PropertyMetadata(BrushFrom("#4B5563")));

    private readonly List<string> _tags = [];
    private bool _isSyncing;

    public TagChipEditorControl()
    {
        InitializeComponent();
        SyncTagsFromText(Text);
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

    public Brush ChipBackground
    {
        get => (Brush)GetValue(ChipBackgroundProperty);
        set => SetValue(ChipBackgroundProperty, value);
    }

    public Brush ChipForeground
    {
        get => (Brush)GetValue(ChipForegroundProperty);
        set => SetValue(ChipForegroundProperty, value);
    }

    public void FocusInput()
    {
        InputBox.Focus();
        Keyboard.Focus(InputBox);
        InputBox.CaretIndex = InputBox.Text.Length;
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TagChipEditorControl)d;

        if (control._isSyncing)
        {
            return;
        }

        control.SyncTagsFromText(e.NewValue as string ?? string.Empty);
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter)
        {
            CommitInput();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Back && string.IsNullOrEmpty(InputBox.Text) && _tags.Count > 0)
        {
            _tags.RemoveAt(_tags.Count - 1);
            SyncTextFromTags();
            RebuildChips();
            e.Handled = true;
        }
    }

    private void OnInputLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CommitInput();
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePlaceholderVisibility();
    }

    private void OnContainerMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        FocusInput();
    }

    private void CommitInput()
    {
        var added = false;

        foreach (var tag in ParseTags(InputBox.Text))
        {
            if (_tags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _tags.Add(tag);
            added = true;
        }

        InputBox.Text = string.Empty;

        if (added)
        {
            SyncTextFromTags();
            RebuildChips();
        }
    }

    private void SyncTagsFromText(string text)
    {
        _tags.Clear();

        foreach (var tag in ParseTags(text))
        {
            if (_tags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _tags.Add(tag);
        }

        RebuildChips();
    }

    private void SyncTextFromTags()
    {
        _isSyncing = true;
        Text = string.Join(", ", _tags);
        _isSyncing = false;
    }

    private void RebuildChips()
    {
        TagsPanel.Children.Clear();

        foreach (var tag in _tags)
        {
            TagsPanel.Children.Add(CreateChip(tag));
        }

        TagsPanel.Children.Add(_placeholder);
        TagsPanel.Children.Add(InputBox);
        UpdatePlaceholderVisibility();
    }

    private Border CreateChip(string tag)
    {
        var label = new TextBlock
        {
            Text = FormatTagForDisplay(tag),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = ChipForeground,
            VerticalAlignment = VerticalAlignment.Center
        };

        var removeButton = new Button
        {
            Content = "×",
            Width = 12,
            Height = 12,
            Padding = new Thickness(0),
            Margin = new Thickness(4, 0, 0, 0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = ChipForeground,
            Cursor = Cursors.Hand,
            ToolTip = "删除标签"
        };
        removeButton.Click += (_, _) =>
        {
            _tags.Remove(tag);
            SyncTextFromTags();
            RebuildChips();
            InputBox.Focus();
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(label);
        content.Children.Add(removeButton);

        return new Border
        {
            Padding = new Thickness(7, 3, 5, 3),
            Margin = new Thickness(0, 0, 6, 5),
            CornerRadius = new CornerRadius(999),
            Background = ChipBackground,
            Child = content
        };
    }

    private void UpdatePlaceholderVisibility()
    {
        _placeholder.Visibility = string.IsNullOrWhiteSpace(InputBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static IEnumerable<string> ParseTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (var item in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tag = item.Trim();

            if (!string.IsNullOrWhiteSpace(tag))
            {
                yield return tag;
            }
        }
    }

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }

    private static string FormatTagForDisplay(string tag)
    {
        return tag;
    }
}
