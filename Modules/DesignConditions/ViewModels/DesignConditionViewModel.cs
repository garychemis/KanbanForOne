using System.Collections.ObjectModel;
using KanbanForOne.Modules.DesignConditions.Data;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Modules.DesignConditions.Repositories;
using KanbanForOne.Modules.DesignConditions.Services;
using KanbanForOne.Services;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Modules.DesignConditions.ViewModels;

public sealed class DesignConditionViewModel : ObservableObject
{
    private readonly DesignConditionDatabaseService _database;
    private readonly IDesignConditionRepository _repository;
    private readonly DesignConditionAttachmentStorageService _storage;
    private readonly DesignConditionOptionRepository _options;
    private readonly DesignConditionExportService _export;
    private readonly DesignConditionOperationCoordinator _operations;
    private readonly NotificationService _notifications;
    private readonly IDialogService _dialogs;
    private readonly IFilePickerService _filePickers;
    private readonly List<DesignConditionEntry> _allEntries = [];
    private readonly Dictionary<DesignConditionArchiveKind, ArchiveFilterState> _archiveFilters = new();
    private DesignConditionArchiveKind _selectedArchiveKind;
    private bool _refreshingOptions;
    private bool _isInitialized;
    private int _loadVersion;
    private bool _isLoading;
    private bool _showSummary;
    private string _selectedProject = string.Empty;
    private string _selectedIssuingDiscipline = string.Empty;
    private string _selectedReceivingDiscipline = string.Empty;
    private string _keyword = string.Empty;
    private DateTime? _issuedStartDate;
    private DateTime? _issuedEndDate;
    private string _errorMessage = string.Empty;

    public DesignConditionViewModel(
        DesignConditionDatabaseService database,
        IDesignConditionRepository repository,
        DesignConditionAttachmentStorageService storage,
        DesignConditionOptionRepository options,
        DesignConditionExportService export,
        DesignConditionOperationCoordinator operations,
        NotificationService notifications,
        IDialogService dialogs,
        IFilePickerService filePickers)
    {
        _database = database;
        _repository = repository;
        _storage = storage;
        _options = options;
        _export = export;
        _operations = operations;
        _notifications = notifications;
        _dialogs = dialogs;
        _filePickers = filePickers;
        NewCommand = new RelayCommand(() => ShowEditorAsync(null, DateTime.Today, SelectedArchiveKind));
        OpenCommand = new RelayCommand(OpenAsync);
        SetViewCommand = new RelayCommand(SetView);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        ExportCommand = new RelayCommand(ExportAsync, _ => CanExport);
        RetryCommand = new RelayCommand(EnsureLoadedAsync);
    }

    public event EventHandler<DesignConditionChangedEventArgs>? DataChanged;
    public ObservableCollection<DesignConditionEntry> Entries { get; } = new();
    public ObservableCollection<DesignConditionSummaryDisplayItem> SummaryItems { get; } = new();
    public ObservableCollection<string> ProjectOptions { get; } = new();
    public ObservableCollection<string> IssuingDisciplineOptions { get; } = new();
    public ObservableCollection<string> ReceivingDisciplineOptions { get; } = new();
    public RelayCommand NewCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand SetViewCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand RetryCommand { get; }

