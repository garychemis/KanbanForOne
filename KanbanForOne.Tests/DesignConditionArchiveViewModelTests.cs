using System.IO;
using KanbanForOne.Models;
using KanbanForOne.Modules.DesignConditions.Data;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Modules.DesignConditions.Repositories;
using KanbanForOne.Modules.DesignConditions.Services;
using KanbanForOne.Modules.DesignConditions.ViewModels;
using KanbanForOne.Services;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Tests;

public sealed class DesignConditionArchiveViewModelTests
{
    [Fact]
    public void Received_editor_preserves_kind_and_both_dates_when_opened_from_another_tab()
    {
        var source = Entry(DesignConditionArchiveKind.Received, "R100", new DateTime(2026, 9, 25), new DateTime(2026, 10, 1));
        var attachment = new DesignConditionAttachment { DesignConditionId = source.Id, OriginalFileName = "接受条件.pdf" };
        source.Attachments.Add(attachment);
        var editor = new DesignConditionEditorViewModel(source, DateTime.Today, [], [], DesignConditionArchiveKind.Issued);

        Assert.True(editor.IsReceived);
        editor.ReceivedDate = new DateTime(2026, 10, 2);
        Assert.True(editor.TryBuild(out var rebuilt));
        Assert.Equal(source.Id, rebuilt.Id);
        Assert.Equal(DesignConditionArchiveKind.Received, rebuilt.ArchiveKind);
        Assert.Equal(source.IssuedDate, rebuilt.IssuedDate);
        Assert.Equal(new DateTime(2026, 10, 2), rebuilt.ReceivedDate);
        Assert.Same(attachment, Assert.Single(rebuilt.Attachments));

        editor.ReceivedDate = null;
        Assert.False(editor.TryBuild(out _));
        Assert.Equal("请选择接受日期。", editor.ValidationMessage);
    }

    [Fact]
    public void New_received_editor_defaults_both_dates_but_legacy_constructor_remains_issued()
    {
        var date = new DateTime(2026, 10, 1);
        var received = new DesignConditionEditorViewModel(null, date, [], [], DesignConditionArchiveKind.Received);
        Assert.Equal(date, received.IssuedDate);
        Assert.Equal(date, received.ReceivedDate);
        Assert.Equal("接受日期", received.DateLabel);

        var issued = new DesignConditionEditorViewModel(null, date, [], []);
        Assert.Equal(DesignConditionArchiveKind.Issued, issued.ArchiveKind);
        Assert.Null(issued.ReceivedDate);
    }

    [Fact]
    public async Task Tabs_keep_separate_filters_options_and_summary_modes()
    {
        using var context = await TestContext.CreateAsync();
        var date = new DateTime(2026, 10, 1);
        await context.Repository.UpsertAsync(Entry(DesignConditionArchiveKind.Issued, "I100", date));
        await context.Repository.UpsertAsync(Entry(DesignConditionArchiveKind.Issued, "I200", date.AddDays(-1)));
        await context.Repository.UpsertAsync(Entry(DesignConditionArchiveKind.Received, "R100", date.AddDays(-5), date));
        await context.ViewModel.EnsureLoadedAsync();

        var vm = context.ViewModel;
        Assert.Equal(2, vm.IssuedRecordCount);
        Assert.Equal(1, vm.ReceivedRecordCount);
        Assert.Equal(2, vm.Entries.Count);
        vm.SelectedProject = "I100";
        vm.Keyword = "设备";
        vm.IssuedStartDate = date;
        vm.SetViewCommand.Execute("Summary");

        vm.SelectedArchiveTabIndex = 1;
        Assert.True(vm.IsReceivedArchive);
        Assert.False(vm.ShowSummary);
        Assert.Equal(string.Empty, vm.SelectedProject);
        Assert.Equal(string.Empty, vm.Keyword);
        Assert.Null(vm.IssuedStartDate);
        Assert.Equal("R100", Assert.Single(vm.Entries).ProjectNumber);
        Assert.Equal("接收专业", vm.SummaryItems.Single(item => item.LevelDepth == 1).Level);
        Assert.Equal("设备", vm.SummaryItems.Single(item => item.LevelDepth == 1).Name);
        Assert.Equal("提出专业", vm.SummaryItems.Single(item => item.LevelDepth == 2).Level);
        Assert.DoesNotContain("I100", vm.ProjectOptions);
        Assert.Single(vm.ProjectOptions, item => item == string.Empty);
        vm.SelectedProject = "R100";
        vm.IssuedStartDate = date;
        vm.IssuedEndDate = date;
        Assert.Single(vm.Entries); // 接受日期命中，提出日期在范围外。

        vm.SelectedArchiveTabIndex = 0;
        Assert.True(vm.ShowSummary);
        Assert.Equal("I100", vm.SelectedProject);
        Assert.Equal("设备", vm.Keyword);
        Assert.Equal(date, vm.IssuedStartDate);
        Assert.Equal("I100", Assert.Single(vm.Entries).ProjectNumber);

        vm.ClearFiltersCommand.Execute(null);
        Assert.Equal(2, vm.Entries.Count);
        vm.SelectedArchiveKind = DesignConditionArchiveKind.Received;
        Assert.Equal("R100", vm.SelectedProject);
        Assert.Equal(date, vm.IssuedEndDate);
        Assert.Single(vm.Entries);
        Assert.Equal(2, vm.IssuedRecordCount);
    }

