using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KanbanForOne.Controls;
using KanbanForOne.Models;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.ViewModels;
using TaskStatus = KanbanForOne.Models.TaskStatus;

namespace KanbanForOne.Tests;

/// <summary>Runs inside UiSmokeTests' single STA/Application, for both palettes.</summary>
internal static class CardHoverAssertions
{
    private const string Details = "第一行详情\r\n第二行详情\r第三行详情\n第四行详情\n第五行不应出现";
    private static readonly DependencyPropertyKey MouseOverKey =
        (DependencyPropertyKey)typeof(UIElement).GetField("IsMouseOverPropertyKey", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    private static readonly MethodInfo WriteCoreFlag = typeof(UIElement).GetMethod("WriteFlag", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly object MouseOverFlag = Enum.Parse(WriteCoreFlag.GetParameters()[0].ParameterType, "IsMouseOverCache");

    public static void Verify()
    {
        foreach (var compact in new[] { false, true })
        {
            var task = new TaskItem { Title = "完成任务标题", Description = Details, Status = TaskStatus.Done, IsArchived = compact };
            var taskCard = new TaskCardControl { DataContext = task, IsCompact = compact };
            VerifyHoverHeight(taskCard, taskCard);
            VerifyEmptyContent(taskCard, () => task.Description = "  \r\n\t ");

            var note = new NoteItem { Title = "备忘标题", Content = Details, IsArchived = compact };
            var noteCard = new NoteCardControl { DataContext = note, IsCompact = compact };
            VerifyHoverHeight(noteCard, noteCard);
            VerifyEmptyContent(noteCard, () => note.Content = string.Empty);

            object? opened = null;
            taskCard.OpenCommand = new RelayCommand(parameter => opened = parameter);
            taskCard.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonUpEvent
            });
            Assert.IsType<CardOpenPayload>(opened);
        }

        VerifyActiveTaskCards();
        VerifyCalendarCards();
        VerifyCalendarMonthLayout();
        VerifyDoneDecoration();
        VerifyWrappingAndMarkdown();
        SavePreview();
    }

    private static void VerifyHoverHeight(FrameworkElement card, UIElement hoverTarget)
    {
        Layout(card);
        Assert.NotEmpty(FindAll<MarkdownPreviewControl>(card));
        Assert.Empty(VisiblePreviews(card));
        var collapsedHeight = card.DesiredSize.Height;

        SetHover(hoverTarget, true, card);
        var preview = Assert.Single(VisiblePreviews(card));
        Assert.True(card.DesiredSize.Height > collapsedHeight + 5, "悬停应增加卡片高度");
        Assert.InRange(preview.ActualHeight, 1, preview.PreviewLineHeight * 4 + 0.1);
        var text = Find<TextBlock>(preview)!;
        Assert.DoesNotContain("第五行不应出现", new TextRange(text.ContentStart, text.ContentEnd).Text);

        SetHover(hoverTarget, false, card);
        Assert.Empty(VisiblePreviews(card));
        Assert.Equal(collapsedHeight, card.DesiredSize.Height, precision: 2);
    }

    private static void VerifyEmptyContent(FrameworkElement card, Action clearDetails)
    {
        clearDetails();
        Layout(card);
        var height = card.DesiredSize.Height;
        SetHover(card, true, card);
        Assert.All(FindAll<MarkdownPreviewControl>(card), preview => Assert.False(preview.HasPreviewContent));
        Assert.Empty(VisiblePreviews(card));
        Assert.Equal(height, card.DesiredSize.Height, precision: 2);
        SetHover(card, false, card);
    }

