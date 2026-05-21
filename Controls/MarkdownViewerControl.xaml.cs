using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace KanbanForOne.Controls;

public partial class MarkdownViewerControl : UserControl
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown),
        typeof(string),
        typeof(MarkdownViewerControl),
        new PropertyMetadata(string.Empty, OnMarkdownChanged));

    public static readonly DependencyProperty ViewerBackgroundProperty = DependencyProperty.Register(
        nameof(ViewerBackground),
        typeof(Brush),
        typeof(MarkdownViewerControl),
        new PropertyMetadata(BrushFrom("#FFFFFF")));

    public static readonly DependencyProperty ViewerBorderBrushProperty = DependencyProperty.Register(
        nameof(ViewerBorderBrush),
        typeof(Brush),
        typeof(MarkdownViewerControl),
        new PropertyMetadata(BrushFrom("#ECECEC")));

    public static readonly DependencyProperty ViewerForegroundProperty = DependencyProperty.Register(
        nameof(ViewerForeground),
        typeof(Brush),
        typeof(MarkdownViewerControl),
        new PropertyMetadata(BrushFrom("#2B2F33"), OnViewerForegroundChanged));

    public static readonly DependencyProperty ForwardMouseWheelToParentScrollViewerProperty = DependencyProperty.Register(
        nameof(ForwardMouseWheelToParentScrollViewer),
        typeof(bool),
        typeof(MarkdownViewerControl),
        new PropertyMetadata(false));

    private static readonly Regex HeadingRegex = new(@"^(#{1,3})\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^\s*\d+\.\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedListRegex = new(@"^\s*[-*+]\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex ListLineRegex = new(@"^(?<indent>\s*)(?<marker>(?:[-*+])|(?:\d+\.))\s+(?<text>.+)$", RegexOptions.Compiled);
    private static readonly Regex InlineRegex = new(@"(\*\*.+?\*\*|__.+?__|`.+?`|\*.+?\*|_.+?_|!\[.*?\]\(.*?\)|\[.*?\]\(.*?\))", RegexOptions.Compiled);
    private int _renderVersion;
    private bool _hasRendered;
    private string? _renderedMarkdown;
    private Brush? _renderedForeground;

    public MarkdownViewerControl()
    {
        InitializeComponent();
        Loaded += (_, _) => ScheduleRenderMarkdown();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                ScheduleRenderMarkdown();
            }
        };
    }

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public Brush ViewerBackground
    {
        get => (Brush)GetValue(ViewerBackgroundProperty);
        set => SetValue(ViewerBackgroundProperty, value);
    }

    public Brush ViewerBorderBrush
    {
        get => (Brush)GetValue(ViewerBorderBrushProperty);
        set => SetValue(ViewerBorderBrushProperty, value);
    }

    public Brush ViewerForeground
    {
        get => (Brush)GetValue(ViewerForegroundProperty);
        set => SetValue(ViewerForegroundProperty, value);
    }

    public bool ForwardMouseWheelToParentScrollViewer
    {
        get => (bool)GetValue(ForwardMouseWheelToParentScrollViewerProperty);
        set => SetValue(ForwardMouseWheelToParentScrollViewerProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MarkdownViewerControl)d).ScheduleRenderMarkdown();
    }

    private static void OnViewerForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MarkdownViewerControl)d).ScheduleRenderMarkdown();
    }

    private void ScheduleRenderMarkdown()
    {
        var version = ++_renderVersion;

        if (!IsLoaded || !IsVisible)
        {
            return;
        }

        Dispatcher.BeginInvoke((Action)(() =>
        {
            if (version != _renderVersion || !IsVisible)
            {
                return;
            }

            RenderMarkdownIfNeeded();
        }), DispatcherPriority.Background);
    }

    private void RenderMarkdownIfNeeded()
    {
        var markdown = Markdown ?? string.Empty;
        var foreground = ViewerForeground;

        if (_hasRendered &&
            string.Equals(_renderedMarkdown, markdown, StringComparison.Ordinal) &&
            Equals(_renderedForeground, foreground))
        {
            return;
        }

        RenderMarkdown(markdown, foreground);
        _hasRendered = true;
        _renderedMarkdown = markdown;
        _renderedForeground = foreground;
    }

    private void RenderMarkdown(string markdown, Brush foreground)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI, Microsoft YaHei UI, Microsoft YaHei"),
            FontSize = 13,
            Foreground = foreground
        };

        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        if (lines.All(string.IsNullOrWhiteSpace))
        {
            document.Blocks.Add(new Paragraph(new Run("暂无描述"))
            {
                Foreground = BrushFrom("#8A8F98"),
                Margin = new Thickness(0)
            });

            Viewer.Document = document;
            return;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                var codeLines = new List<string>();
                i++;

                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    codeLines.Add(lines[i]);
                    i++;
                }

                document.Blocks.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeLines)));
                continue;
            }

            if (line.Trim() is "---" or "***")
            {
                document.Blocks.Add(new BlockUIContainer(new Border
                {
                    Height = 1,
                    Background = BrushFrom("#E5E7EB"),
                    Margin = new Thickness(0, 8, 0, 8)
                }));
                continue;
            }

            var headingMatch = HeadingRegex.Match(line);
            if (headingMatch.Success)
            {
                document.Blocks.Add(CreateHeading(headingMatch.Groups[1].Value.Length, headingMatch.Groups[2].Value, foreground));
                continue;
            }

            if (line.TrimStart().StartsWith("> ", StringComparison.Ordinal))
            {
                document.Blocks.Add(CreateQuote(line.TrimStart()[2..]));
                continue;
            }

            if (TryParseListLine(line, out _))
            {
                var listLines = new List<ListLine>();

                while (i < lines.Length)
                {
                    if (!TryParseListLine(lines[i], out var listLine))
                    {
                        i--;
                        break;
                    }

                    listLines.Add(listLine);
                    i++;
                }

                var listIndex = 0;
                while (listIndex < listLines.Count)
                {
                    document.Blocks.Add(CreateList(listLines, ref listIndex));
                }

                continue;
            }

            document.Blocks.Add(CreateParagraph(line));
        }

        Viewer.Document = document;
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!ForwardMouseWheelToParentScrollViewer || e.Handled)
        {
            return;
        }

        var innerScrollViewer = FindDescendant<ScrollViewer>(Viewer);

        if (innerScrollViewer is not null && CanScrollInDirection(innerScrollViewer, e.Delta))
        {
            return;
        }

        var parentScrollViewer = FindAncestor<ScrollViewer>(this);

        if (parentScrollViewer is null || !CanScrollInDirection(parentScrollViewer, e.Delta))
        {
            return;
        }

        ScrollByWheel(parentScrollViewer, e.Delta);
        e.Handled = true;
    }

    private static bool CanScrollInDirection(ScrollViewer scrollViewer, int delta)
    {
        const double epsilon = 0.1;

        if (scrollViewer.ScrollableHeight <= epsilon)
        {
            return false;
        }

        return delta < 0
            ? scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight - epsilon
            : scrollViewer.VerticalOffset > epsilon;
    }

    private static void ScrollByWheel(ScrollViewer scrollViewer, int delta)
    {
        const double pixelsPerWheelDelta = 0.35;
        var targetOffset = Math.Clamp(
            scrollViewer.VerticalOffset - delta * pixelsPerWheelDelta,
            0,
            scrollViewer.ScrollableHeight);

        scrollViewer.ScrollToVerticalOffset(targetOffset);
    }

    private static T? FindAncestor<T>(DependencyObject current)
        where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(current);

        while (parent is not null)
        {
            if (parent is T typedParent)
            {
                return typedParent;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private static T? FindDescendant<T>(DependencyObject current)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(current);

        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(current, index);

            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindDescendant<T>(child);

            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static Paragraph CreateHeading(int level, string text, Brush foreground)
    {
        return new Paragraph(new Run(text))
        {
            FontSize = level switch { 1 => 18, 2 => 16, _ => 14 },
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground,
            Margin = new Thickness(0, level == 1 ? 0 : 8, 0, 8)
        };
    }

    private static Paragraph CreateParagraph(string text)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 8),
            LineHeight = 20
        };

        foreach (var inline in CreateInlines(text))
        {
            paragraph.Inlines.Add(inline);
        }

        return paragraph;
    }

    private static Paragraph CreateQuote(string text)
    {
        var paragraph = CreateParagraph(text);
        paragraph.Margin = new Thickness(10, 0, 0, 8);
        paragraph.Foreground = BrushFrom("#56616F");
        paragraph.BorderBrush = BrushFrom("#CBD5E1");
        paragraph.BorderThickness = new Thickness(3, 0, 0, 0);
        paragraph.Padding = new Thickness(10, 0, 0, 0);
        return paragraph;
    }

    private static Paragraph CreateCodeBlock(string code)
    {
        return new Paragraph(new Run(code))
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12,
            Foreground = BrushFrom("#1F2937"),
            Background = BrushFrom("#F3F4F6"),
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(10),
            LineHeight = 18
        };
    }

    private static List CreateList(IReadOnlyList<ListLine> lines, ref int index)
    {
        var current = lines[index];
        var indent = current.Indent;
        var isOrdered = current.IsOrdered;
        var list = new List
        {
            MarkerStyle = isOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = new Thickness(indent == 0 ? 18 : 16, 0, 0, 6),
            Padding = new Thickness(0)
        };

        while (index < lines.Count)
        {
            var line = lines[index];

            if (line.Indent < indent || line.Indent == indent && line.IsOrdered != isOrdered)
            {
                break;
            }

            if (line.Indent > indent)
            {
                break;
            }

            var item = CreateListItem(line.Text);
            index++;

            while (index < lines.Count && lines[index].Indent > indent)
            {
                item.Blocks.Add(CreateList(lines, ref index));
            }

            list.ListItems.Add(item);
        }

        return list;
    }

    private static ListItem CreateListItem(string text)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 4), LineHeight = 19 };
        foreach (var inline in CreateInlines(text))
        {
            paragraph.Inlines.Add(inline);
        }

        return new ListItem(paragraph);
    }

    private static bool TryParseListLine(string line, out ListLine listLine)
    {
        var match = ListLineRegex.Match(line);

        if (!match.Success)
        {
            listLine = default;
            return false;
        }

        var marker = match.Groups["marker"].Value;
        listLine = new ListLine(
            IndentWidth(match.Groups["indent"].Value),
            marker.EndsWith(".", StringComparison.Ordinal),
            match.Groups["text"].Value);
        return true;
    }

    private static int IndentWidth(string indent)
    {
        return indent.Sum(character => character == '\t' ? 4 : 1);
    }

    private static IEnumerable<Inline> CreateInlines(string text)
    {
        var index = 0;

        foreach (Match match in InlineRegex.Matches(text))
        {
            if (match.Index > index)
            {
                yield return new Run(text[index..match.Index]);
            }

            yield return CreateInline(match.Value);
            index = match.Index + match.Length;
        }

        if (index < text.Length)
        {
            yield return new Run(text[index..]);
        }
    }

    private static Inline CreateInline(string markdown)
    {
        if (markdown.StartsWith("**", StringComparison.Ordinal) && markdown.EndsWith("**", StringComparison.Ordinal))
        {
            return new Bold(new Run(markdown[2..^2]));
        }

        if (markdown.StartsWith("__", StringComparison.Ordinal) && markdown.EndsWith("__", StringComparison.Ordinal))
        {
            return new Bold(new Run(markdown[2..^2]));
        }

        if (markdown.StartsWith('`') && markdown.EndsWith('`'))
        {
            return new Run(markdown[1..^1])
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Background = BrushFrom("#EEF2F7"),
                Foreground = BrushFrom("#111827")
            };
        }

        var imageMatch = Regex.Match(markdown, @"^!\[(.*?)\]\((.*?)\)$");
        if (imageMatch.Success)
        {
            return new Italic(new Run($"图片：{imageMatch.Groups[1].Value}"));
        }

        var linkMatch = Regex.Match(markdown, @"^\[(.*?)\]\((.*?)\)$");
        if (linkMatch.Success)
        {
            return new Run(linkMatch.Groups[1].Value)
            {
                Foreground = BrushFrom("#2563EB"),
                TextDecorations = TextDecorations.Underline
            };
        }

        if ((markdown.StartsWith('*') && markdown.EndsWith('*')) ||
            (markdown.StartsWith('_') && markdown.EndsWith('_')))
        {
            return new Italic(new Run(markdown[1..^1]));
        }

        return new Run(markdown);
    }

    private readonly record struct ListLine(int Indent, bool IsOrdered, string Text);

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}