    [Fact]
    public async Task Calendar_loads_received_on_received_date_and_keeps_both_creation_commands_explicit()
    {
        using var context = await TestContext.CreateAsync();
        var issuedDate = new DateTime(2026, 9, 25);
        var receivedDate = new DateTime(2026, 10, 1);
        await context.Repository.UpsertAsync(Entry(DesignConditionArchiveKind.Received, "R100", issuedDate, receivedDate));
        context.ViewModel.SelectedArchiveTabIndex = 1;
        var calendar = new DesignConditionCalendarSectionViewModel(context.ViewModel, new NotificationService());
        await calendar.InitializeAsync(issuedDate, receivedDate, receivedDate);
        Assert.True(Assert.Single(calendar.SelectedEntries).IsReceived);
        Assert.Equal(1, calendar.GetSummary(receivedDate)?.RecordCount);
        Assert.Null(calendar.GetSummary(issuedDate));

        await ExecuteAsync(calendar.AddCommand);
        Assert.Equal(DesignConditionArchiveKind.Issued, context.Dialogs.LastEditor?.ArchiveKind);
        Assert.Equal(receivedDate, context.Dialogs.LastEditor?.IssuedDate);
        await ExecuteAsync(calendar.AddReceivedCommand);
        Assert.Equal(DesignConditionArchiveKind.Received, context.Dialogs.LastEditor?.ArchiveKind);
        Assert.Equal(receivedDate, context.Dialogs.LastEditor?.ReceivedDate);
    }

    [Fact]
    public async Task New_command_uses_current_tab_and_edit_events_use_received_dates()
    {
        using var context = await TestContext.CreateAsync();
        var source = Entry(DesignConditionArchiveKind.Received, "R100", new DateTime(2026, 9, 25), new DateTime(2026, 10, 1));
        await context.Repository.UpsertAsync(source);
        await context.ViewModel.EnsureLoadedAsync();
        context.ViewModel.SelectedArchiveTabIndex = 1;
        await ExecuteAsync(context.ViewModel.NewCommand);
        Assert.True(context.Dialogs.LastEditor?.IsReceived);

        context.ViewModel.SelectedArchiveTabIndex = 0;
        context.Dialogs.OnEdit = editor =>
        {
            editor.ReceivedDate = new DateTime(2026, 10, 3);
            return DesignConditionEditorAction.Save;
        };
        DesignConditionChangedEventArgs? changed = null;
        context.ViewModel.DataChanged += (_, args) => changed = args;
        await ExecuteAsync(context.ViewModel.OpenCommand, source);

        var saved = Assert.Single(await context.Repository.GetAllAsync());
        Assert.True(saved.IsReceived);
        Assert.Equal(source.IssuedDate, saved.IssuedDate);
        Assert.Equal(new DateTime(2026, 10, 3), saved.ReceivedDate);
        Assert.NotNull(changed);
        Assert.Equal(new DateTime(2026, 10, 1), changed.OldIssuedDate);
        Assert.Equal(new DateTime(2026, 10, 3), changed.NewIssuedDate);
        Assert.Empty(context.ViewModel.Entries);
    }