    private static void VerifyActiveTaskCards()
    {
        foreach (var compact in new[] { false, true })
        foreach (var status in new[] { TaskStatus.Todo, TaskStatus.Doing, TaskStatus.Blocked })
        {
            var task = CreateTask(status, compact);
            var card = new TaskCardControl { DataContext = task, IsCompact = compact };
            VerifyActiveTaskContent(card, task, compact);
            var originalHeight = card.DesiredSize.Height;
            VerifyHoverDoesNotResize(card);

            // Reuse the same model and control: moving between columns must switch
            // presentation immediately, including while the pointer stays over it.
            task.Status = TaskStatus.Done;
            Layout(card);
            VerifyHoverHeight(card, card);
            Assert.DoesNotContain("优先级 高", VisibleText(card));
            SetHover(card, true, card);
            task.Status = status;
            Layout(card);
            VerifyActiveTaskContent(card, task, compact);
            Assert.Equal(originalHeight, card.DesiredSize.Height, precision: 2);
            SetHover(card, false, card);
            Assert.Equal(originalHeight, card.DesiredSize.Height, precision: 2);
        }
    }

    private static TaskItem CreateTask(TaskStatus status, bool compact = false)
    {
        var task = new TaskItem
        {
            Title = "状态展示验证任务", Description = Details, Status = status,
            Priority = TaskPriority.High, StartDate = DateTime.Today.AddDays(-3),
            EndDate = DateTime.Today.AddDays(-1), IsArchived = compact
        };
        task.Tags.Add("回归标签");
        task.Attachments.Add(new AttachmentItem { OriginalFileName = "需求说明.pdf" });
        return task;
    }

    private static void VerifyActiveTaskContent(TaskCardControl card, TaskItem task, bool compact)
    {
        Layout(card);
        var text = VisibleText(card);
        Assert.Contains(task.Title, text);
        Assert.Contains("优先级 高", text);
        Assert.Contains("附件 1", text);
        if (compact)
        {
            Assert.Empty(VisiblePreviews(card));
            Assert.DoesNotContain(task.DateRangeDisplay, text);
            Assert.DoesNotContain("回归标签", text);
            Assert.DoesNotContain("超期", text);
        }
        else
        {
            var preview = Assert.Single(VisiblePreviews(card));
            Assert.InRange(preview.ActualHeight, 1, 36.1);
            Assert.Contains("第一行详情", text);
            Assert.Contains(task.DateRangeDisplay, text);
            Assert.Contains("回归标签", text);
            Assert.Contains("超期", text);
        }
    }

    private static void VerifyHoverDoesNotResize(FrameworkElement card)
    {
        Layout(card);
        var height = card.DesiredSize.Height;
        var text = VisibleText(card);
        SetHover(card, true, card);
        Assert.Equal(height, card.DesiredSize.Height, precision: 2);
        Assert.Equal(text, VisibleText(card));
        SetHover(card, false, card);
        Assert.Equal(height, card.DesiredSize.Height, precision: 2);
        Assert.Equal(text, VisibleText(card));
    }

