using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

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

    private static readonly Regex HeadingRegex = new(@"^(#{1,3})\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^\d+\.\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedListRegex = new(@"^[-*]\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex InlineRegex = new(@"(\*\*.+?\*\*|__.+?__|`.+?`|\*.+?\*|_.+?_|!\[.*?\]\(.*?\)|\[.*?\]\(.*?\))", RegexOptions.Compiled);

    public MarkdownViewerControl()
    {
        InitializeComponent();
        RenderMarkdown();
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

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MarkdownViewerControl)d).RenderMarkdown();
    }

    private void RenderMarkdown()
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI, Microsoft YaHei UI, Microsoft YaHei"),
            FontSize = 13,
            Foreground = BrushFrom("#2B2F33")
        };

        var lines = (Markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');

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
                document.Blocks.Add(CreateHeading(headingMatch.Groups[1].Value.Length, headingMatch.Groups[2].Value));
                continue;
            }

            if (line.TrimStart().StartsWith("> ", StringComparison.Ordinal))
            {
                document.Blocks.Add(CreateQuote(line.TrimStart()[2..]));
                continue;
            }

            var unorderedMatch = UnorderedListRegex.Match(line.TrimStart());
            if (unorderedMatch.Success)
            {
                var list = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(18, 0, 0, 8) };

                while (i < lines.Length)
                {
                    var itemMatch = UnorderedListRegex.Match(lines[i].TrimStart());
                    if (!itemMatch.Success)
                    {
                        i--;
                        break;
                    }

                    list.ListItems.Add(CreateListItem(itemMatch.Groups[1].Value));
                    i++;
                }

                document.Blocks.Add(list);
                continue;
            }

            var orderedMatch = OrderedListRegex.Match(line.TrimStart());
            if (orderedMatch.Success)
            {
                var list = new List { MarkerStyle = TextMarkerStyle.Decimal, Margin = new Thickness(18, 0, 0, 8) };

                while (i < lines.Length)
                {
                    var itemMatch = OrderedListRegex.Match(lines[i].TrimStart());
                    if (!itemMatch.Success)
                    {
                        i--;
                        break;
                    }

                    list.ListItems.Add(CreateListItem(itemMatch.Groups[1].Value));
                    i++;
                }

                document.Blocks.Add(list);
                continue;
            }

            document.Blocks.Add(CreateParagraph(line));
        }

        Viewer.Document = document;
    }

    private static Paragraph CreateHeading(int level, string text)
    {
        return new Paragraph(new Run(text))
        {
            FontSize = level switch { 1 => 18, 2 => 16, _ => 14 },
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#111827"),
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

    private static ListItem CreateListItem(string text)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 4), LineHeight = 19 };
        foreach (var inline in CreateInlines(text))
        {
            paragraph.Inlines.Add(inline);
        }

        return new ListItem(paragraph);
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

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}
