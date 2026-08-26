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