    private static void VerifyCalendarCards()
    {
        var calendar = new CalendarView();
        foreach (var key in new[] { "CalendarTaskListTemplate", "CalendarListTemplate", "CalendarChipTemplate" })
        {
            var task = CreateTask(TaskStatus.Done);
            var card = (FrameworkElement)((DataTemplate)calendar.Resources[key]).LoadContent();
            card.DataContext = key == "CalendarChipTemplate"
                ? new CalendarTaskChip { Task = task, IsMultiDay = true, StartsOnDate = true } : task;
            VerifyHoverHeight(card, card);
            foreach (var status in new[] { TaskStatus.Todo, TaskStatus.Doing, TaskStatus.Blocked })
            {
                task.Status = status;
                Layout(card);
                var text = VisibleText(card);
                Assert.Contains(task.Title, text);
                if (key == "CalendarChipTemplate") Assert.Contains(">", text);
                else Assert.Contains(task.DateRangeDisplay, text);
                if (key == "CalendarListTemplate")
                {
                    Assert.Contains("第一行详情", text);
                    Assert.Contains("优先级 高", text);
                    Assert.InRange(Assert.Single(VisiblePreviews(card)).ActualHeight, 1, 38.1);
                }
                else Assert.Empty(VisiblePreviews(card));
                VerifyHoverDoesNotResize(card);
                task.Status = TaskStatus.Done;
                Layout(card);
                VerifyHoverHeight(card, card);
            }
        }

        var workCard = (FrameworkElement)((DataTemplate)calendar.Resources["CalendarWorkHourListTemplate"]).LoadContent();
        workCard.DataContext = new WorkHourEntry
        {
            ProjectNumber = "P2026", Discipline = "工艺", WorkActivity = "设计", HourUnits = 8, Remark = Details
        };
        Layout(workCard);
        Assert.Contains("工艺", VisibleText(workCard));
        Assert.Contains("设计", VisibleText(workCard));
        Assert.Contains("第一行详情", VisibleText(workCard));
        VerifyHoverDoesNotResize(workCard);

        var designCard = (FrameworkElement)((DataTemplate)calendar.Resources["CalendarDesignConditionListTemplate"]).LoadContent();
        designCard.DataContext = new DesignConditionEntry
        {
            ConditionName = "接口设计条件", ProjectNumber = "P2026", IssuingDiscipline = "工艺",
            ReceivingDiscipline = "设备", Receiver = "张工", DrawingSize = "A1", DrawingCount = 2
        };
        Layout(designCard);
        Assert.Contains("P2026", VisibleText(designCard));
        Assert.Contains("工艺", VisibleText(designCard));
        Assert.Contains("设备", VisibleText(designCard));
        Assert.Contains("2 张", VisibleText(designCard));
        VerifyHoverDoesNotResize(designCard);
    }

    private static void VerifyWrappingAndMarkdown()
    {
        var preview = new MarkdownPreviewControl
        {
            Markdown = string.Concat(Enumerable.Repeat("自动换行的详细内容需要保持最多四行显示。", 20))
        };
        Layout(preview, 170);
        Assert.Equal(68, preview.ActualHeight, precision: 2);

        preview.Markdown = "# 标题\n\n**重点**\n`代码`\n第五行不应出现";
        Layout(preview);
        var text = Find<TextBlock>(preview)!;
        Assert.Equal(3, text.Inlines.OfType<LineBreak>().Count());
        Assert.Contains(text.Inlines, inline => inline is Bold);
        Assert.DoesNotContain("第五行不应出现", new TextRange(text.ContentStart, text.ContentEnd).Text);
        Assert.InRange(preview.ActualHeight, 1, 68.1);
    }