    [Fact]
    public async Task Data_reset_clears_both_archive_statistics_and_disables_export_until_reload_succeeds()
    {
        using var context = await TestContext.CreateAsync();
        var date = new DateTime(2026, 10, 1);
        await context.Repository.UpsertAsync(Entry(DesignConditionArchiveKind.Issued, "I100", date));
        await context.Repository.UpsertAsync(Entry(DesignConditionArchiveKind.Received, "R100", date, date));
        var vm = context.ViewModel;
        await vm.EnsureLoadedAsync();
        vm.SelectedProject = "I100";
        Assert.True(vm.ExportCommand.CanExecute(null));
        var exportStates = new List<bool>();
        vm.ExportCommand.CanExecuteChanged += (_, _) => exportStates.Add(vm.ExportCommand.CanExecute(null));

        vm.NotifyDataReset();

        Assert.Empty(vm.Entries);
        Assert.Empty(vm.SummaryItems);
        Assert.Equal(0, vm.TotalRecordCount);
        Assert.Equal(0, vm.TotalDrawingCount);
        Assert.Equal(0, vm.TotalAttachmentCount);
        Assert.Equal(0, vm.IssuedRecordCount);
        Assert.Equal(0, vm.ReceivedRecordCount);
        Assert.Equal(0, vm.SummaryItemCount);
        Assert.Equal("I100", vm.SelectedProject);
        Assert.False(vm.ExportCommand.CanExecute(null));
        Assert.Contains(false, exportStates);

        context.ControlledRepository.GetAllOverride = () => Task.FromException<IReadOnlyList<DesignConditionEntry>>(new IOException("模拟恢复后读取失败"));
        await vm.EnsureLoadedAsync();
        Assert.True(vm.HasError);
        Assert.False(vm.IsLoading);
        Assert.False(vm.ExportCommand.CanExecute(null));
        vm.ExportCommand.Execute(null);
        Assert.Equal(0, context.FilePickers.SaveCallCount);

        context.ControlledRepository.GetAllOverride = null;
        await vm.EnsureLoadedAsync();
        Assert.False(vm.HasError);
        Assert.Single(vm.Entries);
        Assert.Equal(1, vm.IssuedRecordCount);
        Assert.Equal(1, vm.ReceivedRecordCount);
        Assert.True(vm.ExportCommand.CanExecute(null));
        Assert.True(exportStates[^1]);
    }

    [Fact]
    public async Task Export_is_disabled_during_reload_and_failed_reload_does_not_leave_stale_statistics()
    {
        using var context = await TestContext.CreateAsync();
        await context.Repository.UpsertAsync(Entry(DesignConditionArchiveKind.Issued, "I100", new DateTime(2026, 10, 1)));
        var vm = context.ViewModel;
        await vm.EnsureLoadedAsync();
        var gate = new TaskCompletionSource<IReadOnlyList<DesignConditionEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.ControlledRepository.GetAllOverride = () => gate.Task;
        var exportStates = new List<bool>();
        vm.ExportCommand.CanExecuteChanged += (_, _) => exportStates.Add(vm.ExportCommand.CanExecute(null));

        var pending = vm.ReloadAsync();
        Assert.True(vm.IsLoading);
        Assert.False(vm.ExportCommand.CanExecute(null));
        Assert.False(exportStates[^1]);
        vm.ExportCommand.Execute(null);
        Assert.Equal(0, context.FilePickers.SaveCallCount);
        gate.SetResult(await context.Repository.GetAllAsync());
        await pending;
        Assert.True(vm.ExportCommand.CanExecute(null));
        Assert.True(exportStates[^1]);

        context.ControlledRepository.GetAllOverride = () => Task.FromException<IReadOnlyList<DesignConditionEntry>>(new IOException("模拟读取失败"));
        await vm.ReloadAsync();
        Assert.True(vm.HasError);
        Assert.Empty(vm.Entries);
        Assert.Empty(vm.SummaryItems);
        Assert.Equal(0, vm.IssuedRecordCount);
        Assert.Equal(0, vm.TotalDrawingCount);
        Assert.False(vm.ExportCommand.CanExecute(null));
    }

