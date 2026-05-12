using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using KanbanForOne.Models;
using KanbanForOne.Services;
using Microsoft.Win32;
using TaskStatus = KanbanForOne.Models.TaskStatus;

namespace KanbanForOne.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly ObservableCollection<TaskItem> _allTasks = new();
    private readonly ObservableCollection<NoteItem> _allNotes = new();
    private readonly DatabaseService _databaseService = new();
    private readonly AttachmentStorageService _attachmentStorage = new();
    private readonly TaskRepository _taskRepository;
    private readonly NoteRepository _noteRepository;
    private readonly AttachmentRepository _attachmentRepository;
    private readonly BackupService _backupService = new();
    private bool _isInitialized;
    private bool _isLoading;
    private string _searchText = string.Empty;
    private string _currentFilter = "Board";
    private string _currentFilterLabel = "看板";
    private string _notificationText = string.Empty;
    private string _lastBackupPath = string.Empty;
    private int _lastBackupAttachmentCount;
    private long _lastBackupSizeBytes;
    private DateTime? _lastBackupCreatedAt;
    private string _lastRestoreSourcePath = string.Empty;
    private string _lastRestoreProtectiveBackupPath = string.Empty;
    private int _lastRestoreAttachmentCount;
    private DateTime? _lastRestoreAt;
    private string _taskTitleDraft = string.Empty;
    private string _taskDescriptionDraft = string.Empty;
    private string _taskTagsDraft = string.Empty;
    private TaskStatus _taskStatusDraft;
    private TaskPriority _taskPriorityDraft = TaskPriority.Medium;
    private DateTime? _taskStartDateDraft;
    private DateTime? _taskEndDateDraft;
    private string _noteTitleDraft = string.Empty;
    private string _noteContentDraft = string.Empty;
    private string _noteTagsDraft = string.Empty;
    private TaskItem? _selectedTask;
    private NoteItem? _selectedNote;

    public MainWindowViewModel()
    {
        _taskRepository = new TaskRepository(_databaseService);
        _noteRepository = new NoteRepository(_databaseService);
        _attachmentRepository = new AttachmentRepository(_databaseService);

        TaskStatusOptions = Enum.GetValues<TaskStatus>();
        TaskPriorityOptions = Enum.GetValues<TaskPriority>();

        CreateTaskCommand = new RelayCommand(CreateTaskAsync);
        CreateNoteCommand = new RelayCommand(CreateNoteAsync);
        OpenTaskCommand = new RelayCommand(parameter => OpenTask(parameter as TaskItem));
        OpenNoteCommand = new RelayCommand(parameter => OpenNote(parameter as NoteItem));
        CloseDrawerCommand = new RelayCommand(CloseDrawer);
        DeleteTaskCommand = new RelayCommand(DeleteSelectedTaskAsync, _ => SelectedTask is not null);
        DeleteNoteCommand = new RelayCommand(DeleteSelectedNoteAsync, _ => SelectedNote is not null);
        SaveTaskCommand = new RelayCommand(SaveSelectedTaskAsync, _ => SelectedTask is not null && HasUnsavedTaskChanges);
        SaveNoteCommand = new RelayCommand(SaveSelectedNoteAsync, _ => SelectedNote is not null && HasUnsavedNoteChanges);
        MoveCardCommand = new RelayCommand(MoveCardAsync);
        AttachFilesCommand = new RelayCommand(AttachFilesAsync);
        PickFilesCommand = new RelayCommand(PickFilesAsync);
        OpenAttachmentCommand = new RelayCommand(OpenAttachment);
        RevealAttachmentCommand = new RelayCommand(RevealAttachment);
        DeleteAttachmentCommand = new RelayCommand(DeleteAttachmentAsync);
        CreateBackupCommand = new RelayCommand(CreateBackupAsync);
        RestoreBackupCommand = new RelayCommand(RestoreBackupAsync);
        ChangeFilterCommand = new RelayCommand(ChangeFilter);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

        RefreshBoard();
    }

    public IReadOnlyList<TaskStatus> TaskStatusOptions { get; }

    public IReadOnlyList<TaskPriority> TaskPriorityOptions { get; }

    public ObservableCollection<TaskItem> TodoTasks { get; } = new();

    public ObservableCollection<TaskItem> DoingTasks { get; } = new();

    public ObservableCollection<TaskItem> BlockedTasks { get; } = new();

    public ObservableCollection<TaskItem> DoneTasks { get; } = new();

    public ObservableCollection<NoteItem> Notes { get; } = new();

    public ObservableCollection<TaskItem> CalendarTasks { get; } = new();

    public RelayCommand CreateTaskCommand { get; }

    public RelayCommand CreateNoteCommand { get; }

    public RelayCommand OpenTaskCommand { get; }

    public RelayCommand OpenNoteCommand { get; }

    public RelayCommand CloseDrawerCommand { get; }

    public RelayCommand DeleteTaskCommand { get; }

    public RelayCommand DeleteNoteCommand { get; }

    public RelayCommand SaveTaskCommand { get; }

    public RelayCommand SaveNoteCommand { get; }

    public RelayCommand MoveCardCommand { get; }

    public RelayCommand AttachFilesCommand { get; }

    public RelayCommand PickFilesCommand { get; }

    public RelayCommand OpenAttachmentCommand { get; }

    public RelayCommand RevealAttachmentCommand { get; }

    public RelayCommand DeleteAttachmentCommand { get; }

    public RelayCommand CreateBackupCommand { get; }

    public RelayCommand RestoreBackupCommand { get; }

    public RelayCommand ChangeFilterCommand { get; }

    public RelayCommand ClearSearchCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshBoard();
            }
        }
    }

    public string CurrentFilterLabel
    {
        get => _currentFilterLabel;
        private set => SetProperty(ref _currentFilterLabel, value);
    }

    public string CurrentFilter => _currentFilter;

    public string NotificationText
    {
        get => _notificationText;
        private set
        {
            if (SetProperty(ref _notificationText, value))
            {
                OnPropertyChanged(nameof(HasNotification));
            }
        }
    }

    public bool HasNotification => !string.IsNullOrWhiteSpace(NotificationText);

    public string DataDirectory => AppPaths.DataRoot;

    public string DatabasePath => AppPaths.DatabasePath;

    public string AttachmentDirectory => AppPaths.AttachmentRoot;

    public string BackupDirectory => AppPaths.BackupRoot;

    public string AppVersion => "v0.1";

    public string CopyrightText => "copyright @ 2026 YF";

    public string LastBackupPath
    {
        get => _lastBackupPath;
        private set
        {
            if (SetProperty(ref _lastBackupPath, value))
            {
                OnPropertyChanged(nameof(HasLastBackup));
            }
        }
    }

    public bool HasLastBackup => !string.IsNullOrWhiteSpace(LastBackupPath);

    public int LastBackupAttachmentCount
    {
        get => _lastBackupAttachmentCount;
        private set => SetProperty(ref _lastBackupAttachmentCount, value);
    }

    public long LastBackupSizeBytes
    {
        get => _lastBackupSizeBytes;
        private set => SetProperty(ref _lastBackupSizeBytes, value);
    }

    public DateTime? LastBackupCreatedAt
    {
        get => _lastBackupCreatedAt;
        private set => SetProperty(ref _lastBackupCreatedAt, value);
    }

    public string LastRestoreSourcePath
    {
        get => _lastRestoreSourcePath;
        private set
        {
            if (SetProperty(ref _lastRestoreSourcePath, value))
            {
                OnPropertyChanged(nameof(HasLastRestore));
            }
        }
    }

    public string LastRestoreProtectiveBackupPath
    {
        get => _lastRestoreProtectiveBackupPath;
        private set => SetProperty(ref _lastRestoreProtectiveBackupPath, value);
    }

    public int LastRestoreAttachmentCount
    {
        get => _lastRestoreAttachmentCount;
        private set => SetProperty(ref _lastRestoreAttachmentCount, value);
    }

    public DateTime? LastRestoreAt
    {
        get => _lastRestoreAt;
        private set => SetProperty(ref _lastRestoreAt, value);
    }

    public bool HasLastRestore => !string.IsNullOrWhiteSpace(LastRestoreSourcePath);

    public string TaskTitleDraft
    {
        get => _taskTitleDraft;
        set
        {
            if (SetProperty(ref _taskTitleDraft, value))
            {
                OnTaskDraftChanged();
            }
        }
    }

    public string TaskDescriptionDraft
    {
        get => _taskDescriptionDraft;
        set
        {
            if (SetProperty(ref _taskDescriptionDraft, value))
            {
                OnTaskDraftChanged();
            }
        }
    }

    public string TaskTagsDraft
    {
        get => _taskTagsDraft;
        set
        {
            if (SetProperty(ref _taskTagsDraft, value))
            {
                OnTaskDraftChanged();
            }
        }
    }

    public TaskStatus TaskStatusDraft
    {
        get => _taskStatusDraft;
        set
        {
            if (SetProperty(ref _taskStatusDraft, value))
            {
                OnTaskDraftChanged();
            }
        }
    }

    public TaskPriority TaskPriorityDraft
    {
        get => _taskPriorityDraft;
        set
        {
            if (SetProperty(ref _taskPriorityDraft, value))
            {
                OnTaskDraftChanged();
            }
        }
    }

    public DateTime? TaskStartDateDraft
    {
        get => _taskStartDateDraft;
        set
        {
            if (SetProperty(ref _taskStartDateDraft, value))
            {
                OnTaskDraftChanged();
            }
        }
    }

    public DateTime? TaskEndDateDraft
    {
        get => _taskEndDateDraft;
        set
        {
            if (SetProperty(ref _taskEndDateDraft, value))
            {
                OnTaskDraftChanged();
            }
        }
    }

    public string NoteTitleDraft
    {
        get => _noteTitleDraft;
        set
        {
            if (SetProperty(ref _noteTitleDraft, value))
            {
                OnNoteDraftChanged();
            }
        }
    }

    public string NoteContentDraft
    {
        get => _noteContentDraft;
        set
        {
            if (SetProperty(ref _noteContentDraft, value))
            {
                OnNoteDraftChanged();
            }
        }
    }

    public string NoteTagsDraft
    {
        get => _noteTagsDraft;
        set
        {
            if (SetProperty(ref _noteTagsDraft, value))
            {
                OnNoteDraftChanged();
            }
        }
    }

    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        private set
        {
            if (SetProperty(ref _selectedTask, value))
            {
                OnPropertyChanged(nameof(IsTaskDrawerOpen));
                OnPropertyChanged(nameof(IsDrawerOpen));
                OnPropertyChanged(nameof(HasUnsavedTaskChanges));
                DeleteTaskCommand.RaiseCanExecuteChanged();
                SaveTaskCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public NoteItem? SelectedNote
    {
        get => _selectedNote;
        private set
        {
            if (SetProperty(ref _selectedNote, value))
            {
                OnPropertyChanged(nameof(IsNoteDrawerOpen));
                OnPropertyChanged(nameof(IsDrawerOpen));
                OnPropertyChanged(nameof(HasUnsavedNoteChanges));
                DeleteNoteCommand.RaiseCanExecuteChanged();
                SaveNoteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsTaskDrawerOpen => SelectedTask is not null;

    public bool IsNoteDrawerOpen => SelectedNote is not null;

    public bool IsDrawerOpen => IsTaskDrawerOpen || IsNoteDrawerOpen;

    public bool HasUnsavedTaskChanges => SelectedTask is not null
        && (!string.Equals(TaskTitleDraft, SelectedTask.Title, StringComparison.Ordinal)
            || !string.Equals(TaskDescriptionDraft, SelectedTask.Description, StringComparison.Ordinal)
            || !string.Equals(TaskTagsDraft, SelectedTask.TagsDisplay, StringComparison.Ordinal)
            || TaskStatusDraft != SelectedTask.Status
            || TaskPriorityDraft != SelectedTask.Priority
            || TaskStartDateDraft?.Date != SelectedTask.StartDate?.Date
            || TaskEndDateDraft?.Date != SelectedTask.EndDate?.Date);

    public bool HasUnsavedNoteChanges => SelectedNote is not null
        && (!string.Equals(NoteTitleDraft, SelectedNote.Title, StringComparison.Ordinal)
            || !string.Equals(NoteContentDraft, SelectedNote.Content, StringComparison.Ordinal)
            || !string.Equals(NoteTagsDraft, SelectedNote.TagsDisplay, StringComparison.Ordinal));

    public bool IsBoardViewVisible => _currentFilter is not "Calendar" and not "Backup" and not "Settings";

    public bool IsCalendarViewVisible => _currentFilter == "Calendar";

    public bool IsBackupViewVisible => _currentFilter == "Backup";

    public bool IsSettingsViewVisible => _currentFilter == "Settings";

    public int CalendarTaskCount => CalendarTasks.Count;

    public int VisibleTaskCount => TodoTasks.Count + DoingTasks.Count + BlockedTasks.Count + DoneTasks.Count;

    public int VisibleNoteCount => Notes.Count;

    public async Task InitializeAsync()
    {
        try
        {
            _isLoading = true;
            await _databaseService.InitializeAsync();

            foreach (var task in _allTasks)
            {
                UntrackTask(task);
            }

            foreach (var note in _allNotes)
            {
                UntrackNote(note);
            }

            _allTasks.Clear();
            _allNotes.Clear();

            var tasks = await _taskRepository.GetAllAsync();
            var notes = await _noteRepository.GetAllAsync();
            var attachments = await _attachmentRepository.GetAllAsync();

            var taskAttachments = attachments
                .Where(attachment => attachment.OwnerType == AttachmentOwnerType.Task)
                .GroupBy(attachment => attachment.OwnerId)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.SortOrder).ToArray());
            var noteAttachments = attachments
                .Where(attachment => attachment.OwnerType == AttachmentOwnerType.Note)
                .GroupBy(attachment => attachment.OwnerId)
                .ToDictionary(group => group.Key, group => group.OrderBy(item => item.SortOrder).ToArray());

            foreach (var task in tasks)
            {
                if (taskAttachments.TryGetValue(task.Id, out var items))
                {
                    foreach (var attachment in items)
                    {
                        task.Attachments.Add(attachment);
                    }
                }

                AddTaskToMemory(task);
            }

            foreach (var note in notes)
            {
                if (noteAttachments.TryGetValue(note.Id, out var items))
                {
                    foreach (var attachment in items)
                    {
                        note.Attachments.Add(attachment);
                    }
                }

                AddNoteToMemory(note);
            }

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            SetNotification($"数据库初始化失败：{ex.Message}");
        }
        finally
        {
            _isLoading = false;
            RefreshBoard();
        }
    }

    private async Task CreateTaskAsync(object? parameter)
    {
        if (!ConfirmDiscardDrawerChanges())
        {
            return;
        }

        var status = parameter switch
        {
            TaskStatus typedStatus => typedStatus,
            KanbanColumnKind.Todo => TaskStatus.Todo,
            KanbanColumnKind.Doing => TaskStatus.Doing,
            KanbanColumnKind.Blocked => TaskStatus.Blocked,
            KanbanColumnKind.Done => TaskStatus.Done,
            _ => TaskStatus.Todo
        };

        var task = new TaskItem
        {
            Title = "新任务",
            Description = "在右侧抽屉补充任务说明。",
            Status = status,
            Priority = TaskPriority.Medium,
            SortOrder = NextTaskSortOrder(status)
        };
        task.Tags.Add("Inbox");

        if (!await SaveTaskSafelyAsync(task, "已创建任务"))
        {
            return;
        }

        AddTaskToMemory(task);
        RefreshBoard();
        OpenTask(task);
    }

    private async Task CreateNoteAsync()
    {
        if (!ConfirmDiscardDrawerChanges())
        {
            return;
        }

        var note = new NoteItem
        {
            Title = "新备忘",
            Content = "记录一个想法、提醒或临时片段。",
            SortOrder = NextNoteSortOrder()
        };
        note.Tags.Add("Note");

        if (!await SaveNoteSafelyAsync(note, "已创建备忘"))
        {
            return;
        }

        AddNoteToMemory(note);
        RefreshBoard();
        OpenNote(note);
    }

    private void OpenTask(TaskItem? task)
    {
        if (task is null)
        {
            return;
        }

        if (!ReferenceEquals(SelectedTask, task) && !ConfirmDiscardDrawerChanges())
        {
            return;
        }

        SelectedNote = null;
        ClearNoteDraft();
        SelectedTask = task;
        LoadTaskDraft(task);
    }

    private void OpenNote(NoteItem? note)
    {
        if (note is null)
        {
            return;
        }

        if (!ReferenceEquals(SelectedNote, note) && !ConfirmDiscardDrawerChanges())
        {
            return;
        }

        SelectedTask = null;
        ClearTaskDraft();
        SelectedNote = note;
        LoadNoteDraft(note);
    }

    private void CloseDrawer()
    {
        if (!ConfirmDiscardDrawerChanges())
        {
            return;
        }

        SelectedTask = null;
        SelectedNote = null;
        ClearTaskDraft();
        ClearNoteDraft();
        RefreshBoard();
    }

    private async Task DeleteSelectedTaskAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var task = SelectedTask;

        if (MessageBox.Show(
                "删除后会同时删除这张任务卡片的本地附件，是否继续？",
                "删除任务",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        IReadOnlyList<StagedAttachmentDelete> stagedDeletes = [];
        var attachmentsDeleted = false;
        var taskDeleted = false;

        try
        {
            stagedDeletes = StageAttachmentDeletes(task.Attachments);
            await _attachmentRepository.DeleteByOwnerAsync(AttachmentOwnerType.Task, task.Id);
            attachmentsDeleted = true;
            await _taskRepository.DeleteAsync(task.Id);
            taskDeleted = true;
            CommitStagedDeletes(stagedDeletes);
            UntrackTask(task);
            _allTasks.Remove(task);
            SelectedTask = null;
            ClearTaskDraft();
            RefreshBoard();
            SetNotification("已删除任务");
        }
        catch (Exception ex)
        {
            var filesRestored = TryRollbackStagedDeletes(stagedDeletes);
            var restored = await RestoreDeletedTaskRecordsAsync(task, attachmentsDeleted, taskDeleted);
            SetNotification(filesRestored && restored
                ? $"删除任务失败：{ex.Message}"
                : $"删除任务失败，且恢复附件文件或数据库记录失败：{ex.Message}");
        }
    }

    private async Task DeleteSelectedNoteAsync()
    {
        if (SelectedNote is null)
        {
            return;
        }

        var note = SelectedNote;

        if (MessageBox.Show(
                "删除后会同时删除这张备忘的本地附件，是否继续？",
                "删除备忘",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        IReadOnlyList<StagedAttachmentDelete> stagedDeletes = [];
        var attachmentsDeleted = false;
        var noteDeleted = false;

        try
        {
            stagedDeletes = StageAttachmentDeletes(note.Attachments);
            await _attachmentRepository.DeleteByOwnerAsync(AttachmentOwnerType.Note, note.Id);
            attachmentsDeleted = true;
            await _noteRepository.DeleteAsync(note.Id);
            noteDeleted = true;
            CommitStagedDeletes(stagedDeletes);
            UntrackNote(note);
            _allNotes.Remove(note);
            SelectedNote = null;
            ClearNoteDraft();
            RefreshBoard();
            SetNotification("已删除备忘");
        }
        catch (Exception ex)
        {
            var filesRestored = TryRollbackStagedDeletes(stagedDeletes);
            var restored = await RestoreDeletedNoteRecordsAsync(note, attachmentsDeleted, noteDeleted);
            SetNotification(filesRestored && restored
                ? $"删除备忘失败：{ex.Message}"
                : $"删除备忘失败，且恢复附件文件或数据库记录失败：{ex.Message}");
        }
    }

    private async Task MoveCardAsync(object? parameter)
    {
        if (parameter is not CardDropPayload payload)
        {
            return;
        }

        if (payload.Item is TaskItem task && payload.TargetColumn != KanbanColumnKind.Notes)
        {
            await MoveTaskToColumnAsync(task, payload.TargetColumn, payload.TargetIndex);
            return;
        }

        if (payload.Item is NoteItem note && payload.TargetColumn == KanbanColumnKind.Notes)
        {
            await MoveNoteAsync(note, payload.TargetIndex);
        }
    }

    private async Task MoveTaskToColumnAsync(TaskItem task, KanbanColumnKind targetColumn, int targetIndex)
    {
        var sourceStatus = task.Status;
        var targetStatus = targetColumn switch
        {
            KanbanColumnKind.Todo => TaskStatus.Todo,
            KanbanColumnKind.Doing => TaskStatus.Doing,
            KanbanColumnKind.Blocked => TaskStatus.Blocked,
            KanbanColumnKind.Done => TaskStatus.Done,
            _ => task.Status
        };

        var currentList = _allTasks
            .Where(item => !item.IsArchived && item.Status == targetStatus)
            .OrderBy(item => item.SortOrder)
            .ToList();
        var originalIndex = currentList.FindIndex(item => item.Id == task.Id);
        var insertIndex = targetIndex;

        if (task.Status == targetStatus && originalIndex >= 0 && insertIndex > originalIndex)
        {
            insertIndex--;
        }

        currentList.RemoveAll(item => item.Id == task.Id);
        insertIndex = Math.Clamp(insertIndex, 0, currentList.Count);

        _isLoading = true;
        task.Status = targetStatus;
        currentList.Insert(insertIndex, task);

        var sourceList = sourceStatus == targetStatus
            ? new List<TaskItem>()
            : _allTasks
                .Where(item => !item.IsArchived && item.Status == sourceStatus && item.Id != task.Id)
                .OrderBy(item => item.SortOrder)
                .ToList();

        for (var index = 0; index < currentList.Count; index++)
        {
            currentList[index].SortOrder = index;
        }

        for (var index = 0; index < sourceList.Count; index++)
        {
            sourceList[index].SortOrder = index;
        }

        _isLoading = false;
        RefreshBoard();

        foreach (var item in currentList.Concat(sourceList))
        {
            if (!await SaveTaskSafelyAsync(item))
            {
                return;
            }
        }

        SetNotification("任务顺序已更新");
    }

    private async Task MoveNoteAsync(NoteItem note, int targetIndex)
    {
        var currentList = _allNotes
            .Where(item => !item.IsArchived)
            .OrderBy(item => item.SortOrder)
            .ToList();
        var originalIndex = currentList.FindIndex(item => item.Id == note.Id);
        var insertIndex = targetIndex;

        if (originalIndex >= 0 && insertIndex > originalIndex)
        {
            insertIndex--;
        }

        currentList.RemoveAll(item => item.Id == note.Id);
        insertIndex = Math.Clamp(insertIndex, 0, currentList.Count);

        _isLoading = true;
        currentList.Insert(insertIndex, note);

        for (var index = 0; index < currentList.Count; index++)
        {
            currentList[index].SortOrder = index;
        }

        _isLoading = false;
        RefreshBoard();

        foreach (var item in currentList)
        {
            if (!await SaveNoteSafelyAsync(item))
            {
                return;
            }
        }

        SetNotification("备忘顺序已更新");
    }

    private async Task AttachFilesAsync(object? parameter)
    {
        if (parameter is not FileDropPayload payload)
        {
            return;
        }

        IReadOnlyList<AttachmentItem> copiedAttachments = [];

        try
        {
            if (payload.Owner is TaskItem task)
            {
                copiedAttachments = await _attachmentStorage.CopyFilesAsync(AttachmentOwnerType.Task, task.Id, payload.FilePaths);
                await _attachmentRepository.AddRangeAsync(copiedAttachments);

                foreach (var attachment in copiedAttachments)
                {
                    task.Attachments.Add(attachment);
                }

                RefreshBoard();
                SetNotification($"已保存 {copiedAttachments.Count} 个附件");
                return;
            }

            if (payload.Owner is NoteItem note)
            {
                copiedAttachments = await _attachmentStorage.CopyFilesAsync(AttachmentOwnerType.Note, note.Id, payload.FilePaths);
                await _attachmentRepository.AddRangeAsync(copiedAttachments);

                foreach (var attachment in copiedAttachments)
                {
                    note.Attachments.Add(attachment);
                }

                RefreshBoard();
                SetNotification($"已保存 {copiedAttachments.Count} 个附件");
            }
        }
        catch (Exception ex)
        {
            foreach (var attachment in copiedAttachments)
            {
                try
                {
                    await _attachmentStorage.DeleteAttachmentFileAsync(attachment);
                }
                catch
                {
                    // Best-effort cleanup after a failed database write.
                }
            }

            SetNotification(ex.Message);
        }
    }

    private async Task PickFilesAsync(object? owner)
    {
        if (owner is not TaskItem and not NoteItem)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            Title = "选择附件"
        };

        if (dialog.ShowDialog() == true)
        {
            await AttachFilesAsync(new FileDropPayload(owner, dialog.FileNames));
        }
    }

    private void OpenAttachment(object? parameter)
    {
        if (parameter is not AttachmentItem attachment)
        {
            return;
        }

        try
        {
            _attachmentStorage.OpenAttachment(attachment);
        }
        catch (Exception ex)
        {
            SetNotification(ex.Message);
        }
    }

    private void RevealAttachment(object? parameter)
    {
        if (parameter is not AttachmentItem attachment)
        {
            return;
        }

        try
        {
            _attachmentStorage.RevealAttachment(attachment);
        }
        catch (Exception ex)
        {
            SetNotification(ex.Message);
        }
    }

    private async Task DeleteAttachmentAsync(object? parameter)
    {
        if (parameter is not AttachmentItem attachment)
        {
            return;
        }

        StagedAttachmentDelete? stagedDelete = null;
        var databaseDeleted = false;

        try
        {
            stagedDelete = _attachmentStorage.StageAttachmentFileForDelete(attachment);
            await _attachmentRepository.DeleteAsync(attachment.Id);
            databaseDeleted = true;
            _attachmentStorage.CommitStagedDelete(stagedDelete);
            var owner = attachment.OwnerType == AttachmentOwnerType.Task
                ? _allTasks.FirstOrDefault(task => task.Id == attachment.OwnerId)?.Attachments
                : _allNotes.FirstOrDefault(note => note.Id == attachment.OwnerId)?.Attachments;

            owner?.Remove(attachment);
            RefreshBoard();
            SetNotification("已删除附件");
        }
        catch (Exception ex)
        {
            var fileRestored = true;

            if (stagedDelete is not null)
            {
                fileRestored = TryRollbackStagedDelete(stagedDelete);
            }

            if (databaseDeleted)
            {
                var restored = await RestoreAttachmentRecordAsync(attachment);
                SetNotification(fileRestored && restored
                    ? $"删除附件失败：{ex.Message}"
                    : $"删除附件失败，且恢复附件文件或数据库记录失败：{ex.Message}");
                return;
            }

            SetNotification(fileRestored
                ? $"删除附件失败：{ex.Message}"
                : $"删除附件失败，且恢复附件文件失败：{ex.Message}");
        }
    }

    private async Task<bool> RestoreDeletedTaskRecordsAsync(TaskItem task, bool attachmentsDeleted, bool taskDeleted)
    {
        try
        {
            if (taskDeleted)
            {
                await _taskRepository.UpsertAsync(task);
            }

            if (attachmentsDeleted)
            {
                await _attachmentRepository.AddRangeAsync(task.Attachments);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> RestoreDeletedNoteRecordsAsync(NoteItem note, bool attachmentsDeleted, bool noteDeleted)
    {
        try
        {
            if (noteDeleted)
            {
                await _noteRepository.UpsertAsync(note);
            }

            if (attachmentsDeleted)
            {
                await _attachmentRepository.AddRangeAsync(note.Attachments);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> RestoreAttachmentRecordAsync(AttachmentItem attachment)
    {
        try
        {
            await _attachmentRepository.AddRangeAsync([attachment]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private IReadOnlyList<StagedAttachmentDelete> StageAttachmentDeletes(IEnumerable<AttachmentItem> attachments)
    {
        var stagedDeletes = new List<StagedAttachmentDelete>();

        try
        {
            foreach (var attachment in attachments.ToArray())
            {
                stagedDeletes.Add(_attachmentStorage.StageAttachmentFileForDelete(attachment));
            }

            return stagedDeletes;
        }
        catch
        {
            TryRollbackStagedDeletes(stagedDeletes);
            throw;
        }
    }

    private void CommitStagedDeletes(IEnumerable<StagedAttachmentDelete> stagedDeletes)
    {
        foreach (var stagedDelete in stagedDeletes)
        {
            _attachmentStorage.CommitStagedDelete(stagedDelete);
        }
    }

    private bool TryRollbackStagedDeletes(IEnumerable<StagedAttachmentDelete> stagedDeletes)
    {
        var succeeded = true;

        foreach (var stagedDelete in stagedDeletes.Reverse())
        {
            succeeded &= TryRollbackStagedDelete(stagedDelete);
        }

        return succeeded;
    }

    private bool TryRollbackStagedDelete(StagedAttachmentDelete stagedDelete)
    {
        try
        {
            _attachmentStorage.RollbackStagedDelete(stagedDelete);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ChangeFilter(object? parameter)
    {
        var filter = parameter as string ?? "Board";

        if (!ConfirmDiscardDrawerChanges())
        {
            return;
        }

        _currentFilter = filter;
        OnPropertyChanged(nameof(CurrentFilter));
        SelectedTask = null;
        SelectedNote = null;
        ClearTaskDraft();
        ClearNoteDraft();
        CurrentFilterLabel = filter switch
        {
            "AllTasks" => "全部任务",
            "Today" => "今日任务",
            "Calendar" => "日历",
            "High" => "高优先级",
            "WithAttachments" => "有附件",
            "Backup" => "数据备份",
            "Settings" => "设置",
            _ => "看板"
        };

        OnPropertyChanged(nameof(IsBoardViewVisible));
        OnPropertyChanged(nameof(IsCalendarViewVisible));
        OnPropertyChanged(nameof(IsBackupViewVisible));
        OnPropertyChanged(nameof(IsSettingsViewVisible));
        RefreshBoard();
    }

    private async Task CreateBackupAsync()
    {
        try
        {
            if (!await EnsureDraftsReadyForBackupAsync())
            {
                return;
            }

            await SaveAllAsync();
            var result = await _backupService.CreateBackupAsync();
            LastBackupPath = result.BackupPath;
            LastBackupAttachmentCount = result.AttachmentFileCount;
            LastBackupSizeBytes = result.BackupSizeBytes;
            LastBackupCreatedAt = result.CreatedAt;
            SetNotification($"备份已创建：{result.BackupPath}");
        }
        catch (Exception ex)
        {
            SetNotification($"创建备份失败：{ex.Message}");
        }
    }

    private async Task RestoreBackupAsync()
    {
        if (!ConfirmDiscardDrawerChanges())
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Title = "选择 Kanban41 备份文件",
            InitialDirectory = Directory.Exists(BackupDirectory) ? BackupDirectory : DataDirectory,
            Filter = "Kanban41 备份 (*.zip)|*.zip|所有文件 (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (MessageBox.Show(
                "恢复备份会覆盖当前数据库和附件。恢复前会自动创建一份当前数据的保护备份，是否继续？",
                "恢复备份",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            SelectedTask = null;
            SelectedNote = null;
            ClearTaskDraft();
            ClearNoteDraft();

            var result = await _backupService.RestoreBackupAsync(dialog.FileName);
            await InitializeAsync();

            LastRestoreSourcePath = result.SourceBackupPath;
            LastRestoreProtectiveBackupPath = result.ProtectiveBackupPath;
            LastRestoreAttachmentCount = result.AttachmentFileCount;
            LastRestoreAt = result.RestoredAt;
            SetNotification($"已恢复备份：{Path.GetFileName(result.SourceBackupPath)}");
        }
        catch (Exception ex)
        {
            SetNotification($"恢复备份失败：{ex.Message}");
        }
    }

    private void RefreshBoard()
    {
        var tasks = _allTasks
            .Where(task => !task.IsArchived)
            .Where(PassesTaskFilter)
            .Where(PassesTaskSearch)
            .OrderBy(task => task.SortOrder)
            .ToArray();

        Replace(TodoTasks, tasks.Where(task => task.Status == TaskStatus.Todo));
        Replace(DoingTasks, tasks.Where(task => task.Status == TaskStatus.Doing));
        Replace(BlockedTasks, tasks.Where(task => task.Status == TaskStatus.Blocked));
        Replace(DoneTasks, tasks.Where(task => task.Status == TaskStatus.Done));

        var notes = _allNotes
            .Where(note => !note.IsArchived)
            .Where(PassesNoteFilter)
            .Where(PassesNoteSearch)
            .OrderBy(note => note.SortOrder);

        Replace(Notes, _currentFilter is "Board" or "WithAttachments" ? notes : Enumerable.Empty<NoteItem>());

        Replace(
            CalendarTasks,
            _allTasks
                .Where(task => !task.IsArchived && (task.StartDate.HasValue || task.EndDate.HasValue))
                .Where(PassesTaskSearch)
                .OrderBy(task => task.StartDate ?? task.EndDate)
                .ThenBy(task => task.EndDate ?? task.StartDate)
                .ThenBy(task => task.SortOrder));

        OnPropertyChanged(nameof(VisibleTaskCount));
        OnPropertyChanged(nameof(VisibleNoteCount));
        OnPropertyChanged(nameof(CalendarTaskCount));
    }

    private bool PassesTaskFilter(TaskItem task)
    {
        return _currentFilter switch
        {
            "Today" => IsTodayWithinTaskDateRange(task),
            "High" => task.Priority == TaskPriority.High,
            "WithAttachments" => task.AttachmentCount > 0,
            _ => true
        };
    }

    private static bool IsTodayWithinTaskDateRange(TaskItem task)
    {
        if (task.StartDate is null || task.EndDate is null)
        {
            return false;
        }

        var today = DateTime.Today;
        var startDate = task.StartDate.Value.Date;
        var endDate = task.EndDate.Value.Date;

        if (startDate > endDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        return startDate <= today && today <= endDate;
    }

    private bool PassesNoteFilter(NoteItem note)
    {
        return _currentFilter switch
        {
            "WithAttachments" => note.AttachmentCount > 0,
            _ => true
        };
    }

    private bool PassesTaskSearch(TaskItem task)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var query = SearchText.Trim();
        return task.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || task.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || task.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase))
            || task.Attachments.Any(attachment => attachment.OriginalFileName.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private bool PassesNoteSearch(NoteItem note)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var query = SearchText.Trim();
        return note.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || note.Content.Contains(query, StringComparison.OrdinalIgnoreCase)
            || note.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase))
            || note.Attachments.Any(attachment => attachment.OriginalFileName.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private int NextTaskSortOrder(TaskStatus status)
    {
        return _allTasks.Where(task => task.Status == status).Select(task => task.SortOrder).DefaultIfEmpty().Max() + 1;
    }

    private int NextNoteSortOrder()
    {
        return _allNotes.Select(note => note.SortOrder).DefaultIfEmpty().Max() + 1;
    }

    private void AddTaskToMemory(TaskItem task)
    {
        TrackTask(task);
        _allTasks.Add(task);
    }

    private void AddNoteToMemory(NoteItem note)
    {
        TrackNote(note);
        _allNotes.Add(note);
    }

    private void TrackTask(TaskItem task)
    {
        task.PropertyChanged -= OnTaskPropertyChanged;
        task.PropertyChanged += OnTaskPropertyChanged;
    }

    private void UntrackTask(TaskItem task)
    {
        task.PropertyChanged -= OnTaskPropertyChanged;
    }

    private void TrackNote(NoteItem note)
    {
        note.PropertyChanged -= OnNotePropertyChanged;
        note.PropertyChanged += OnNotePropertyChanged;
    }

    private void UntrackNote(NoteItem note)
    {
        note.PropertyChanged -= OnNotePropertyChanged;
    }

    private async void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isInitialized || _isLoading || sender is not TaskItem task)
        {
            return;
        }

        if (e.PropertyName is nameof(TaskItem.AttachmentCount)
            or nameof(TaskItem.UpdatedAt)
            or nameof(TaskItem.CompletedAt)
            or nameof(TaskItem.DateRangeDisplay))
        {
            return;
        }

        RefreshBoard();
        await SaveTaskSafelyAsync(task);
    }

    private async void OnNotePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isInitialized || _isLoading || sender is not NoteItem note)
        {
            return;
        }

        if (e.PropertyName is nameof(NoteItem.AttachmentCount) or nameof(NoteItem.UpdatedAt))
        {
            return;
        }

        RefreshBoard();
        await SaveNoteSafelyAsync(note);
    }

    private async Task SaveSelectedTaskAsync()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var task = SelectedTask;
        var oldTitle = task.Title;
        var oldDescription = task.Description;
        var oldTags = task.TagsDisplay;
        var oldStatus = task.Status;
        var oldPriority = task.Priority;
        var oldStartDate = task.StartDate;
        var oldEndDate = task.EndDate;
        var oldCompletedAt = task.CompletedAt;
        var oldUpdatedAt = task.UpdatedAt;

        try
        {
            _isLoading = true;
            var startDate = TaskStartDateDraft?.Date;
            var endDate = TaskEndDateDraft?.Date;

            if (startDate.HasValue && endDate.HasValue && startDate.Value > endDate.Value)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            task.Title = string.IsNullOrWhiteSpace(TaskTitleDraft) ? "未命名任务" : TaskTitleDraft.Trim();
            task.Description = TaskDescriptionDraft;
            task.TagsDisplay = TaskTagsDraft;
            task.Status = TaskStatusDraft;
            task.Priority = TaskPriorityDraft;
            task.StartDate = startDate;
            task.EndDate = endDate;
            await _taskRepository.UpsertAsync(task);
            _isLoading = false;

            LoadTaskDraft(task);
            RefreshBoard();
            SetNotification("已保存");
        }
        catch (Exception ex)
        {
            task.Title = oldTitle;
            task.Description = oldDescription;
            task.TagsDisplay = oldTags;
            task.Status = oldStatus;
            task.Priority = oldPriority;
            task.StartDate = oldStartDate;
            task.EndDate = oldEndDate;
            task.CompletedAt = oldCompletedAt;
            task.UpdatedAt = oldUpdatedAt;
            _isLoading = false;
            RefreshBoard();
            OnPropertyChanged(nameof(HasUnsavedTaskChanges));
            SaveTaskCommand.RaiseCanExecuteChanged();
            SetNotification($"保存任务失败：{ex.Message}");
        }
    }

    private async Task SaveSelectedNoteAsync()
    {
        if (SelectedNote is null)
        {
            return;
        }

        var note = SelectedNote;
        var oldTitle = note.Title;
        var oldContent = note.Content;
        var oldTags = note.TagsDisplay;
        var oldUpdatedAt = note.UpdatedAt;

        try
        {
            _isLoading = true;
            note.Title = string.IsNullOrWhiteSpace(NoteTitleDraft) ? "未命名备忘" : NoteTitleDraft.Trim();
            note.Content = NoteContentDraft;
            note.TagsDisplay = NoteTagsDraft;
            await _noteRepository.UpsertAsync(note);
            _isLoading = false;

            LoadNoteDraft(note);
            RefreshBoard();
            SetNotification("已保存");
        }
        catch (Exception ex)
        {
            note.Title = oldTitle;
            note.Content = oldContent;
            note.TagsDisplay = oldTags;
            note.UpdatedAt = oldUpdatedAt;
            _isLoading = false;
            RefreshBoard();
            OnPropertyChanged(nameof(HasUnsavedNoteChanges));
            SaveNoteCommand.RaiseCanExecuteChanged();
            SetNotification($"保存备忘失败：{ex.Message}");
        }
    }

    private async Task<bool> SaveTaskSafelyAsync(TaskItem task, string? successMessage = null)
    {
        if (!_isInitialized)
        {
            return false;
        }

        try
        {
            await _taskRepository.UpsertAsync(task);

            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                SetNotification(successMessage);
            }

            return true;
        }
        catch (Exception ex)
        {
            SetNotification($"保存任务失败：{ex.Message}");
            return false;
        }
    }

    private async Task<bool> SaveNoteSafelyAsync(NoteItem note, string? successMessage = null)
    {
        if (!_isInitialized)
        {
            return false;
        }

        try
        {
            await _noteRepository.UpsertAsync(note);

            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                SetNotification(successMessage);
            }

            return true;
        }
        catch (Exception ex)
        {
            SetNotification($"保存备忘失败：{ex.Message}");
            return false;
        }
    }

    private void LoadTaskDraft(TaskItem task)
    {
        _taskTitleDraft = task.Title;
        _taskDescriptionDraft = task.Description;
        _taskTagsDraft = task.TagsDisplay;
        _taskStatusDraft = task.Status;
        _taskPriorityDraft = task.Priority;
        _taskStartDateDraft = task.StartDate;
        _taskEndDateDraft = task.EndDate;
        OnPropertyChanged(nameof(TaskTitleDraft));
        OnPropertyChanged(nameof(TaskDescriptionDraft));
        OnPropertyChanged(nameof(TaskTagsDraft));
        OnPropertyChanged(nameof(TaskStatusDraft));
        OnPropertyChanged(nameof(TaskPriorityDraft));
        OnPropertyChanged(nameof(TaskStartDateDraft));
        OnPropertyChanged(nameof(TaskEndDateDraft));
        OnPropertyChanged(nameof(HasUnsavedTaskChanges));
        SaveTaskCommand.RaiseCanExecuteChanged();
    }

    private void ClearTaskDraft()
    {
        _taskTitleDraft = string.Empty;
        _taskDescriptionDraft = string.Empty;
        _taskTagsDraft = string.Empty;
        _taskStatusDraft = TaskStatus.Todo;
        _taskPriorityDraft = TaskPriority.Medium;
        _taskStartDateDraft = null;
        _taskEndDateDraft = null;
        OnPropertyChanged(nameof(TaskTitleDraft));
        OnPropertyChanged(nameof(TaskDescriptionDraft));
        OnPropertyChanged(nameof(TaskTagsDraft));
        OnPropertyChanged(nameof(TaskStatusDraft));
        OnPropertyChanged(nameof(TaskPriorityDraft));
        OnPropertyChanged(nameof(TaskStartDateDraft));
        OnPropertyChanged(nameof(TaskEndDateDraft));
        OnPropertyChanged(nameof(HasUnsavedTaskChanges));
        SaveTaskCommand.RaiseCanExecuteChanged();
    }

    private void OnTaskDraftChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedTaskChanges));
        SaveTaskCommand.RaiseCanExecuteChanged();
    }

    private void LoadNoteDraft(NoteItem note)
    {
        _noteTitleDraft = note.Title;
        _noteContentDraft = note.Content;
        _noteTagsDraft = note.TagsDisplay;
        OnPropertyChanged(nameof(NoteTitleDraft));
        OnPropertyChanged(nameof(NoteContentDraft));
        OnPropertyChanged(nameof(NoteTagsDraft));
        OnPropertyChanged(nameof(HasUnsavedNoteChanges));
        SaveNoteCommand.RaiseCanExecuteChanged();
    }

    private void ClearNoteDraft()
    {
        _noteTitleDraft = string.Empty;
        _noteContentDraft = string.Empty;
        _noteTagsDraft = string.Empty;
        OnPropertyChanged(nameof(NoteTitleDraft));
        OnPropertyChanged(nameof(NoteContentDraft));
        OnPropertyChanged(nameof(NoteTagsDraft));
        OnPropertyChanged(nameof(HasUnsavedNoteChanges));
        SaveNoteCommand.RaiseCanExecuteChanged();
    }

    private void OnNoteDraftChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedNoteChanges));
        SaveNoteCommand.RaiseCanExecuteChanged();
    }

    private bool ConfirmDiscardNoteChanges()
    {
        return true;
    }

    private bool ConfirmDiscardTaskChanges()
    {
        return true;
    }

    private bool ConfirmDiscardDrawerChanges()
    {
        return ConfirmDiscardTaskChanges() && ConfirmDiscardNoteChanges();
    }

    private Task<bool> EnsureDraftsReadyForBackupAsync()
    {
        return Task.FromResult(true);
    }

    private async Task SaveAllAsync()
    {
        foreach (var task in _allTasks)
        {
            await _taskRepository.UpsertAsync(task);
        }

        foreach (var note in _allNotes)
        {
            await _noteRepository.UpsertAsync(note);
        }
    }

    private void SetNotification(string message)
    {
        NotificationText = message;
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();

        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