    private static void VerifyCalendarMonthLayout()
    {
        var calendar = new CalendarView();
        var days = Enumerable.Range(0, 42).Select(index =>
        {
            var day = new CalendarDayItem
            {
                Date = new DateTime(2026, 9, 1).AddDays(index), IsCurrentMonth = true, TotalTaskCount = 1
            };
            day.VisibleTaskChips.Add(new CalendarTaskChip
            {
                Task = new TaskItem { Title = "当天完成任务", Description = Details, Status = TaskStatus.Done }
            });
            return day;
        }).ToArray();
        var panelFactory = new FrameworkElementFactory(typeof(CalendarMonthPanel));
        panelFactory.SetValue(CalendarMonthPanel.ViewportHeightProperty, 480d);
        var items = new ItemsControl
        {
            ItemsSource = days,
            ItemTemplate = (DataTemplate)calendar.Resources["CalendarDayTemplate"],
            ItemsPanel = new ItemsPanelTemplate(panelFactory)
        };
        var viewer = new ScrollViewer
        {
            Content = items, Height = 480,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible
        };
        Layout(viewer, 1050);
        var panel = Find<CalendarMonthPanel>(viewer)!;
        Assert.NotNull(panel);
        Assert.Equal(42, panel.Children.Count);
        Assert.Equal(480, panel.DesiredSize.Height, precision: 2);

        // Last week is the hardest case: all four lines must fit inside its own
        // enlarged day, and the scroll extent must include the enlarged week.
        var presenter = (ContentPresenter)panel.Children[38];
        var dayBorder = Find<Border>(presenter)!;
        var chip = FindAll<Border>(presenter).Single(border => border.Name == "MonthTaskCard");
        var preview = FindAll<MarkdownPreviewControl>(chip).Single(control => control.PreviewMaxHeight == 64);
        SetMouseOver(presenter, true);
        SetMouseOver(chip, true);
        preview.GetBindingExpression(MarkdownPreviewControl.IsPreviewExpandedProperty)!.UpdateTarget();
        panel.InvalidateMeasure();
        Layout(viewer, 1050);
        Assert.True(panel.DesiredSize.Height > 480,
            $"Panel={panel.DesiredSize.Height}, presenter-hover={presenter.IsMouseOver}, chip-hover={chip.IsMouseOver}, preview-expanded={preview.IsPreviewExpanded}, preview={preview.Visibility}/{preview.ActualHeight}, day={dayBorder.ActualHeight}, presenter-desired={presenter.DesiredSize.Height}");
        Assert.True(viewer.ExtentHeight > viewer.ViewportHeight);
        Assert.Equal(64, preview.ActualHeight, precision: 2);
        Assert.True(dayBorder.ActualHeight > 80);
        var previewBottom = preview.TransformToAncestor(dayBorder).Transform(new Point(0, preview.ActualHeight)).Y;
        Assert.True(previewBottom < dayBorder.ActualHeight - 10, "月格详情不得被格子边界或底部计数裁剪");
        Assert.Equal(80, ((FrameworkElement)panel.Children[0]).ActualHeight, precision: 2);
        viewer.ScrollToBottom();
        Layout(viewer, 1050);
        SaveVisual(viewer, "Calendar-month-hover");

        SetMouseOver(presenter, false);
        SetMouseOver(chip, false);
        preview.GetBindingExpression(MarkdownPreviewControl.IsPreviewExpandedProperty)!.UpdateTarget();
        panel.InvalidateMeasure();
        Layout(viewer, 1050);
        Assert.Equal(Visibility.Collapsed, preview.Visibility);
        Assert.Equal(480, panel.DesiredSize.Height, precision: 2);
        Assert.Equal(80, presenter.ActualHeight, precision: 2);
    }

    private static void VerifyDoneDecoration()
    {
        foreach (var status in Enum.GetValues<KanbanForOne.Models.TaskStatus>())
        {
            var task = new TaskItem { Title = "状态标题", Status = status };
            var card = new TaskCardControl { DataContext = task };
            Layout(card);
            var title = FindAll<TextBlock>(card).Single(text => text.Text == task.Title && HasVisibleAncestors(text));
            if (status == KanbanForOne.Models.TaskStatus.Done)
                Assert.Contains(title.TextDecorations, decoration => decoration.Location == TextDecorationLocation.Strikethrough);
            else
                Assert.True(title.TextDecorations is null || title.TextDecorations.Count == 0);
        }
    }

    private static void SetHover(UIElement target, bool value, FrameworkElement root)
    {
        // Exercise the actual IsMouseOver binding and layout without moving the user's cursor.
        SetMouseOver(target, value);
        foreach (var preview in FindAll<MarkdownPreviewControl>(root))
            preview.GetBindingExpression(MarkdownPreviewControl.IsPreviewExpandedProperty)?.UpdateTarget();
        Layout(root);
    }

    private static void SetMouseOver(UIElement element, bool value)
    {
        // WPF's input manager updates both the dependency property (used by XAML)
        // and its fast CLR getter cache (used by panels); mirror both for detached trees.
        WriteCoreFlag.Invoke(element, [MouseOverFlag, value]);
        element.SetValue(MouseOverKey, value);
    }