    [Fact]
    public async Task Filtered_empty_state_clears_only_current_tab_and_notifies_its_bindings()
    {
        using var context = await TestContext.CreateAsync();
        var date = new DateTime(2026, 10, 1);
        await context.Repository.UpsertAsync(Entry(DesignConditionArchiveKind.Issued, "I100", date));
        var vm = context.ViewModel;
        await vm.EnsureLoadedAsync();
        var notifications = new HashSet<string>();
        vm.PropertyChanged += (_, args) => { if (args.PropertyName is { } property) notifications.Add(property); };

        vm.Keyword = "没有这条条件";
        Assert.Empty(vm.Entries);
        Assert.True(vm.HasActiveFilters);
        Assert.Equal("没有匹配的条件", vm.EmptyStateTitle);
        Assert.Contains("调整或清除筛选", vm.EmptyStateDescription);
        Assert.Equal("清除筛选", vm.EmptyStateActionLabel);
        Assert.Same(vm.ClearFiltersCommand, vm.EmptyStateActionCommand);
        AssertEmptyStateNotifications(notifications);

        notifications.Clear();
        vm.SelectedArchiveKind = DesignConditionArchiveKind.Received;
        Assert.False(vm.HasActiveFilters);
        Assert.Equal("暂无接受条件", vm.EmptyStateTitle);
        Assert.Equal("新增接受条件", vm.EmptyStateActionLabel);
        Assert.Same(vm.NewCommand, vm.EmptyStateActionCommand);
        AssertEmptyStateNotifications(notifications);
        vm.SelectedProject = "R404";
        vm.EmptyStateActionCommand.Execute(null);
        Assert.False(vm.HasActiveFilters);
        Assert.Same(vm.NewCommand, vm.EmptyStateActionCommand);

        vm.SelectedArchiveKind = DesignConditionArchiveKind.Issued;
        Assert.True(vm.HasActiveFilters);
        Assert.Equal("没有这条条件", vm.Keyword);
        notifications.Clear();
        vm.EmptyStateActionCommand.Execute(null);
        Assert.False(vm.HasActiveFilters);
        Assert.Single(vm.Entries);
        Assert.Equal("新增提出条件", vm.EmptyStateActionLabel);
        AssertEmptyStateNotifications(notifications);

        // 空白搜索并未实际过滤记录，不应显示“清除筛选”。
        vm.Keyword = "  ";
        Assert.False(vm.HasActiveFilters);
    }

    [Theory]
    [InlineData("StartDate")]
    [InlineData("EndDate")]
    [InlineData("Project")]
    [InlineData("IssuingDiscipline")]
    [InlineData("ReceivingDiscipline")]
    public async Task Empty_state_notifies_for_each_filter_even_before_initial_load(string filter)
    {
        using var context = await TestContext.CreateAsync();
        var vm = context.ViewModel;
        var notifications = new HashSet<string>();
        vm.PropertyChanged += (_, args) => { if (args.PropertyName is { } property) notifications.Add(property); };
        switch (filter)
        {
            case "StartDate": vm.IssuedStartDate = new DateTime(2026, 10, 1); break;
            case "EndDate": vm.IssuedEndDate = new DateTime(2026, 10, 1); break;
            case "Project": vm.SelectedProject = "P100"; break;
            case "IssuingDiscipline": vm.SelectedIssuingDiscipline = "工艺"; break;
            case "ReceivingDiscipline": vm.SelectedReceivingDiscipline = "设备"; break;
        }
        Assert.True(vm.HasActiveFilters);
        Assert.Same(vm.ClearFiltersCommand, vm.EmptyStateActionCommand);
        AssertEmptyStateNotifications(notifications);
        vm.ClearFiltersCommand.Execute(null);
        Assert.False(vm.HasActiveFilters);
    }

    private static void AssertEmptyStateNotifications(ISet<string> notifications)
    {
        Assert.Contains(nameof(DesignConditionViewModel.HasActiveFilters), notifications);
        Assert.Contains(nameof(DesignConditionViewModel.EmptyStateTitle), notifications);
        Assert.Contains(nameof(DesignConditionViewModel.EmptyStateDescription), notifications);
        Assert.Contains(nameof(DesignConditionViewModel.EmptyStateActionLabel), notifications);
        Assert.Contains(nameof(DesignConditionViewModel.EmptyStateActionCommand), notifications);
    }

    private static async Task ExecuteAsync(RelayCommand command, object? parameter = null)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = false;
        void OnChanged(object? sender, EventArgs args)
        {
            if (!command.CanExecute(parameter)) started = true;
            else if (started) completed.TrySetResult();
        }
        command.CanExecuteChanged += OnChanged;
        try
        {
            Assert.True(command.CanExecute(parameter));
            command.Execute(parameter);
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { command.CanExecuteChanged -= OnChanged; }
    }

    private static DesignConditionEntry Entry(DesignConditionArchiveKind kind, string project, DateTime issuedDate, DateTime? receivedDate = null) => new()
    {
        ArchiveKind = kind,
        ProjectNumber = project,
        IssuingDiscipline = "工艺",
        ReceivingDiscipline = "设备",
        Receiver = "张三",
        IssuedDate = issuedDate,
        ReceivedDate = receivedDate,
        ConditionName = "设备布置条件",
        DrawingSize = "A1",
        DrawingCounts = "3",
        DrawingCount = 3
    };

