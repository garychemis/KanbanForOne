using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KanbanForOne.Controls;
using KanbanForOne.Models;
using KanbanForOne.ViewModels;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Modules.DesignConditions.Data;
using KanbanForOne.Modules.DesignConditions.Repositories;
using KanbanForOne.Modules.DesignConditions.Services;
using KanbanForOne.Modules.DesignConditions.Views;
using KanbanForOne.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace KanbanForOne.Tests;

/// <summary>
/// 归档页与人工时汇总页 UI 冒烟测试（STA）。
/// 单测试类单方法：WPF Application 在同一 AppDomain 只能实例化一次，因此两个页面验证
/// 必须在同一条 STA 线程、同一个窗口实例中依次完成。
/// 依赖桌面会话（WPF 渲染），在无头 CI 中会失败。
/// </summary>
public sealed class UiSmokeTests
{
    private static Exception? _threadException;
    private static string _result = string.Empty;

    [Fact]
    public void Archive_and_summary_pages_render()
    {
        var thread = new Thread(Run) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "STA thread timed out");
        if (_threadException is not null)
        {
            throw new Exception("STA thread failed", _threadException);
        }

        Assert.Equal("ALL OK", _result);
    }

    private static void Run()
    {
        _threadException = null;
        _result = string.Empty;
        string? dataDir = null;
        try
        {
            PrepareTrayIcon();
            // 清理测试输出目录下的数据，保证每次测试从干净数据库启动
            // 防御：确认目标路径位于测试输出目录内，避免误删仓库数据
            var baseDir = Path.GetFullPath(AppContext.BaseDirectory);
            dataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data"));
            Assert.True(
                dataDir.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase),
                "测试数据目录必须位于测试输出目录内");
            if (Directory.Exists(dataDir))
            {
                Directory.Delete(dataDir, recursive: true);
            }

            App.BuildServiceProvider();
            var app = new App();
            app.InitializeComponent();
            VerifyDesignConditionDrawingGridStyles();

            // Capture the same container as this window: App startup can replace
            // the static provider when the test subsequently pumps the dispatcher.
            var notifications = App.Services.GetRequiredService<NotificationService>();
            var window = new MainWindow();
            window.Show();
            DoEvents();

            var vm = (MainWindowViewModel)window.DataContext;
            var boardView = FindVisualChild<BoardView>(window);
            Assert.NotNull(boardView);
            var summaryView = FindVisualChild<WorkHourSummaryView>(window);
            Assert.NotNull(summaryView);
            var calendarView = FindVisualChild<CalendarView>(window);
            Assert.NotNull(calendarView);
            var designConditionView = FindVisualChild<DesignConditionView>(window);
            Assert.NotNull(designConditionView);

            // 0) 等待应用初始化完成（默认归档分区已加载）
            WaitUntil(() => vm.Board.ArchiveSections.Count >= 1, timeoutMilliseconds: 10000);

            VerifyArchivePage(vm, boardView);
            VerifySummaryPage(vm, summaryView);
            VerifyCalendarChipRefresh(vm, calendarView);
            VerifyDesignConditionPageAndCalendar(vm, designConditionView, calendarView);
            VerifyUnifiedBackupRoundTrip();
            VerifyWorkspaceLayout(window, vm, notifications);
            VerifyEditors(window, vm);
            SaveCardPalettePreview();
            SaveModuleCardPreview(calendarView);

            window.Close();
            app.Shutdown();
            _result = "ALL OK";
        }
        catch (Exception ex)
        {
            _threadException = ex;
        }
        finally
        {
            try { Application.Current?.Shutdown(); } catch { }
            SqliteConnection.ClearAllPools();
            if (dataDir is not null && Directory.Exists(dataDir))
            {
                try { Directory.Delete(dataDir, recursive: true); } catch { }
            }
        }
    }

    private static void VerifyDesignConditionDrawingGridStyles()
    {
        var resources = new ResourceDictionary
        {
            Source = new Uri(
                "/KanbanForOne;component/Modules/DesignConditions/Views/DesignConditionControlStyles.xaml",
                UriKind.RelativeOrAbsolute)
        };

        Assert.IsType<SolidColorBrush>(resources["ConditionDrawingAlternateRowBrush"]);
        Assert.IsType<Style>(resources["ConditionDrawingGridChromeStyle"]);
        Assert.IsType<Style>(resources["ConditionDrawingDeleteButtonStyle"]);
    }

    private static void VerifyWorkspaceLayout(MainWindow window, MainWindowViewModel vm, NotificationService notifications)
    {
        Assert.Equal(WindowStyle.None, window.WindowStyle);
        foreach (var width in new[] { 1100d, 1512d, 1920d })
        {
            window.Width = width;
            window.Height = width == 1100 ? 720 : 900;
            foreach (var page in new[] { "Board", "Calendar", "WorkHourSummary", "DesignConditions", "Archived", "Backup", "Settings", "About" })
            {
                vm.ChangeFilterCommand.Execute(page);
                notifications.Notify("界面验证通知");
                DoEvents();
                window.UpdateLayout();
                Assert.True(IsVisibleText(window, "界面验证通知"),
                    $"{page}: global notifications must remain visible; current={vm.NotificationText}; matches={FindElementsByText(window, "界面验证通知").Count}");
                var close = FindButtons(window).Single(button =>
                    System.Windows.Automation.AutomationProperties.GetName(button) == "关闭");
                var bounds = close.TransformToAncestor(window).TransformBounds(new Rect(close.RenderSize));
                Assert.True(bounds.Right <= window.ActualWidth && bounds.Left >= 0,
                    $"{page}: close button is outside the window at {width}px");
                if (page == "Board")
                {
                    var column = FindVisualChild<KanbanColumnControl>(window)!;
                    var board = FindVisualChild<BoardView>(window)!;
                    Assert.True(column.ActualHeight <= board.ActualHeight,
                        "Board columns must fit the available vertical space");
                    if (width == 1920)
                        Assert.True(column.ActualWidth > 280, "Columns should expand on a wide workspace");
                    if (width == 1100)
                    {
                        var scroll = (ScrollViewer)board.FindName("BoardScrollViewer");
                        scroll.ScrollToRightEnd();
                        DoEvents();
                        Assert.True(scroll.HorizontalOffset > 0, "Narrow windows must allow access to all five columns");
                        scroll.ScrollToLeftEnd();
                        DoEvents();
                    }
                }
                if (page == "Calendar" && width == 1100)
                {
                    var count = FindElementsByText(window, "条 1").OfType<TextBlock>().FirstOrDefault();
                    Assert.NotNull(count);
                    Assert.True(count.IsVisible, "A compact calendar day must retain its design-condition count");
                    var countBounds = count.TransformToAncestor(window).TransformBounds(new Rect(count.RenderSize));
                    Assert.True(countBounds.Bottom <= window.ActualHeight, "Calendar summaries must fit the short window");
                }
                SavePreview(window, $"{page}-{width:0}");
            }
        }
    }

    private static void SavePreview(FrameworkElement element, string name)
    {
        var directory = Environment.GetEnvironmentVariable("KANBAN_UI_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        // Let popup layouts and the existing 150–220 ms editor animation settle.
        var renderAt = Environment.TickCount64 + 300;
        WaitUntil(() => Environment.TickCount64 >= renderAt);
        element.UpdateLayout();
        Directory.CreateDirectory(directory);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(directory, name + ".png"));
        encoder.Save(output);
    }

    private static void SaveCardPalettePreview()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KANBAN_UI_PREVIEW_DIR"))) return;

        // These samples only belong to this detached visual tree; no workspace or repository is changed.
        Brush BrushResource(string key) => (Brush)Application.Current.FindResource(key);
        TextBlock Label(string text, double size, bool strong = false) => new()
        {
            Text = text,
            FontSize = size,
            FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = BrushResource(strong ? "TextPrimaryBrush" : "TextSecondaryBrush")
        };

        var content = new StackPanel();
        content.Children.Add(Label("卡片配色 · 实际控件预览", 22, strong: true));
        var subtitle = Label("五类卡片并列展示，底部为归档紧凑卡片", 12);
        subtitle.Margin = new Thickness(0, 8, 0, 24);
        content.Children.Add(subtitle);

        var columns = new Grid();
        content.Children.Add(columns);
        var headings = new[] { "待办", "进行中", "卡住", "完成", "备忘录" };
        var titles = new[,]
        {
            { "整理本周工作清单", "补充设备接口资料", "项目资料整理" },
            { "更新管道布置图", "校核设备基础条件", "方案深化记录" },
            { "等待供应商确认尺寸", "待补充上游设计条件", "接口协调记录" },
            { "发布本周设计成果", "完成设备清单复核", "阶段成果交付" },
            { "周会要点", "现场核对提醒", "会议记录备忘" }
        };
        var descriptions = new[,]
        {
            { "梳理交付顺序，确认本周需要协调的事项。", "核对接口清单，准备下一轮专业协同。" },
            { "根据最新设备资料，完善检修与操作空间。", "核对基础标高与定位尺寸，整理校核意见。" },
            { "已发送尺寸确认单，收到回复后继续布置。", "暂缺工艺参数，待专业确认后补充设计。" },
            { "图纸与清单已整理完成，并提交项目归档。", "数量与规格已核对，相关修改同步完成。" },
            { "下次会议确认接口边界，提前汇总待办问题。", "带上最新图纸，重点记录检修通道与净空。" }
        };

        for (var index = 0; index < headings.Length; index++)
        {
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            var cards = new StackPanel();
            var heading = Label(headings[index], 16, strong: true);
            heading.Margin = new Thickness(0, 0, 0, 18);
            cards.Children.Add(heading);

            for (var row = 0; row < 3; row++)
            {
                var compact = row == 2;
                if (compact)
                {
                    var archiveLabel = Label("归档 · 紧凑显示", 11);
                    archiveLabel.Margin = new Thickness(0, 10, 0, 12);
                    cards.Children.Add(archiveLabel);
                }

                FrameworkElement card;
                if (index == 4)
                {
                    card = new NoteCardControl
                    {
                        IsCompact = compact,
                        DataContext = new NoteItem
                        {
                            Title = titles[index, row],
                            Content = compact ? string.Empty : descriptions[index, row],
                            TagsDisplay = "工作记录",
                            IsArchived = compact,
                            UpdatedAt = DateTime.Today
                        }
                    };
                }
                else
                {
                    card = new TaskCardControl
                    {
                        IsCompact = compact,
                        DataContext = new TaskItem
                        {
                            Title = titles[index, row],
                            Description = compact ? string.Empty : descriptions[index, row],
                            Status = (KanbanForOne.Models.TaskStatus)index,
                            Priority = TaskPriority.Medium,
                            StartDate = DateTime.Today,
                            EndDate = DateTime.Today.AddDays(3),
                            TagsDisplay = "项目协同",
                            IsArchived = compact
                        }
                    };
                }
                // Keep the sample rows aligned while allowing each real card to size naturally.
                card.VerticalAlignment = VerticalAlignment.Top;
                cards.Children.Add(new Border { Height = compact ? 110 : 204, Child = card });
            }

            var column = new Border
            {
                Background = BrushResource("ColumnBackgroundBrush"),
                BorderBrush = BrushResource("BorderBrushSoft"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 14, 10, 12),
                Margin = new Thickness(0, 0, index == headings.Length - 1 ? 0 : 12, 0),
                Child = cards
            };
            Grid.SetColumn(column, index);
            columns.Children.Add(column);
        }

        var preview = new Border
        {
            Width = 1512,
            Background = BrushResource("MainContentBackgroundBrush"),
            Padding = new Thickness(24),
            UseLayoutRounding = true,
            Child = content
        };
        System.Windows.Documents.TextElement.SetFontFamily(preview, (FontFamily)Application.Current.FindResource("AppFontFamily"));
        TextOptions.SetTextFormattingMode(preview, TextFormattingMode.Display);
        preview.Measure(new Size(preview.Width, double.PositiveInfinity));
        preview.Arrange(new Rect(new Point(), preview.DesiredSize));
        SavePreview(preview, "Card-palette");
    }

    private static void SaveModuleCardPreview(CalendarView calendar)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("KANBAN_UI_PREVIEW_DIR"))) return;

        // Render the actual calendar templates with in-memory examples only.
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        void AddColumn(int column, string heading, string templateKey, object[] samples)
        {
            var content = new StackPanel { Margin = new Thickness(column == 0 ? 0 : 16, 0, 0, 0) };
            content.Children.Add(new TextBlock
            {
                Text = heading, FontSize = 16, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.FindResource("TextPrimaryBrush"),
                Margin = new Thickness(0, 0, 0, 16)
            });
            foreach (var sample in samples)
            {
                var card = (FrameworkElement)((DataTemplate)calendar.Resources[templateKey]).LoadContent();
                card.DataContext = sample;
                content.Children.Add(card);
            }
            Grid.SetColumn(content, column);
            grid.Children.Add(content);
        }

        AddColumn(0, "人工时 · 赭金边框", "CalendarWorkHourListTemplate",
        [
            new WorkHourEntry { ProjectNumber = "P2026-01", Discipline = "设备", WorkActivity = "图纸校核", HourUnits = 350, Remark = "复核设备基础尺寸与接口条件。" },
            new WorkHourEntry { ProjectNumber = "P2026-02", Discipline = "管道", WorkActivity = "专业协调", HourUnits = 200, Remark = "汇总本轮接口调整意见。" }
        ]);
        AddColumn(1, "设计条件归档 · 青绿边框", "CalendarDesignConditionListTemplate",
        [
            new DesignConditionEntry { ProjectNumber = "P2026-01", ConditionName = "设备基础条件", IssuingDiscipline = "设备", ReceivingDiscipline = "土建", DrawingSize = "A1", DrawingCount = 3 },
            new DesignConditionEntry { ProjectNumber = "P2026-02", ConditionName = "管道接口条件", IssuingDiscipline = "管道", ReceivingDiscipline = "设备", DrawingSize = "A2", DrawingCount = 2 }
        ]);

        var preview = new UserControl
        {
            Width = 960,
            DataContext = calendar.DataContext,
            FontFamily = (FontFamily)Application.Current.FindResource("AppFontFamily"),
            Content = new Border
            {
                Background = (Brush)Application.Current.FindResource("MainContentBackgroundBrush"),
                Padding = new Thickness(24), Child = grid
            },
            UseLayoutRounding = true
        };
        preview.Measure(new Size(preview.Width, double.PositiveInfinity));
        preview.Arrange(new Rect(new Point(), preview.DesiredSize));
        SavePreview(preview, "Module-cards");
    }

    private static void VerifyEditors(MainWindow window, MainWindowViewModel vm)
    {
        window.Width = 1512;
        window.Height = 900;
        vm.ChangeFilterCommand.Execute("Board");
        DoEvents();
        vm.Board.OpenTaskCommand.Execute(vm.Board.AllTasks.First());
        WaitUntil(() => vm.Board.IsSpotlightOpen);
        DoEvents();
        SavePreview(window, "Task-details");
        vm.Board.CloseSpotlightCommand.Execute(null);
        DoEvents();
        vm.Board.CreateNoteCommand.Execute(KanbanColumnKind.Notes);
        WaitUntil(() => vm.Board.IsSpotlightOpen);
        SavePreview(window, "Note-details");
        vm.Board.CloseSpotlightCommand.Execute(null);
        DoEvents();

        var editorVm = new KanbanForOne.Modules.DesignConditions.ViewModels.DesignConditionEditorViewModel(
            null, DateTime.Today, new[] { "工艺", "设备" }, new[] { "测试人员" });
        var condition = (DesignConditionEditorDialog)Activator.CreateInstance(
            typeof(DesignConditionEditorDialog), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { editorVm, App.Services.GetRequiredService<DesignConditionAttachmentStorageService>() }, null)!;
        condition.Owner = window;
        condition.Show();
        DoEvents();
        Assert.Equal(WindowStyle.None, condition.WindowStyle);
        Assert.True(IsVisibleText(condition, "设计条件详情"));
        var combo = FindVisualChild<ComboBox>(condition)!;
        combo.IsDropDownOpen = true;
        DoEvents();
        Assert.True(((Popup)combo.Template.FindName("PART_Popup", combo)).IsOpen);
        combo.SelectedIndex = 0;
        combo.IsDropDownOpen = false;
        Assert.Equal("工艺", editorVm.IssuingDiscipline);
        SavePreview(condition, "Design-condition-editor");
        condition.Close();

        var hours = (WorkHourEntryDialog)Activator.CreateInstance(
            typeof(WorkHourEntryDialog), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, new object?[] { null, DateTime.Today, new[] { "工艺", "设备" }, new[] { "设计", "校核" } }, null)!;
        hours.Owner = window;
        hours.Show();
        DoEvents();
        Assert.Equal(WindowStyle.None, hours.WindowStyle);
        var date = (DatePicker)hours.FindName("WorkDatePicker");
        date.IsDropDownOpen = true;
        DoEvents();
        Assert.True(date.IsDropDownOpen);
        date.IsDropDownOpen = false;
        SavePreview(hours, "Work-hour-editor");
        hours.Close();
        var confirm = new ConfirmDialog("删除记录", "删除后无法撤销，请确认是否继续。", "删除", "取消") { Owner = window };
        confirm.Show();
        DoEvents();
        Assert.Equal(WindowStyle.None, confirm.WindowStyle);
        SavePreview(confirm, "Confirmation");
        confirm.Close();
    }

    private static void VerifyArchivePage(MainWindowViewModel vm, BoardView boardView)
    {
        // 默认看板：归档横条不可见
        Assert.False(IsVisibleText(boardView, "归档"), "归档工具栏在默认视图应隐藏");
        Assert.False(IsVisibleText(boardView, "选择分区"), "分区选择器在默认视图应隐藏");

        // 切到归档视图：横条与选择器出现（等待分区数据加载完成）
        vm.ChangeFilterCommand.Execute("Archived");
        WaitUntil(() => IsVisibleText(boardView, vm.Board.ArchiveSectionSelectorHint));
        Assert.True(IsVisibleText(boardView, "归档"), "归档工具栏应可见");
        Assert.True(IsVisibleText(boardView, "选择分区"), "分区选择器应显示占位文本");
        Assert.True(IsVisibleText(boardView, vm.Board.ArchiveSectionSelectorHint), "分区数提示应显示");
        Assert.Equal("选择分区", vm.Board.ArchiveSectionSelectorText);

        // 归档对话框渲染（非模态展示）
        var defaultSection = vm.Board.ArchiveSections.Single(section => section.IsDefault);
        var dialog = new ArchiveSectionDialog(vm.Board.ArchiveSections, defaultSection);
        dialog.Show();
        DoEvents();
        Assert.True(IsVisibleText(dialog, "选择归档分区"), "归档对话框标题应渲染");
        Assert.True(IsVisibleText(dialog, "默认"), "默认分区标签应渲染");
        Assert.True(IsVisibleText(dialog, "0 项"), "计数徽章应渲染");
        SavePreview(dialog, "Archive-picker");
        dialog.Close();

        // 分区管理对话框渲染：副标题、搜索框、新建输入、默认分区无删除按钮
        var picker = new ArchiveSectionPickerDialog(vm.Board.ArchiveSections, null);
        picker.Show();
        DoEvents();
        Assert.True(IsVisibleText(picker, "搜索、新建或删除分区"), "管理对话框副标题应渲染");
        var textBoxes = FindTextBoxes(picker);
        Assert.True(textBoxes.Count >= 2, "管理对话框应包含搜索框与新建输入框");
        var deleteButtons = FindButtons(picker).Where(button => button.IsVisible && ContainsText(button, "删除")).ToArray();
        Assert.True(deleteButtons.Length == 0, "默认分区不应显示删除按钮");
        SavePreview(picker, "Archive-manager");
        picker.Close();
    }

    private static void VerifySummaryPage(MainWindowViewModel vm, WorkHourSummaryView summaryView)
    {
        // 默认看板：汇总页不可见
        Assert.False(IsVisibleText(summaryView, "人工时汇总"), "汇总页在默认视图应隐藏");

        // 切到汇总视图：等待页面数据加载完成
        vm.ChangeFilterCommand.Execute("WorkHourSummary");
        WaitUntil(() => IsVisibleText(summaryView, "人工时汇总"));

        // 页面标题与副标题
        Assert.True(IsVisibleText(summaryView, "人工时汇总"), "页面标题应渲染");
        Assert.True(IsVisibleText(summaryView, "项目、专业与工作内容的工时构成"), "页面副标题应渲染");

        // 统计卡
        Assert.True(IsVisibleText(summaryView, "总工时"), "统计卡-总工时应渲染");
        Assert.True(IsVisibleText(summaryView, "原始记录"), "统计卡-原始记录应渲染");
        Assert.True(IsVisibleText(summaryView, "汇总组合"), "统计卡-汇总组合应渲染");

        // 查询模式切换
        Assert.True(IsVisibleText(summaryView, "按月"), "按月模式按钮应渲染");
        Assert.True(IsVisibleText(summaryView, "日期区间"), "日期区间按钮应渲染");
        Assert.True(IsVisibleText(summaryView, "本月"), "本月按钮应渲染");

        // 导出按钮
        Assert.True(IsVisibleText(summaryView, "导出 Excel"), "导出按钮应渲染");

        // 双面板与空态（干净库无数据）
        Assert.True(IsVisibleText(summaryView, "工时分布"), "工时分布面板应渲染");
        Assert.True(IsVisibleText(summaryView, "汇总明细"), "汇总明细面板应渲染");
        Assert.True(IsVisibleText(summaryView, "当前范围没有人工时"), "空态提示应渲染");
    }

    private static void VerifyCalendarChipRefresh(MainWindowViewModel vm, CalendarView calendarView)
    {
        // 回看板视图，创建一个无日期任务并赋予今天的日期
        vm.ChangeFilterCommand.Execute("Board");
        DoEvents();
        vm.Board.CreateTaskCommand.Execute(KanbanColumnKind.Todo);
        DoEvents();

        var task = vm.Board.AllTasks.FirstOrDefault(item => item.Title == "新任务");
        Assert.NotNull(task);
        task.StartDate = DateTime.Today;
        task.EndDate = DateTime.Today;

        // 切到日历视图：芯片应显示任务标题
        vm.ChangeFilterCommand.Execute("Calendar");
        WaitUntil(() => IsVisibleText(calendarView, "新任务"));

        // 有搜索词时，标题变化会触发日历刷新链（ShouldRefreshCalendarForTaskChange 返回 true）；
        // UpdatedAt 变化应触发网格重建，芯片立即显示新标题而不是旧快照。
        vm.SearchText = "任务";
        WaitUntil(() => IsVisibleText(calendarView, "新任务"));
        task.Title = "日历冒烟-已改";
        WaitUntil(() => IsVisibleText(calendarView, "日历冒烟-已改"));
        Assert.False(IsVisibleText(calendarView, "新任务"), "旧标题不应继续显示");
    }

    private static void VerifyDesignConditionPageAndCalendar(MainWindowViewModel vm, DesignConditionView designConditionView, CalendarView calendarView)
    {
        vm.SearchText = string.Empty;
        vm.ChangeFilterCommand.Execute("DesignConditions");
        WaitUntil(() => IsVisibleText(designConditionView, "设计条件归档"));
        Assert.True(IsVisibleText(designConditionView, "+ 新建设计条件"), "设计条件页面应提供创建入口");
        Assert.True(IsVisibleText(designConditionView, "图纸量汇总"), "设计条件页面应提供汇总入口");

        var repository = App.Services.GetRequiredService<DesignConditionRepository>();
        WaitForTask(repository.UpsertAsync(new DesignConditionEntry
        {
            ProjectNumber = "UI100",
            IssuingDiscipline = "工艺",
            ReceivingDiscipline = "设备",
            Receiver = "测试人员",
            IssuedDate = DateTime.Today,
            ConditionName = "日历设计条件",
            Revision = "A",
            DrawingSize = "A1|A4",
            DrawingCounts = "1|2",
            DrawingCount = 3,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        }));
        WaitForTask(vm.DesignConditions.ReloadAsync());
        WaitUntil(() => IsVisibleText(designConditionView, "A1 × 1 · A4 × 2"));

        vm.ChangeFilterCommand.Execute("Calendar");
        WaitForTask(vm.Calendar.DesignConditions.LoadRangeAsync(DateTime.Today.AddDays(-10), DateTime.Today.AddDays(31)));
        WaitForTask(vm.Calendar.DesignConditions.LoadSelectedDateAsync(DateTime.Today));
        WaitUntil(() => IsVisibleText(calendarView, "日历设计条件"));
        Assert.True(IsVisibleText(calendarView, "设计条件"), "日历详情应显示设计条件区域");
    }

    private static void VerifyUnifiedBackupRoundTrip()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), "KanbanForOne.UiSmoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceRoot);
        try
        {
            var taskRepository = App.Services.GetRequiredService<TaskRepository>();
            var attachmentRepository = App.Services.GetRequiredService<AttachmentRepository>();
            var attachmentStorage = App.Services.GetRequiredService<AttachmentStorageService>();
            var designRepository = App.Services.GetRequiredService<DesignConditionRepository>();
            var designAttachmentRepository = App.Services.GetRequiredService<DesignConditionAttachmentRepository>();
            var designStorage = App.Services.GetRequiredService<DesignConditionAttachmentStorageService>();
            var backup = App.Services.GetRequiredService<UnifiedBackupService>();

            var task = new TaskItem
            {
                Title = "完整备份-核心数据",
                Status = KanbanForOne.Models.TaskStatus.Todo,
                Priority = TaskPriority.Medium
            };
            WaitForTask(taskRepository.UpsertAsync(task));
            var coreSource = Path.Combine(sourceRoot, "core.txt");
            File.WriteAllText(coreSource, "core attachment");
            var coreAttachment = Assert.Single(WaitForTask(
                attachmentStorage.CopyFilesAsync(AttachmentOwnerType.Task, task.Id, [coreSource])));
            WaitForTask(attachmentRepository.AddRangeAsync([coreAttachment]));

            var design = new DesignConditionEntry
            {
                ProjectNumber = "BACKUP100",
                IssuingDiscipline = "工艺",
                ReceivingDiscipline = "管道",
                Receiver = "恢复测试",
                IssuedDate = DateTime.Today,
                ConditionName = "完整备份-设计条件",
                Revision = "A",
                DrawingSize = "A1",
                DrawingCount = 1,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            WaitForTask(designRepository.UpsertAsync(design));
            var designSource = Path.Combine(sourceRoot, "design.dwg");
            File.WriteAllText(designSource, "design attachment");
            var designAttachment = Assert.Single(WaitForTask(designStorage.CopyFilesAsync(design.Id, [designSource])));
            WaitForTask(designAttachmentRepository.AddRangeAsync([designAttachment]));

            var created = WaitForTask(backup.CreateBackupAsync());
            Assert.True(File.Exists(created.BackupPath));

            WaitForTask(attachmentRepository.DeleteByOwnerAsync(AttachmentOwnerType.Task, task.Id));
            WaitForTask(taskRepository.DeleteAsync(task.Id));
            File.Delete(attachmentStorage.GetAbsolutePath(coreAttachment));
            var stagedDesignFiles = designStorage.StageConditionFolderDelete(design.Id);
            WaitForTask(designRepository.DeleteAsync(design.Id));
            DesignConditionAttachmentStorageService.CommitDelete(stagedDesignFiles);

            var restored = WaitForTask(backup.RestoreBackupAsync(created.BackupPath));
            Assert.True(File.Exists(restored.ProtectiveBackupPath));
            Assert.Contains(WaitForTask(taskRepository.GetAllAsync()), item => item.Id == task.Id);
            var restoredCoreAttachment = Assert.Single(
                WaitForTask(attachmentRepository.GetByOwnerIdsAsync(AttachmentOwnerType.Task, [task.Id])));
            Assert.True(File.Exists(attachmentStorage.GetAbsolutePath(restoredCoreAttachment)));
            var restoredDesign = Assert.Single(
                WaitForTask(designRepository.GetAllAsync()), item => item.Id == design.Id);
            Assert.True(File.Exists(designStorage.GetAbsolutePath(Assert.Single(restoredDesign.Attachments))));
        }
        finally
        {
            if (Directory.Exists(sourceRoot)) Directory.Delete(sourceRoot, recursive: true);
        }
    }

    private static bool ContainsText(Button button, string text)
    {
        return FindElementsByText(button, text).Count > 0;
    }

    private static List<Button> FindButtons(DependencyObject root)
    {
        var results = new List<Button>();

        if (root is Button button)
        {
            results.Add(button);
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            results.AddRange(FindButtons(VisualTreeHelper.GetChild(root, i)));
        }

        return results;
    }

    private static List<TextBox> FindTextBoxes(DependencyObject root)
    {
        var results = new List<TextBox>();

        if (root is TextBox textBox)
        {
            results.Add(textBox);
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            results.AddRange(FindTextBoxes(VisualTreeHelper.GetChild(root, i)));
        }

        return results;
    }

    private static bool IsVisibleText(DependencyObject root, string text)
    {
        return FindElementsByText(root, text).Any(element =>
            element is TextBlock { IsVisible: true }
            || element is ButtonBase { IsVisible: true });
    }

    private static List<DependencyObject> FindElementsByText(DependencyObject root, string text)
    {
        var results = new List<DependencyObject>();

        if (root is TextBlock textBlock && textBlock.Text == text)
        {
            results.Add(textBlock);
        }

        if (root is ButtonBase buttonBase && buttonBase.Content is string content && content == text)
        {
            results.Add(buttonBase);
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            results.AddRange(FindElementsByText(VisualTreeHelper.GetChild(root, i), text));
        }

        return results;
    }

    private static T? FindVisualChild<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
            {
                return typed;
            }

            var found = FindVisualChild<T>(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static void PrepareTrayIcon()
    {
        var iconDir = Path.Combine(AppContext.BaseDirectory, "icon");
        Directory.CreateDirectory(iconDir);
        var sourceIcon = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "icon", "icon-transparent.ico"));
        if (File.Exists(sourceIcon))
        {
            File.Copy(sourceIcon, Path.Combine(iconDir, "icon-transparent.ico"), overwrite: true);
        }
    }

    private static void WaitUntil(Func<bool> condition, int timeoutMilliseconds = 5000)
    {
        var deadline = Environment.TickCount + timeoutMilliseconds;

        while (!condition())
        {
            DoEvents();
            Thread.Sleep(10);

            if (Environment.TickCount > deadline)
            {
                throw new TimeoutException($"等待条件超时（{timeoutMilliseconds} ms）");
            }
        }
    }

    private static void WaitForTask(Task task, int timeoutMilliseconds = 10000)
    {
        WaitUntil(() => task.IsCompleted, timeoutMilliseconds);
        task.GetAwaiter().GetResult();
    }

    private static T WaitForTask<T>(Task<T> task, int timeoutMilliseconds = 10000)
    {
        WaitUntil(() => task.IsCompleted, timeoutMilliseconds);
        return task.GetAwaiter().GetResult();
    }

    private static void DoEvents()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
}