    private static void Layout(FrameworkElement element, double width = 300)
    {
        // Resolve the namescopes and template bindings of detached preview trees.
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
        element.Measure(new Size(width, double.PositiveInfinity));
        element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height));
        element.UpdateLayout();
    }

    private static T? Find<T>(DependencyObject root) where T : DependencyObject => FindAll<T>(root).FirstOrDefault();

    private static IEnumerable<MarkdownPreviewControl> VisiblePreviews(DependencyObject root) =>
        FindAll<MarkdownPreviewControl>(root).Where(HasVisibleAncestors);

    private static string VisibleText(DependencyObject root) => string.Join("\n",
        FindAll<TextBlock>(root).Where(HasVisibleAncestors)
            .Select(text => new TextRange(text.ContentStart, text.ContentEnd).Text));

    private static bool HasVisibleAncestors(DependencyObject element)
    {
        // Detached test trees have IsVisible=false even when their own layout is
        // visible. Check the complete Visibility chain instead, including the
        // inactive branch of the completed/unfinished task presentation.
        for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement visual && visual.Visibility != Visibility.Visible) return false;
        return true;
    }

    private static IEnumerable<T> FindAll<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        foreach (var descendant in FindAll<T>(VisualTreeHelper.GetChild(root, index))) yield return descendant;
    }

    private static void SavePreview()
    {
        var output = Environment.GetEnvironmentVariable("KANBAN_UI_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;

        Brush Brush(string key) => (Brush)Application.Current.FindResource(key);
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "未完成任务常驻详情 · 完成与备忘录悬停预览", FontSize = 23, FontWeight = FontWeights.SemiBold,
            Foreground = Brush("TextPrimaryBrush"), Margin = new Thickness(0, 0, 0, 18)
        });
        var grid = new Grid();
        for (var column = 0; column < 3; column++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var states = new[] { "默认状态", "鼠标悬停", "鼠标移开" };
        for (var column = 0; column < states.Length; column++)
        {
            var label = new TextBlock { Text = states[column], Foreground = Brush("TextSecondaryBrush"), Margin = new Thickness(0, 0, 0, 14) };
            Grid.SetColumn(label, column);
            grid.Children.Add(label);
        }

        var titles = new[] { "待办 · 整理本周工作清单", "进行中 · 更新管道布置图", "卡住 · 等待供应商确认尺寸", "完成 · 发布本周设计成果", "备忘录 · 周会要点", "归档完成 · 项目资料整理" };
        var details = "第一行：确认本周交付安排\n第二行：核对设备接口清单\n第三行：整理专业协调意见\n第四行：更新项目归档记录\n第五行：本行不应出现";
        for (var row = 0; row < titles.Length; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var column = 0; column < 3; column++)
            {
                var task = CreateTask((TaskStatus)Math.Min(row, 3), row == 5);
                task.Title = titles[row];
                task.Description = details;
                FrameworkElement card = row == 4
                    ? new NoteCardControl { DataContext = new NoteItem { Title = titles[row], Content = details } }
                    : new TaskCardControl
                    {
                        IsCompact = row == 5,
                        DataContext = task
                    };
                card.Margin = new Thickness(0, 0, 18, 4);
                card.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(card, column);
                Grid.SetRow(card, row + 1);
                grid.Children.Add(card);
                Layout(card);
                if (column > 0) SetHover(card, true, card);
                if (column == 2) SetHover(card, false, card);
            }
        }
        content.Children.Add(grid);
        var surface = new Border { Background = Brush("MainContentBackgroundBrush"), Padding = new Thickness(28), Child = content };
        Layout(surface, 1100);
        SaveVisual(surface, "Card-hover");
    }

    private static void SaveVisual(FrameworkElement element, string name)
    {
        var output = Environment.GetEnvironmentVariable("KANBAN_UI_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(output);
        var isDark = ((SolidColorBrush)Application.Current.FindResource("MainContentBackgroundBrush")).Color.R < 100;
        using var stream = File.Create(Path.Combine(output, $"{(isDark ? "Dark" : "Light")}-{name}.png"));
        encoder.Save(stream);
    }
}