    private sealed class TestContext : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "KanbanForOne.ArchiveVm.Tests", Guid.NewGuid().ToString("N"));
        public DesignConditionDatabaseService Database { get; }
        public DesignConditionRepository Repository { get; }
        public ControlledDesignConditionRepository ControlledRepository { get; }
        public RecordingDialogs Dialogs { get; } = new();
        public CancelFilePickers FilePickers { get; } = new();
        public DesignConditionViewModel ViewModel { get; }

        private TestContext()
        {
            var paths = new DesignConditionStorageOptions(_root);
            Database = new DesignConditionDatabaseService(paths);
            Repository = new DesignConditionRepository(Database, new DesignConditionAttachmentRepository(Database));
            ControlledRepository = new ControlledDesignConditionRepository(Repository);
            ViewModel = new DesignConditionViewModel(Database, ControlledRepository, new DesignConditionAttachmentStorageService(paths),
                new DesignConditionOptionRepository(Database), new DesignConditionExportService(), new DesignConditionOperationCoordinator(),
                new NotificationService(), Dialogs, FilePickers);
        }

        public static async Task<TestContext> CreateAsync()
        {
            var context = new TestContext();
            await context.Database.InitializeAsync();
            return context;
        }

        public void Dispose()
        {
            Database.ClearPool();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }

    private sealed class RecordingDialogs : IDialogService
    {
        public DesignConditionEditorViewModel? LastEditor { get; private set; }
        public Func<DesignConditionEditorViewModel, DesignConditionEditorAction>? OnEdit { get; set; }
        public DesignConditionEditorAction ShowDesignConditionEditor(DesignConditionEditorViewModel editor, DesignConditionAttachmentStorageService storage)
        {
            LastEditor = editor;
            return OnEdit?.Invoke(editor) ?? DesignConditionEditorAction.None;
        }
        public bool Confirm(string title, string message, string confirmText) => false;
        public ArchiveSectionDialogResult? PickArchiveSection(IEnumerable<ArchiveSection> sections, ArchiveSection defaultSection) => null;
        public ArchiveSectionDialogResult? PickExistingArchiveSection(IEnumerable<ArchiveSection> sections, ArchiveSection? selectedSection) => null;
        public WorkHourEntryDialogResult? EditWorkHourEntry(WorkHourEntry? entry, DateTime defaultDate, IReadOnlyList<string> disciplines, IReadOnlyList<string> workActivities) => null;
    }

    private sealed class CancelFilePickers : IFilePickerService
    {
        public int SaveCallCount { get; private set; }
        public string? PickOpenFile(string title, string initialDirectory, string filter) => null;
        public IReadOnlyList<string> PickOpenFiles(string title, string initialDirectory, string filter) => [];
        public string? PickSaveFile(string title, string defaultFileName, string filter, string defaultExtension)
        {
            SaveCallCount++;
            return null;
        }
    }

    private sealed class ControlledDesignConditionRepository(IDesignConditionRepository inner) : IDesignConditionRepository
    {
        public Func<Task<IReadOnlyList<DesignConditionEntry>>>? GetAllOverride { get; set; }
        public Task<IReadOnlyList<DesignConditionEntry>> GetAllAsync() => GetAllOverride?.Invoke() ?? inner.GetAllAsync();
        public Task<IReadOnlyList<DesignConditionEntry>> GetByIssuedDateAsync(DateTime date) => inner.GetByIssuedDateAsync(date);
        public Task<IReadOnlyList<DesignConditionEntry>> GetByIssuedDateRangeAsync(DateTime startDate, DateTime endDate) => inner.GetByIssuedDateRangeAsync(startDate, endDate);
        public Task<IReadOnlyList<DesignConditionCalendarSummary>> GetCalendarSummariesAsync(DateTime startDate, DateTime endDate) => inner.GetCalendarSummariesAsync(startDate, endDate);
        public Task UpsertAsync(DesignConditionEntry entry) => inner.UpsertAsync(entry);
        public Task SaveAggregateAsync(DesignConditionEntry entry, IEnumerable<Guid> deletedAttachmentIds, IEnumerable<DesignConditionAttachment> addedAttachments)
            => inner.SaveAggregateAsync(entry, deletedAttachmentIds, addedAttachments);
        public Task DeleteAsync(Guid id) => inner.DeleteAsync(id);
    }
}
