using System.Collections.ObjectModel;
using KanbanForOne.Controls;
using KanbanForOne.Modules.DesignConditions.Data;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Modules.DesignConditions.Repositories;
using KanbanForOne.Modules.DesignConditions.Services;
using KanbanForOne.Modules.DesignConditions.Views;
using KanbanForOne.Services;
using KanbanForOne.ViewModels;
using Microsoft.Win32;

namespace KanbanForOne.Modules.DesignConditions.ViewModels;

public sealed class DesignConditionViewModel : ObservableObject
{
    private readonly DesignConditionDatabaseService _database;
    private readonly DesignConditionRepository _repository;
    private readonly DesignConditionAttachmentStorageService _storage;
    private readonly DesignConditionOptionRepository _options;
    private readonly DesignConditionExportService _export;
    private readonly DesignConditionOperationCoordinator _operations;
    private readonly NotificationService _notifications;
    private readonly List<DesignConditionEntry> _allEntries = [];
    private bool _isInitialized;
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
        DesignConditionRepository repository,
        DesignConditionAttachmentStorageService storage,
        DesignConditionOptionRepository options,
        DesignConditionExportService export,
        DesignConditionOperationCoordinator operations,
        NotificationService notifications)
    {
        _database = database;
        _repository = repository;
        _storage = storage;
        _options = options;
        _export = export;
        _operations = operations;
        _notifications = notifications;
        NewCommand = new RelayCommand(() => ShowEditorAsync(null, DateTime.Today));
        OpenCommand = new RelayCommand(OpenAsync);
        SetViewCommand = new RelayCommand(SetView);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        ExportCommand = new RelayCommand(ExportAsync, _ => Entries.Count > 0);
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

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool ShowSummary { get => _showSummary; private set { if (SetProperty(ref _showSummary, value)) { OnPropertyChanged(nameof(ShowList)); } } }
    public bool ShowList => !ShowSummary;
    public string ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => ErrorMessage.Length > 0;
    public bool HasEntries => Entries.Count > 0;
    public int TotalRecordCount => Entries.Count;
    public int TotalDrawingCount => Entries.Sum(item => item.DrawingCount);
    public int TotalAttachmentCount => Entries.Sum(item => item.AttachmentCount);
    public int SummaryItemCount => SummaryItems.Count;
    public int MaxSummaryDrawingCount => Math.Max(1, SummaryItems.Select(item => item.DrawingCount).DefaultIfEmpty(1).Max());

    public DateTime? IssuedStartDate { get => _issuedStartDate; set { if (SetProperty(ref _issuedStartDate, value?.Date)) ApplyFilters(); } }
    public DateTime? IssuedEndDate { get => _issuedEndDate; set { if (SetProperty(ref _issuedEndDate, value?.Date)) ApplyFilters(); } }
    public string SelectedProject { get => _selectedProject; set { if (SetProperty(ref _selectedProject, value ?? string.Empty)) ApplyFilters(); } }
    public string SelectedIssuingDiscipline { get => _selectedIssuingDiscipline; set { if (SetProperty(ref _selectedIssuingDiscipline, value ?? string.Empty)) ApplyFilters(); } }
    public string SelectedReceivingDiscipline { get => _selectedReceivingDiscipline; set { if (SetProperty(ref _selectedReceivingDiscipline, value ?? string.Empty)) ApplyFilters(); } }
    public string Keyword { get => _keyword; set { if (SetProperty(ref _keyword, value ?? string.Empty)) ApplyFilters(); } }

    public async Task EnsureLoadedAsync()
    {
        if (_isInitialized && !HasError)
        {
            return;
        }
        await ReloadAsync();
    }

    public Task CreateForDateAsync(DateTime issuedDate) => ShowEditorAsync(null, issuedDate.Date);

    public void NotifyDataReset() => DataChanged?.Invoke(this, new DesignConditionChangedEventArgs(Guid.Empty, null, null));

    public async Task ReloadAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            await _database.InitializeAsync();
            _storage.CleanupStaging();
            _allEntries.Clear();
            _allEntries.AddRange(await _repository.GetAllAsync());
            _isInitialized = true;
            RefreshOptions();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _notifications.Notify($"加载设计条件失败：{ex.Message}");
        }
        finally
        {
            IsLoading = false;
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
            await ShowEditorAsync(entry, entry.IssuedDate);
        }
    }

    private async Task ShowEditorAsync(DesignConditionEntry? source, DateTime defaultDate)
    {
        await EnsureLoadedAsync();
        if (HasError) return;
        var disciplines = (await _options.GetAsync("Discipline")).Concat(_allEntries.SelectMany(item => new[] { item.IssuingDiscipline, item.ReceivingDiscipline })).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray();
        var receivers = (await _options.GetAsync("Receiver")).Concat(_allEntries.Select(item => item.Receiver)).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray();
        var sizes = await _options.GetAsync("DrawingSize");
        var editor = new DesignConditionEditorViewModel(source, defaultDate, disciplines, receivers, sizes);
        var action = DesignConditionEditorDialog.Show(DialogHelper.GetDialogOwner(), editor, _storage);
        if (action == DesignConditionEditorAction.None) return;
        if (action == DesignConditionEditorAction.Delete && source is not null)
        {
            await DeleteAsync(source);
            return;
        }
        if (action != DesignConditionEditorAction.Save || !editor.TryBuild(out var entry)) return;

        using var operation = await _operations.EnterAsync();
        var oldDate = source?.IssuedDate;
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
        DataChanged?.Invoke(this, new DesignConditionChangedEventArgs(entry.Id, oldDate, entry.IssuedDate));
        _notifications.Notify(source is null ? "已添加设计条件" : "已保存设计条件");
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
        DataChanged?.Invoke(this, new DesignConditionChangedEventArgs(entry.Id, entry.IssuedDate, null));
        _notifications.Notify("已删除设计条件");
    }

    private void ApplyFilters()
    {
        if (!_isInitialized) return;
        var keyword = Keyword.Trim();
        var filtered = _allEntries.Where(item =>
            (!IssuedStartDate.HasValue || item.IssuedDate >= IssuedStartDate.Value) &&
            (!IssuedEndDate.HasValue || item.IssuedDate <= IssuedEndDate.Value) &&
            (SelectedProject.Length == 0 || item.ProjectNumber.Equals(SelectedProject, StringComparison.OrdinalIgnoreCase)) &&
            (SelectedIssuingDiscipline.Length == 0 || item.IssuingDiscipline.Equals(SelectedIssuingDiscipline, StringComparison.OrdinalIgnoreCase)) &&
            (SelectedReceivingDiscipline.Length == 0 || item.ReceivingDiscipline.Equals(SelectedReceivingDiscipline, StringComparison.OrdinalIgnoreCase)) &&
            (keyword.Length == 0 || Search(item, keyword)))
            .OrderByDescending(item => item.IssuedDate).ThenByDescending(item => item.UpdatedAt).ToArray();
        CollectionHelper.Replace(Entries, filtered);
        BuildSummary(filtered);
        NotifyStatistics();
    }

    private static bool Search(DesignConditionEntry item, string keyword) =>
        item.ProjectNumber.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        item.ConditionName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        item.Receiver.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        item.IssuingDiscipline.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        item.ReceivingDiscipline.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
        item.Attachments.Any(file => file.OriginalFileName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private void BuildSummary(IReadOnlyList<DesignConditionEntry> entries)
    {
        var items = new List<DesignConditionSummaryDisplayItem>();
        foreach (var project in entries.GroupBy(item => item.ProjectNumber).OrderBy(group => group.Key))
        {
            items.Add(DesignConditionSummaryDisplayItem.From("项目", project.Key, project, 0));
            foreach (var issuing in project.GroupBy(item => item.IssuingDiscipline).OrderBy(group => group.Key))
            {
                items.Add(DesignConditionSummaryDisplayItem.From("提出专业", issuing.Key, issuing, 1));
                foreach (var receiving in issuing.GroupBy(item => item.ReceivingDiscipline).OrderBy(group => group.Key))
                {
                    items.Add(DesignConditionSummaryDisplayItem.From("接收专业", receiving.Key, receiving, 2));
                }
            }
        }
        CollectionHelper.Replace(SummaryItems, items);
        OnPropertyChanged(nameof(SummaryItemCount));
        OnPropertyChanged(nameof(MaxSummaryDrawingCount));
    }

    private void RefreshOptions()
    {
        CollectionHelper.Replace(ProjectOptions, new[] { string.Empty }.Concat(_allEntries.Select(item => item.ProjectNumber).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value)));
        CollectionHelper.Replace(IssuingDisciplineOptions, new[] { string.Empty }.Concat(_allEntries.Select(item => item.IssuingDiscipline).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value)));
        CollectionHelper.Replace(ReceivingDisciplineOptions, new[] { string.Empty }.Concat(_allEntries.Select(item => item.ReceivingDiscipline).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value)));
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
        var dialog = new SaveFileDialog { DefaultExt = ".xlsx", Filter = "Excel 工作簿 (*.xlsx)|*.xlsx", FileName = $"设计条件汇总_{DateTime.Now:yyyyMMdd}.xlsx", Title = "导出设计条件汇总" };
        if (dialog.ShowDialog() != true) return;
        await _export.ExportAsync(dialog.FileName, Entries.ToArray());
        _notifications.Notify($"设计条件汇总已导出：{dialog.FileName}");
    }

    private void NotifyStatistics()
    {
        OnPropertyChanged(nameof(HasEntries)); OnPropertyChanged(nameof(TotalRecordCount)); OnPropertyChanged(nameof(TotalDrawingCount)); OnPropertyChanged(nameof(TotalAttachmentCount));
        ExportCommand.RaiseCanExecuteChanged();
    }
}

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