    public DesignConditionArchiveKind SelectedArchiveKind
    {
        get => _selectedArchiveKind;
        set
        {
            if (value is not DesignConditionArchiveKind.Issued and not DesignConditionArchiveKind.Received || value == _selectedArchiveKind) return;
            _archiveFilters[_selectedArchiveKind] = CaptureFilters();
            _selectedArchiveKind = value;
            RestoreFilters(_archiveFilters.GetValueOrDefault(value) ?? new ArchiveFilterState());
            RefreshOptions();
            NotifyArchiveProperties();
            ApplyFilters();
        }
    }
    public int SelectedArchiveTabIndex
    {
        get => IsReceivedArchive ? 1 : 0;
        set { if (value is 0 or 1) SelectedArchiveKind = (DesignConditionArchiveKind)value; }
    }
    public bool IsReceivedArchive => SelectedArchiveKind == DesignConditionArchiveKind.Received;
    public bool IsIssuedArchive => !IsReceivedArchive;
    public int IssuedRecordCount => _allEntries.Count(item => item.ArchiveKind == DesignConditionArchiveKind.Issued);
    public int ReceivedRecordCount => _allEntries.Count(item => item.ArchiveKind == DesignConditionArchiveKind.Received);
    public string CurrentArchiveTitle => IsReceivedArchive ? "接受设计条件存档" : "提出设计条件归档";
    public string CurrentArchiveDescription => IsReceivedArchive
        ? "集中管理接受的设计条件，按接受日期查看、汇总和导出。"
        : "归档提出的设计条件，按提出日期查看、汇总和导出。";
    public string NewConditionLabel => IsReceivedArchive ? "新增接受条件" : "新增提出条件";
    public string ArchiveDateLabel => IsReceivedArchive ? "接受日期" : "提出日期";
    public string DateFilterLabel => ArchiveDateLabel;
    public string ListTitle => IsReceivedArchive ? "接受条件明细" : "提出条件明细";
    public string ExportLabel => IsReceivedArchive ? "导出接受条件" : "导出提出条件";
    public string SummaryDescription => IsReceivedArchive ? "按项目、接收专业和提出专业分级" : "按项目、提出专业和接收专业分级";
    public bool HasActiveFilters => IssuedStartDate.HasValue || IssuedEndDate.HasValue
        || SelectedProject.Length > 0 || SelectedIssuingDiscipline.Length > 0 || SelectedReceivingDiscipline.Length > 0
        || !string.IsNullOrWhiteSpace(Keyword);
    public string EmptyStateTitle => HasActiveFilters ? "没有匹配的条件" : IsReceivedArchive ? "暂无接受条件" : "暂无提出条件";
    public string EmptyStateDescription => HasActiveFilters
        ? "调整或清除筛选，查看其他设计条件。"
        : IsReceivedArchive ? "新增接受记录，集中保存收到的设计条件与附件。" : "新增提出记录，归档对外提出的设计条件与附件。";
    public string EmptyStateActionLabel => HasActiveFilters ? "清除筛选" : NewConditionLabel;
    public RelayCommand EmptyStateActionCommand => HasActiveFilters ? ClearFiltersCommand : NewCommand;

