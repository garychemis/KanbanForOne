using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace KanbanForOne.Controls;

public partial class MarkdownPreviewControl : UserControl
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown),
        typeof(string),
        typeof(MarkdownPreviewControl),
        new PropertyMetadata(string.Empty, OnMarkdownChanged));

    public static readonly DependencyProperty PreviewForegroundProperty = DependencyProperty.Register(
        nameof(PreviewForeground),
        typeof(Brush),
        typeof(MarkdownPreviewControl),
        new PropertyMetadata(BrushFrom("#56616F"), OnMarkdownChanged));

    public static readonly DependencyProperty PreviewFontSizeProperty = DependencyProperty.Register(
        nameof(PreviewFontSize),
        typeof(double),
        typeof(MarkdownPreviewControl),
        new PropertyMetadata(12d));

    public static readonly DependencyProperty PreviewLineHeightProperty = DependencyProperty.Register(
        nameof(PreviewLineHeight),
        typeof(double),
        typeof(MarkdownPreviewControl),
        new PropertyMetadata(17d));

    public static readonly DependencyProperty PreviewMaxHeightProperty = DependencyProperty.Register(
        nameof(PreviewMaxHeight),
        typeof(double),
        typeof(MarkdownPreviewControl),
        new PropertyMetadata(36d));

    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^(\d+)\.\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedListRegex = new(@"^[-*+]\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex InlineRegex = new(@"(\*\*.+?\*\*|__.+?__|`.+?`|\*.+?\*|_.+?_|!\[.*?\]\(.*?\)|\[.*?\]\(.*?\))", RegexOptions.Compiled);

    public MarkdownPreviewControl()
    {
        InitializeComponent();
        RenderMarkdown();
    }

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public Brush PreviewForeground
    {
        get => (Brush)GetValue(PreviewForegroundProperty);
        set => SetValue(PreviewForegroundProperty, value);
    }

    public double PreviewFontSize
    {
        get => (double)GetValue(PreviewFontSizeProperty);
        set => SetValue(PreviewFontSizeProperty, value);
    }

    public double PreviewLineHeight
    {
        get => (double)GetValue(PreviewLineHeightProperty);
        set => SetValue(PreviewLineHeightProperty, value);
    }

    public double PreviewMaxHeight
    {
        get => (double)GetValue(PreviewMaxHeightProperty);
        set => SetValue(PreviewMaxHeightProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((MarkdownPreviewControl)dependencyObject).RenderMarkdown();
    }

    private void RenderMarkdown()
    {
        PreviewText.Inlines.Clear();

        foreach (var previewLine in CreatePreviewLines(Markdown ?? string.Empty))
        {
            if (PreviewText.Inlines.Count > 0)
            {
                PreviewText.Inlines.Add(new LineBreak());
            }

            foreach (var inline in CreatePreviewInlines(previewLine))
            {
                PreviewText.Inlines.Add(inline);
            }
        }
    }

    private static IEnumerable<string> CreatePreviewLines(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var inCodeBlock = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (line is "---" or "***")
            {
                continue;
            }

            if (inCodeBlock)
            {
                yield return $"`{line}`";
                continue;
            }

            var headingMatch = HeadingRegex.Match(line);
            if (headingMatch.Success)
            {
                yield return $"**{headingMatch.Groups[2].Value}**";
                continue;
            }

            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                yield return line[2..];
                continue;
            }

            var orderedMatch = OrderedListRegex.Match(line);
            if (orderedMatch.Success)
            {
                yield return $"{orderedMatch.Groups[1].Value}. {orderedMatch.Groups[2].Value}";
                continue;
            }

            var unorderedMatch = UnorderedListRegex.Match(line);
            if (unorderedMatch.Success)
            {
                yield return $"• {unorderedMatch.Groups[1].Value}";
                continue;
            }

            yield return line;
        }
    }

    private static IEnumerable<Inline> CreatePreviewInlines(string text)
    {
        var index = 0;

        foreach (Match match in InlineRegex.Matches(text))
        {
            if (match.Index > index)
            {
                yield return new Run(text[index..match.Index]);
            }

            yield return CreatePreviewInline(match.Value);
            index = match.Index + match.Length;
        }

        if (index < text.Length)
        {
            yield return new Run(text[index..]);
        }
    }

    private static Inline CreatePreviewInline(string markdown)
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
            return new Italic(new Run(imageMatch.Groups[1].Value));
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

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}