    public bool IsLoading
    {
        get => _isLoading;
        private set { if (SetProperty(ref _isLoading, value)) ExportCommand.RaiseCanExecuteChanged(); }
    }
    public bool ShowSummary { get => _showSummary; private set { if (SetProperty(ref _showSummary, value)) { OnPropertyChanged(nameof(ShowList)); } } }
    public bool ShowList => !ShowSummary;
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetProperty(ref _errorMessage, value)) return;
            OnPropertyChanged(nameof(HasError));
            ExportCommand.RaiseCanExecuteChanged();
        }
    }
    public bool HasError => ErrorMessage.Length > 0;
    public bool HasEntries => Entries.Count > 0;
    public int TotalRecordCount => Entries.Count;
    public int TotalDrawingCount => Entries.Sum(item => item.DrawingCount);
    public int TotalAttachmentCount => Entries.Sum(item => item.AttachmentCount);
    public int SummaryItemCount => SummaryItems.Count;
    public int MaxSummaryDrawingCount => Math.Max(1, SummaryItems.Select(item => item.DrawingCount).DefaultIfEmpty(1).Max());
    private bool CanExport => _isInitialized && !IsLoading && !HasError && Entries.Count > 0;

    public DateTime? IssuedStartDate { get => _issuedStartDate; set { if (SetProperty(ref _issuedStartDate, value?.Date)) ApplyFilters(); } }
    public DateTime? IssuedEndDate { get => _issuedEndDate; set { if (SetProperty(ref _issuedEndDate, value?.Date)) ApplyFilters(); } }
    public string SelectedProject { get => _selectedProject; set { if (!_refreshingOptions && SetProperty(ref _selectedProject, value ?? string.Empty)) ApplyFilters(); } }
    public string SelectedIssuingDiscipline { get => _selectedIssuingDiscipline; set { if (!_refreshingOptions && SetProperty(ref _selectedIssuingDiscipline, value ?? string.Empty)) ApplyFilters(); } }
    public string SelectedReceivingDiscipline { get => _selectedReceivingDiscipline; set { if (!_refreshingOptions && SetProperty(ref _selectedReceivingDiscipline, value ?? string.Empty)) ApplyFilters(); } }
    public string Keyword { get => _keyword; set { if (SetProperty(ref _keyword, value ?? string.Empty)) ApplyFilters(); } }

    public async Task EnsureLoadedAsync()
    {
        if (_isInitialized && !HasError)
        {
            return;
        }
        await ReloadAsync();
    }

    public Task CreateForDateAsync(DateTime date, DesignConditionArchiveKind archiveKind = DesignConditionArchiveKind.Issued)
        => ShowEditorAsync(null, date.Date, archiveKind);

    /// <summary>外部数据被整体替换（如完整备份恢复）后调用：使缓存失效并通知订阅方（日历区块）刷新。</summary>
    public void NotifyDataReset()
    {
        _isInitialized = false;
        _loadVersion++; // 使进行中的加载失效，避免旧结果覆盖恢复后的新数据
        IsLoading = false;
        ErrorMessage = string.Empty;
        ClearLoadedEntries();
        DataChanged?.Invoke(this, new DesignConditionChangedEventArgs(Guid.Empty, null, null));
    }

    public async Task ReloadAsync()
    {
        var version = ++_loadVersion;
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            await _database.InitializeAsync();
            var entries = await _repository.GetAllAsync();
            if (version != _loadVersion) return; // 已被更新的加载取代，不碰任何状态
            // 清理暂存目录是带副作用的操作，仅在当前版本仍在执行时进行，
            // 避免过期加载清掉仍在暂存中的附件文件。
            _storage.CleanupStaging();
            _allEntries.Clear();
            _allEntries.AddRange(entries);
            _isInitialized = true;
            RefreshOptions();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
            {
                _isInitialized = false;
                ClearLoadedEntries();
                ErrorMessage = ex.Message;
                _notifications.Notify($"加载设计条件失败：{ex.Message}");
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    public async Task<IReadOnlyList<DesignConditionEntry>> GetByIssuedDateAsync(DateTime date)
    {
        await EnsureLoadedAsync();
        if (HasError) return [];
        return await _repository.GetByIssuedDateAsync(date);
    }

    public async Task<IReadOnlyList<DesignConditionEntry>> GetByIssuedDateRangeAsync(DateTime start, DateTime end)
    {
        await EnsureLoadedAsync();
        if (HasError) return [];
        return await _repository.GetByIssuedDateRangeAsync(start, end);
    }

    public async Task<IReadOnlyList<DesignConditionCalendarSummary>> GetCalendarSummariesAsync(DateTime start, DateTime end)
    {
        await EnsureLoadedAsync();
        if (HasError) return [];
        return await _repository.GetCalendarSummariesAsync(start, end);
    }

    private async Task OpenAsync(object? parameter)
    {
        if (parameter is DesignConditionEntry entry)
        {
            await ShowEditorAsync(entry, entry.ArchiveDate, entry.ArchiveKind);
        }
    }

    private async Task ShowEditorAsync(DesignConditionEntry? source, DateTime defaultDate, DesignConditionArchiveKind archiveKind)
    {
        await EnsureLoadedAsync();
        if (HasError) return;
        var disciplines = (await _options.GetAsync("Discipline")).Concat(_allEntries.SelectMany(item => new[] { item.IssuingDiscipline, item.ReceivingDiscipline })).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray();
        var receivers = (await _options.GetAsync("Receiver")).Concat(_allEntries.Select(item => item.Receiver)).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray();
        var editor = new DesignConditionEditorViewModel(source, defaultDate, disciplines, receivers, archiveKind);
        var action = _dialogs.ShowDesignConditionEditor(editor, _storage);
        if (action == DesignConditionEditorAction.None) return;
        if (action == DesignConditionEditorAction.Delete && source is not null)
        {
            await DeleteAsync(source);
            return;
        }
        if (action != DesignConditionEditorAction.Save || !editor.TryBuild(out var entry)) return;

        using var operation = await _operations.EnterAsync();
        var oldDate = source?.ArchiveDate;
        var stagedDeletes = new List<DesignConditionStagedDelete>();
        IReadOnlyList<DesignConditionAttachment> copied = [];
        try
        {
            foreach (var deleted in editor.DeletedAttachments)
            {
                var staged = _storage.StageDelete(deleted);
                stagedDeletes.Add(staged);
            }
            var nextSortOrder = editor.Attachments.Select(item => item.SortOrder).DefaultIfEmpty(-1).Max() + 1;
            copied = await _storage.CopyFilesAsync(entry.Id, editor.PendingFilePaths.ToArray(), nextSortOrder);
            await _repository.SaveAggregateAsync(entry, editor.DeletedAttachments.Select(item => item.Id), copied);
        }
        catch (Exception original)
        {
            var rollbackErrors = new List<Exception>();
            foreach (var staged in stagedDeletes)
            {
                try { DesignConditionAttachmentStorageService.RollbackDelete(staged); }
                catch (Exception ex) { rollbackErrors.Add(ex); }
            }
            foreach (var attachment in copied)
            {
                try
                {
                    var copiedDelete = _storage.StageDelete(attachment);
                    DesignConditionAttachmentStorageService.CommitDelete(copiedDelete);
                }
                catch (Exception ex) { rollbackErrors.Add(ex); }
            }
            if (rollbackErrors.Count > 0)
                throw new AggregateException("保存设计条件失败，且部分文件回滚未完成。", new[] { original }.Concat(rollbackErrors));
            throw;
        }
        foreach (var staged in stagedDeletes)
        {
            try { DesignConditionAttachmentStorageService.CommitDelete(staged); }
            catch (Exception ex) { _notifications.Notify($"设计条件已保存，但暂存文件清理失败：{ex.Message}"); }
        }
        await ReloadAsync();
        DataChanged?.Invoke(this, new DesignConditionChangedEventArgs(entry.Id, oldDate, entry.ArchiveDate));
        _notifications.Notify($"{(source is null ? "已添加" : "已保存")}{entry.ArchiveKindDisplay}条件");
    }

    private async Task DeleteAsync(DesignConditionEntry entry)
    {
        using var operation = await _operations.EnterAsync();
        var staged = _storage.StageConditionFolderDelete(entry.Id);
        try
        {
            await _repository.DeleteAsync(entry.Id);
        }
        catch (Exception original)
        {
            try { DesignConditionAttachmentStorageService.RollbackDelete(staged); }
            catch (Exception rollback) { throw new AggregateException("删除设计条件失败，且条件文件回滚未完成。", original, rollback); }
            throw;
        }
        try { DesignConditionAttachmentStorageService.CommitDelete(staged); }
        catch (Exception ex) { _notifications.Notify($"设计条件已删除，但暂存文件清理失败：{ex.Message}"); }
        await ReloadAsync();
        DataChanged?.Invoke(this, new DesignConditionChangedEventArgs(entry.Id, entry.ArchiveDate, null));
        _notifications.Notify("已删除设计条件");
    }

    private void ApplyFilters()
    {
        NotifyEmptyState();
        if (!_isInitialized) return;
        var keyword = Keyword.Trim();
        var filtered = _allEntries.Where(item =>
            item.ArchiveKind == SelectedArchiveKind &&
            (!IssuedStartDate.HasValue || item.ArchiveDate >= IssuedStartDate.Value) &&
            (!IssuedEndDate.HasValue || item.ArchiveDate <= IssuedEndDate.Value) &&
            (SelectedProject.Length == 0 || item.ProjectNumber.Equals(SelectedProject, StringComparison.OrdinalIgnoreCase)) &&
            (SelectedIssuingDiscipline.Length == 0 || item.IssuingDiscipline.Equals(SelectedIssuingDiscipline, StringComparison.OrdinalIgnoreCase)) &&
            (SelectedReceivingDiscipline.Length == 0 || item.ReceivingDiscipline.Equals(SelectedReceivingDiscipline, StringComparison.OrdinalIgnoreCase)) &&
            (keyword.Length == 0 || Search(item, keyword)))
            .OrderByDescending(item => item.ArchiveDate).ThenByDescending(item => item.UpdatedAt).ToArray();
        CollectionHelper.Replace(Entries, filtered);
        BuildSummary(filtered);
        NotifyStatistics();
    }

    private void ClearLoadedEntries()
    {
        _allEntries.Clear();
        Entries.Clear();
        BuildSummary([]);
        RefreshOptions();
        NotifyStatistics();
    }

    private static bool Search(DesignConditionEntry item, string keyword) =>
        item.ProjectNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        item.ConditionName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        item.Receiver.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        item.IssuingDiscipline.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        item.ReceivingDiscipline.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        item.DrawingSpecifications.Any(specification => specification.DrawingSize.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
        item.Attachments.Any(file => file.OriginalFileName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private void BuildSummary(IReadOnlyList<DesignConditionEntry> entries)
    {
        var items = new List<DesignConditionSummaryDisplayItem>();
        foreach (var project in entries.GroupBy(item => item.ProjectNumber).OrderBy(group => group.Key))
        {
            items.Add(DesignConditionSummaryDisplayItem.From("项目", project.Key, project, 0));
            foreach (var primary in project.GroupBy(item => IsReceivedArchive ? item.ReceivingDiscipline : item.IssuingDiscipline).OrderBy(group => group.Key))
            {
                items.Add(DesignConditionSummaryDisplayItem.From(IsReceivedArchive ? "接收专业" : "提出专业", primary.Key, primary, 1));
                foreach (var secondary in primary.GroupBy(item => IsReceivedArchive ? item.IssuingDiscipline : item.ReceivingDiscipline).OrderBy(group => group.Key))
                {
                    items.Add(DesignConditionSummaryDisplayItem.From(IsReceivedArchive ? "提出专业" : "接收专业", secondary.Key, secondary, 2));
                }
            }
        }
        CollectionHelper.Replace(SummaryItems, items);
        OnPropertyChanged(nameof(SummaryItemCount));
        OnPropertyChanged(nameof(MaxSummaryDrawingCount));
    }

    private void RefreshOptions()
    {
        var entries = _allEntries.Where(item => item.ArchiveKind == SelectedArchiveKind).ToArray();
        // ComboBox 会在选项移除时将 SelectedItem 写回 null；更新选项期间保留各 Tab 的筛选值。
        _refreshingOptions = true;
        try
        {
            CollectionHelper.Replace(ProjectOptions, entries.Select(item => item.ProjectNumber).Append(SelectedProject).Append(string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));
            CollectionHelper.Replace(IssuingDisciplineOptions, entries.Select(item => item.IssuingDiscipline).Append(SelectedIssuingDiscipline).Append(string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));
            CollectionHelper.Replace(ReceivingDisciplineOptions, entries.Select(item => item.ReceivingDiscipline).Append(SelectedReceivingDiscipline).Append(string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));
        }
        finally { _refreshingOptions = false; }
        NotifyFilterProperties();
    }

    private void ClearFilters()
    {
        _issuedStartDate = null; _issuedEndDate = null; _selectedProject = string.Empty; _selectedIssuingDiscipline = string.Empty; _selectedReceivingDiscipline = string.Empty; _keyword = string.Empty;
        OnPropertyChanged(nameof(IssuedStartDate)); OnPropertyChanged(nameof(IssuedEndDate)); OnPropertyChanged(nameof(SelectedProject)); OnPropertyChanged(nameof(SelectedIssuingDiscipline)); OnPropertyChanged(nameof(SelectedReceivingDiscipline)); OnPropertyChanged(nameof(Keyword));
        ApplyFilters();
    }

    private void SetView(object? parameter) => ShowSummary = string.Equals(parameter as string, "Summary", StringComparison.Ordinal);

    private async Task ExportAsync(object? _)
    {
        if (!CanExport) return;
        var archiveKind = SelectedArchiveKind;
        var archiveName = IsReceivedArchive ? "接受设计条件" : "提出设计条件";
        var exportEntries = Entries.ToArray();
        if (!DesignConditionExportService.TryValidate(exportEntries, out var validationError))
        {
            _notifications.Notify(validationError);
            return;
        }

        var file = _filePickers.PickSaveFile($"导出{archiveName}汇总", $"{archiveName}汇总_{DateTime.Now:yyyyMMdd}.xlsx", "Excel 工作簿 (*.xlsx)|*.xlsx", ".xlsx");
        if (file is null) return;
        await _export.ExportAsync(file, exportEntries, archiveKind);
        _notifications.Notify($"{archiveName}汇总已导出：{file}");
    }

    private void NotifyStatistics()
    {
        OnPropertyChanged(nameof(HasEntries)); OnPropertyChanged(nameof(TotalRecordCount)); OnPropertyChanged(nameof(TotalDrawingCount)); OnPropertyChanged(nameof(TotalAttachmentCount));
        OnPropertyChanged(nameof(IssuedRecordCount)); OnPropertyChanged(nameof(ReceivedRecordCount));
        ExportCommand.RaiseCanExecuteChanged();
    }

    private ArchiveFilterState CaptureFilters() => new(
        _selectedProject, _selectedIssuingDiscipline, _selectedReceivingDiscipline,
        _keyword, _issuedStartDate, _issuedEndDate, _showSummary);

    private void RestoreFilters(ArchiveFilterState state)
    {
        _selectedProject = state.Project;
        _selectedIssuingDiscipline = state.IssuingDiscipline;
        _selectedReceivingDiscipline = state.ReceivingDiscipline;
        _keyword = state.Keyword;
        _issuedStartDate = state.StartDate;
        _issuedEndDate = state.EndDate;
        _showSummary = state.ShowSummary;
    }

    private void NotifyFilterProperties()
    {
        OnPropertyChanged(nameof(SelectedProject));
        OnPropertyChanged(nameof(SelectedIssuingDiscipline));
        OnPropertyChanged(nameof(SelectedReceivingDiscipline));
        OnPropertyChanged(nameof(Keyword));
        OnPropertyChanged(nameof(IssuedStartDate));
        OnPropertyChanged(nameof(IssuedEndDate));
        OnPropertyChanged(nameof(ShowSummary));
        OnPropertyChanged(nameof(ShowList));
        NotifyEmptyState();
    }

    private void NotifyArchiveProperties()
    {
        OnPropertyChanged(nameof(SelectedArchiveKind));
        OnPropertyChanged(nameof(SelectedArchiveTabIndex));
        OnPropertyChanged(nameof(IsReceivedArchive));
        OnPropertyChanged(nameof(IsIssuedArchive));
        OnPropertyChanged(nameof(CurrentArchiveTitle));
        OnPropertyChanged(nameof(CurrentArchiveDescription));
        OnPropertyChanged(nameof(NewConditionLabel));
        OnPropertyChanged(nameof(ArchiveDateLabel));
        OnPropertyChanged(nameof(DateFilterLabel));
        OnPropertyChanged(nameof(ListTitle));
        OnPropertyChanged(nameof(ExportLabel));
        OnPropertyChanged(nameof(SummaryDescription));
    }

    private void NotifyEmptyState()
    {
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
        OnPropertyChanged(nameof(EmptyStateActionLabel));
        OnPropertyChanged(nameof(EmptyStateActionCommand));
    }

    private sealed record ArchiveFilterState(
        string Project = "", string IssuingDiscipline = "", string ReceivingDiscipline = "",
        string Keyword = "", DateTime? StartDate = null, DateTime? EndDate = null, bool ShowSummary = false);
}

/// <summary>日期成员名保持兼容，值为变更前后的归档日期（提出日或接受日）。</summary>
public sealed record DesignConditionChangedEventArgs(Guid Id, DateTime? OldIssuedDate, DateTime? NewIssuedDate);

public sealed record DesignConditionSummaryDisplayItem(string Level, string Name, int LevelDepth, int RecordCount, int DrawingCount, int AttachmentCount)
{
    public string DisplayName => $"{new string('　', LevelDepth)}{(LevelDepth == 0 ? "" : "↳ ")}{Level} · {Name}";
    public static DesignConditionSummaryDisplayItem From(string level, string name, IEnumerable<DesignConditionEntry> entries, int depth)
    {
        var items = entries.ToArray();
        return new DesignConditionSummaryDisplayItem(level, name, depth, items.Length, items.Sum(item => item.DrawingCount), items.Sum(item => item.AttachmentCount));
    }
}
