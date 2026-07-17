using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using KanbanForOne.Controls;
using KanbanForOne.Models;
using KanbanForOne.Services;
using Microsoft.Win32;
using TaskStatus = KanbanForOne.Models.TaskStatus;

namespace KanbanForOne.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private static readonly Assembly AppAssembly = typeof(MainWindowViewModel).Assembly;
    private readonly ObservableCollection<TaskItem> _allTasks = new();
    private readonly ObservableCollection<NoteItem> _allNotes = new();
    private readonly DatabaseService _databaseService = new();
    private readonly AttachmentStorageService _attachmentStorage = new();
    private readonly TaskRepository _taskRepository;
    private readonly NoteRepository _noteRepository;
    private readonly AttachmentRepository _attachmentRepository;
    private readonly ArchiveSectionRepository _archiveSectionRepository;
    private readonly WorkHourRepository _workHourRepository;
    private readonly WorkHourOptionsService _workHourOptionsService = new();
    private readonly BackupService _backupService = new();
    private bool _isInitialized;
    private bool _isLoading;
    private string _searchText = string.Empty;
    private string _normalizedSearchText = string.Empty;
    private CancellationTokenSource? _searchRefreshCancellation;
    private DateTime? _dateFilterStart;
    private DateTime? _dateFilterEnd;
    private string _currentFilter = "Board";
    private ArchiveSection? _selectedArchiveSection;
    private Guid? _loadedArchiveSectionId;
    private bool _isArchiveContentLoaded;
    private string _newArchiveSectionName = string.Empty;
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
    private DateTime _calendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _selectedCalendarDate = DateTime.Today;
    private string _calendarViewMode = "Month";
    private int _workHourLoadVersion;
    private string _newWorkDiscipline = string.Empty;
    private string _newWorkActivity = string.Empty;
    private TaskItem? _selectedTask;
    private NoteItem? _selectedNote;
    private TaskItem? _focusedTask;
    private NoteItem? _focusedNote;
    private object? _spotlightDetailDataContext;
    private bool _isTaskSpotlightEditing;
    private bool _isNoteSpotlightEditing;
    private double _spotlightSourceLeft = 260;
    private double _spotlightSourceTop = 96;
    private double _spotlightSourceWidth = 240;
    private double _spotlightSourceHeight = 140;
    private double _spotlightLeft = 260;
    private double _spotlightTop = 96;
    private double _spotlightWidth = 640;
    private double _spotlightHeight = 540;
    private Size _spotlightHostSize = new(1180, 820);

    public MainWindowViewModel()
    {
        RelayCommand.UnhandledException -= OnCommandUnhandledException;
        RelayCommand.UnhandledException += OnCommandUnhandledException;

        _taskRepository = new TaskRepository(_databaseService);
        _noteRepository = new NoteRepository(_databaseService);
        _attachmentRepository = new AttachmentRepository(_databaseService);
        _archiveSectionRepository = new ArchiveSectionRepository(_databaseService);
        _workHourRepository = new WorkHourRepository(_databaseService);
        WorkHourSummary = new WorkHourSummaryViewModel(
            _workHourRepository,
            new WorkHourExportService(),
            SetNotification);

        TaskStatusOptions = Enum.GetValues<TaskStatus>();
        TaskPriorityOptions = Enum.GetValues<TaskPriority>();

        CreateTaskCommand = new RelayCommand(CreateTaskAsync);
        CreateNoteCommand = new RelayCommand(CreateNoteAsync);
        OpenTaskCommand = new RelayCommand(OpenTaskFromParameter);
        OpenNoteCommand = new RelayCommand(OpenNoteFromParameter);
        CloseSpotlightCommand = new RelayCommand(CloseSpotlight);
        EditSpotlightCommand = new RelayCommand(_ => EnterSpotlightEditMode(), _ => IsSpotlightOpen && !IsSpotlightEditing);
        CancelSpotlightEditCommand = new RelayCommand(_ => CancelSpotlightEdit(), _ => IsSpotlightEditing);
        ArchiveTaskCommand = new RelayCommand(ArchiveTaskAsync, _ => ActiveTask is not null);
        ArchiveNoteCommand = new RelayCommand(ArchiveNoteAsync, _ => ActiveNote is not null);
        DeleteTaskCommand = new RelayCommand(DeleteSelectedTaskAsync, _ => ActiveTask is not null);
        DeleteNoteCommand = new RelayCommand(DeleteSelectedNoteAsync, _ => ActiveNote is not null);
        SaveTaskCommand = new RelayCommand(SaveSelectedTaskAsync, _ => ActiveTask is not null && HasUnsavedTaskChanges);
        SaveNoteCommand = new RelayCommand(SaveSelectedNoteAsync, _ => ActiveNote is not null && HasUnsavedNoteChanges);
        MoveCardCommand = new RelayCommand(MoveCardAsync);
        AttachFilesCommand = new RelayCommand(AttachFilesAsync);
        PickFilesCommand = new RelayCommand(PickFilesAsync);
        PasteClipboardImageCommand = new RelayCommand(PasteClipboardImageAsync, _ => IsSpotlightEditing);
        OpenAttachmentCommand = new RelayCommand(OpenAttachment);
        RevealAttachmentCommand = new RelayCommand(RevealAttachment);
        DeleteAttachmentCommand = new RelayCommand(DeleteAttachmentAsync);
        CreateBackupCommand = new RelayCommand(CreateBackupAsync);
        RestoreBackupCommand = new RelayCommand(RestoreBackupAsync);
        ChangeFilterCommand = new RelayCommand(ChangeFilterAsync);
        SelectArchiveSectionCommand = new RelayCommand(SelectArchiveSectionAsync);
        OpenArchiveSectionPickerCommand = new RelayCommand(OpenArchiveSectionPickerAsync);
        CreateArchiveSectionCommand = new RelayCommand(CreateArchiveSectionAsync, _ => !string.IsNullOrWhiteSpace(NewArchiveSectionName));
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        ClearDateFilterCommand = new RelayCommand(_ => ClearDateFilter(), _ => HasDateFilter);
        PreviousCalendarMonthCommand = new RelayCommand(() => CalendarMonth = CalendarMonth.AddMonths(-1));
        NextCalendarMonthCommand = new RelayCommand(() => CalendarMonth = CalendarMonth.AddMonths(1));
        GoToTodayCommand = new RelayCommand(GoToToday);
        SelectCalendarDateCommand = new RelayCommand(SelectCalendarDate);
        CreateTaskForCalendarDateCommand = new RelayCommand(CreateTaskForCalendarDateAsync);
        MoveTaskToCalendarDateCommand = new RelayCommand(MoveTaskToCalendarDateAsync);
        SetCalendarViewModeCommand = new RelayCommand(SetCalendarViewMode);
        AddWorkHourCommand = new RelayCommand(AddWorkHourAsync);
        OpenWorkHourCommand = new RelayCommand(OpenWorkHourAsync);
        AddWorkDisciplineCommand = new RelayCommand(AddWorkDisciplineAsync, _ => !string.IsNullOrWhiteSpace(NewWorkDiscipline));
        RemoveWorkDisciplineCommand = new RelayCommand(RemoveWorkDisciplineAsync);
        AddWorkActivityCommand = new RelayCommand(AddWorkActivityAsync, _ => !string.IsNullOrWhiteSpace(NewWorkActivity));
        RemoveWorkActivityCommand = new RelayCommand(RemoveWorkActivityAsync);

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

    public ObservableCollection<CalendarDayItem> CalendarDays { get; } = new();

    public ObservableCollection<TaskItem> SelectedCalendarTasks { get; } = new();

    public ObservableCollection<WorkHourEntry> SelectedCalendarWorkHours { get; } = new();

    public ObservableCollection<string> WorkDisciplines { get; } = new();

    public ObservableCollection<string> WorkActivities { get; } = new();

    public WorkHourSummaryViewModel WorkHourSummary { get; }

    public ObservableCollection<ArchiveSection> ArchiveSections { get; } = new();

    public IReadOnlyList<string> CalendarWeekdayHeaders { get; } = ["周一", "周二", "周三", "周四", "周五", "周六", "周日"];

    public RelayCommand CreateTaskCommand { get; }

    public RelayCommand CreateNoteCommand { get; }

    public RelayCommand OpenTaskCommand { get; }

    public RelayCommand OpenNoteCommand { get; }

    public RelayCommand CloseSpotlightCommand { get; }

    public RelayCommand EditSpotlightCommand { get; }

    public RelayCommand CancelSpotlightEditCommand { get; }

    public RelayCommand ArchiveTaskCommand { get; }

    public RelayCommand ArchiveNoteCommand { get; }

    public RelayCommand DeleteTaskCommand { get; }

    public RelayCommand DeleteNoteCommand { get; }

    public RelayCommand SaveTaskCommand { get; }

    public RelayCommand SaveNoteCommand { get; }

    public RelayCommand MoveCardCommand { get; }

    public RelayCommand AttachFilesCommand { get; }

    public RelayCommand PickFilesCommand { get; }

    public RelayCommand PasteClipboardImageCommand { get; }

    public RelayCommand OpenAttachmentCommand { get; }

    public RelayCommand RevealAttachmentCommand { get; }

    public RelayCommand DeleteAttachmentCommand { get; }

    public RelayCommand CreateBackupCommand { get; }

    public RelayCommand RestoreBackupCommand { get; }

    public RelayCommand ChangeFilterCommand { get; }

    public RelayCommand SelectArchiveSectionCommand { get; }

    public RelayCommand OpenArchiveSectionPickerCommand { get; }

    public RelayCommand CreateArchiveSectionCommand { get; }

    public RelayCommand ClearSearchCommand { get; }

    public RelayCommand ClearDateFilterCommand { get; }

    public RelayCommand PreviousCalendarMonthCommand { get; }

    public RelayCommand NextCalendarMonthCommand { get; }

    public RelayCommand GoToTodayCommand { get; }

    public RelayCommand SelectCalendarDateCommand { get; }

    public RelayCommand CreateTaskForCalendarDateCommand { get; }

    public RelayCommand MoveTaskToCalendarDateCommand { get; }

    public RelayCommand SetCalendarViewModeCommand { get; }

    public RelayCommand AddWorkHourCommand { get; }

    public RelayCommand OpenWorkHourCommand { get; }

    public RelayCommand AddWorkDisciplineCommand { get; }

    public RelayCommand RemoveWorkDisciplineCommand { get; }

    public RelayCommand AddWorkActivityCommand { get; }

    public RelayCommand RemoveWorkActivityCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _normalizedSearchText = _searchText.Trim();
                ScheduleSearchRefresh();
            }
        }
    }

    public DateTime? DateFilterStart
    {
        get => _dateFilterStart;
        set
        {
            if (SetProperty(ref _dateFilterStart, value?.Date))
            {
                OnDateFilterChanged();
            }
        }
    }

    public DateTime? DateFilterEnd
    {
        get => _dateFilterEnd;
        set
        {
            if (SetProperty(ref _dateFilterEnd, value?.Date))
            {
                OnDateFilterChanged();
            }
        }
    }

    public bool HasDateFilter => DateFilterStart.HasValue || DateFilterEnd.HasValue;

    public string CurrentFilterLabel
    {
        get => _currentFilterLabel;
        private set => SetProperty(ref _currentFilterLabel, value);
    }

    public string CurrentFilter => _currentFilter;

    public bool IsArchiveFilter => _currentFilter == "Archived";

    public ArchiveSection? SelectedArchiveSection
    {
        get => _selectedArchiveSection;
        private set
        {
            if (ReferenceEquals(_selectedArchiveSection, value))
            {
                return;
            }

            if (_selectedArchiveSection is not null)
            {
                _selectedArchiveSection.IsSelected = false;
            }

            _selectedArchiveSection = value;

            if (_selectedArchiveSection is not null)
            {
                _selectedArchiveSection.IsSelected = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedArchiveSection));
            OnPropertyChanged(nameof(ArchiveSectionSelectorText));
        }
    }

    public bool HasSelectedArchiveSection => SelectedArchiveSection is not null;

    public string ArchiveSectionSelectorText => SelectedArchiveSection is null
        ? "选择分区"
        : $"{SelectedArchiveSection.Name} · {SelectedArchiveSection.TotalCount}";

    public string ArchiveSectionSelectorHint => $"{ArchiveSections.Count} 个分区";

    public bool IsArchiveContentLoaded
    {
        get => _isArchiveContentLoaded;
        private set
        {
            if (SetProperty(ref _isArchiveContentLoaded, value))
            {
                OnPropertyChanged(nameof(IsArchiveWaitingForSection));
            }
        }
    }

    public bool IsArchiveWaitingForSection => IsArchiveFilter && !IsArchiveContentLoaded;

    public string NewArchiveSectionName
    {
        get => _newArchiveSectionName;
        set
        {
            if (SetProperty(ref _newArchiveSectionName, value))
            {
                CreateArchiveSectionCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewWorkDiscipline
    {
        get => _newWorkDiscipline;
        set
        {
            if (SetProperty(ref _newWorkDiscipline, value))
            {
                AddWorkDisciplineCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewWorkActivity
    {
        get => _newWorkActivity;
        set
        {
            if (SetProperty(ref _newWorkActivity, value))
            {
                AddWorkActivityCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string TaskArchiveActionText => ActiveTask?.IsArchived == true ? "恢复" : "归档";

    public string NoteArchiveActionText => ActiveNote?.IsArchived == true ? "恢复" : "归档";

    public bool IsFocusedTaskAttachmentEmpty => FocusedTask?.AttachmentCount == 0;

    public bool IsFocusedNoteAttachmentEmpty => FocusedNote?.AttachmentCount == 0;

    public object? SpotlightDetailDataContext
    {
        get => _spotlightDetailDataContext;
        private set => SetProperty(ref _spotlightDetailDataContext, value);
    }

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

    public string WorkHourOptionsPath => AppPaths.WorkHourOptionsPath;

    public string AttachmentDirectory => AppPaths.AttachmentRoot;

    public string BackupDirectory => AppPaths.BackupRoot;

    public string AppVersion => AppAssembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion
        ?? AppAssembly.GetName().Version?.ToString()
        ?? string.Empty;

    public string CopyrightText => AppAssembly
        .GetCustomAttribute<AssemblyCopyrightAttribute>()?
        .Copyright
        ?? string.Empty;

    public IReadOnlyList<ReleaseNoteEntry> ReleaseNotes => ReleaseNotesService.FromAssembly(AppAssembly);

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

    public TaskItem? FocusedTask
    {
        get => _focusedTask;
        private set
        {
            if (ReferenceEquals(_focusedTask, value))
            {
                return;
            }

            if (_focusedTask is not null)
            {
                _focusedTask.IsExpanded = false;
            }

            _focusedTask = value;

            if (_focusedTask is not null)
            {
                _focusedTask.IsExpanded = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsTaskSpotlightOpen));
            OnPropertyChanged(nameof(IsSpotlightOpen));
            OnPropertyChanged(nameof(IsTaskSpotlightPreviewing));
            OnPropertyChanged(nameof(ActiveTask));
            OnPropertyChanged(nameof(HasUnsavedTaskChanges));
            OnPropertyChanged(nameof(TaskArchiveActionText));
            OnPropertyChanged(nameof(IsFocusedTaskAttachmentEmpty));
            RaiseTaskActionCanExecuteChanged();

            if (!IsSpotlightOpen)
            {
                SpotlightDetailDataContext = null;
            }
        }
    }

    public NoteItem? FocusedNote
    {
        get => _focusedNote;
        private set
        {
            if (ReferenceEquals(_focusedNote, value))
            {
                return;
            }

            if (_focusedNote is not null)
            {
                _focusedNote.IsExpanded = false;
            }

            _focusedNote = value;

            if (_focusedNote is not null)
            {
                _focusedNote.IsExpanded = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNoteSpotlightOpen));
            OnPropertyChanged(nameof(IsSpotlightOpen));
            OnPropertyChanged(nameof(IsNoteSpotlightPreviewing));
            OnPropertyChanged(nameof(ActiveNote));
            OnPropertyChanged(nameof(HasUnsavedNoteChanges));
            OnPropertyChanged(nameof(NoteArchiveActionText));
            OnPropertyChanged(nameof(IsFocusedNoteAttachmentEmpty));
            RaiseNoteActionCanExecuteChanged();

            if (!IsSpotlightOpen)
            {
                SpotlightDetailDataContext = null;
            }
        }
    }

    public TaskItem? ActiveTask => FocusedTask ?? SelectedTask;

    public NoteItem? ActiveNote => FocusedNote ?? SelectedNote;

    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        private set
        {
            if (SetProperty(ref _selectedTask, value))
            {
                OnPropertyChanged(nameof(ActiveTask));
                OnPropertyChanged(nameof(TaskArchiveActionText));
                OnPropertyChanged(nameof(HasUnsavedTaskChanges));
                RaiseTaskActionCanExecuteChanged();
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
                OnPropertyChanged(nameof(ActiveNote));
                OnPropertyChanged(nameof(NoteArchiveActionText));
                OnPropertyChanged(nameof(HasUnsavedNoteChanges));
                RaiseNoteActionCanExecuteChanged();
            }
        }
    }

    public bool IsTaskSpotlightOpen => FocusedTask is not null;

    public bool IsNoteSpotlightOpen => FocusedNote is not null;

    public bool IsSpotlightOpen => IsTaskSpotlightOpen || IsNoteSpotlightOpen;

    public bool IsTaskSpotlightEditing
    {
        get => _isTaskSpotlightEditing;
        private set
        {
            if (SetProperty(ref _isTaskSpotlightEditing, value))
            {
                OnPropertyChanged(nameof(IsSpotlightEditing));
                OnPropertyChanged(nameof(IsTaskSpotlightPreviewing));
                EditSpotlightCommand.RaiseCanExecuteChanged();
                CancelSpotlightEditCommand.RaiseCanExecuteChanged();
                PasteClipboardImageCommand.RaiseCanExecuteChanged();
                SaveTaskCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNoteSpotlightEditing
    {
        get => _isNoteSpotlightEditing;
        private set
        {
            if (SetProperty(ref _isNoteSpotlightEditing, value))
            {
                OnPropertyChanged(nameof(IsSpotlightEditing));
                OnPropertyChanged(nameof(IsNoteSpotlightPreviewing));
                EditSpotlightCommand.RaiseCanExecuteChanged();
                CancelSpotlightEditCommand.RaiseCanExecuteChanged();
                PasteClipboardImageCommand.RaiseCanExecuteChanged();
                SaveNoteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsTaskSpotlightPreviewing => IsTaskSpotlightOpen && !IsTaskSpotlightEditing;

    public bool IsNoteSpotlightPreviewing => IsNoteSpotlightOpen && !IsNoteSpotlightEditing;

    public bool IsSpotlightEditing => IsTaskSpotlightEditing || IsNoteSpotlightEditing;

    public double SpotlightSourceLeft
    {
        get => _spotlightSourceLeft;
        private set => SetProperty(ref _spotlightSourceLeft, value);
    }

    public double SpotlightSourceTop
    {
        get => _spotlightSourceTop;
        private set => SetProperty(ref _spotlightSourceTop, value);
    }

    public double SpotlightSourceWidth
    {
        get => _spotlightSourceWidth;
        private set => SetProperty(ref _spotlightSourceWidth, value);
    }

    public double SpotlightSourceHeight
    {
        get => _spotlightSourceHeight;
        private set => SetProperty(ref _spotlightSourceHeight, value);
    }

    public double SpotlightLeft
    {
        get => _spotlightLeft;
        private set => SetProperty(ref _spotlightLeft, value);
    }

    public double SpotlightTop
    {
        get => _spotlightTop;
        private set => SetProperty(ref _spotlightTop, value);
    }

    public double SpotlightWidth
    {
        get => _spotlightWidth;
        private set => SetProperty(ref _spotlightWidth, value);
    }

    public double SpotlightHeight
    {
        get => _spotlightHeight;
        private set => SetProperty(ref _spotlightHeight, value);
    }

    public bool HasUnsavedTaskChanges => ActiveTask is not null
        && (!string.Equals(TaskTitleDraft, ActiveTask.Title, StringComparison.Ordinal)
            || !string.Equals(TaskDescriptionDraft, ActiveTask.Description, StringComparison.Ordinal)
            || !string.Equals(TaskTagsDraft, ActiveTask.TagsDisplay, StringComparison.Ordinal)
            || TaskStatusDraft != ActiveTask.Status
            || TaskPriorityDraft != ActiveTask.Priority
            || TaskStartDateDraft?.Date != ActiveTask.StartDate?.Date
            || TaskEndDateDraft?.Date != ActiveTask.EndDate?.Date);

    public bool HasUnsavedNoteChanges => ActiveNote is not null
        && (!string.Equals(NoteTitleDraft, ActiveNote.Title, StringComparison.Ordinal)
            || !string.Equals(NoteContentDraft, ActiveNote.Content, StringComparison.Ordinal)
            || !string.Equals(NoteTagsDraft, ActiveNote.TagsDisplay, StringComparison.Ordinal));

    public bool IsBoardViewVisible => _currentFilter is not "Calendar" and not "WorkHourSummary" and not "Backup" and not "Settings";

    public bool IsCalendarViewVisible => _currentFilter == "Calendar";

    public bool IsWorkHourSummaryViewVisible => _currentFilter == "WorkHourSummary";

    public bool IsBackupViewVisible => _currentFilter == "Backup";

    public bool IsSettingsViewVisible => _currentFilter == "Settings";

    public DateTime CalendarMonth
    {
        get => _calendarMonth;
        private set
        {
            var normalized = new DateTime(value.Year, value.Month, 1);

            if (SetProperty(ref _calendarMonth, normalized))
            {
                if (SelectedCalendarDate.Year != normalized.Year || SelectedCalendarDate.Month != normalized.Month)
                {
                    _selectedCalendarDate = normalized;
                    OnPropertyChanged(nameof(SelectedCalendarDate));
                    OnPropertyChanged(nameof(SelectedCalendarDateDisplay));
                }

                OnPropertyChanged(nameof(CalendarMonthTitle));
                RefreshBoard();
                _ = LoadSelectedWorkHoursAsync();
            }
        }
    }

    public string CalendarMonthTitle => CalendarMonth.ToString("yyyy年 M月");

    public DateTime SelectedCalendarDate
    {
        get => _selectedCalendarDate;
        private set
        {
            if (SetProperty(ref _selectedCalendarDate, value.Date))
            {
                OnPropertyChanged(nameof(SelectedCalendarDateDisplay));
                RefreshCalendarSelection();
                _ = LoadSelectedWorkHoursAsync();
            }
        }
    }

    public string SelectedCalendarDateDisplay => SelectedCalendarDate.ToString("yyyy年 M月 d日");

    public string CalendarViewMode
    {
        get => _calendarViewMode;
        private set
        {
            if (SetProperty(ref _calendarViewMode, value))
            {
                OnPropertyChanged(nameof(IsCalendarMonthMode));
                OnPropertyChanged(nameof(IsCalendarListMode));
            }
        }
    }

    public bool IsCalendarMonthMode => CalendarViewMode == "Month";

    public bool IsCalendarListMode => CalendarViewMode == "List";

    public int CalendarTaskCount => CalendarTasks.Count;

    public bool HasCalendarTasks => CalendarTaskCount > 0;

    public int SelectedCalendarTaskCount => SelectedCalendarTasks.Count;

    public bool HasSelectedCalendarTasks => SelectedCalendarTaskCount > 0;

    public int SelectedCalendarWorkHourCount => SelectedCalendarWorkHours.Count;

    public bool HasSelectedCalendarWorkHours => SelectedCalendarWorkHourCount > 0;

    public int SelectedCalendarWorkHourUnits => SelectedCalendarWorkHours.Sum(entry => entry.HourUnits);

    public string SelectedCalendarWorkHourTotalDisplay => $"{WorkHourValueConverter.FormatHours(SelectedCalendarWorkHourUnits)}h";

    public int VisibleTaskCount => TodoTasks.Count + DoingTasks.Count + BlockedTasks.Count + DoneTasks.Count;

    public int VisibleNoteCount => Notes.Count;

    public async Task InitializeAsync()
    {
        try
        {
            _isLoading = true;
            _isInitialized = false;
            await _databaseService.InitializeAsync();
            WorkHourSummary.Invalidate();
            await LoadWorkHourOptionsAsync();
            ClearWorkingSet();
            await LoadArchiveSectionsAsync();
            SelectedArchiveSection = null;
            _loadedArchiveSectionId = null;
            IsArchiveContentLoaded = false;
            await LoadActiveWorkspaceAsync();
            await LoadSelectedWorkHoursAsync();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            SetNotification($"初始化失败：{ex.Message}");
        }
        finally
        {
            _isLoading = false;
            RefreshBoard();
        }
    }

    private async Task LoadWorkHourOptionsAsync()
    {
        var options = await _workHourOptionsService.LoadAsync();
        Replace(WorkDisciplines, options.Disciplines);
        Replace(WorkActivities, options.WorkActivities);
    }

    private async Task AddWorkDisciplineAsync(object? _)
    {
        var value = NewWorkDiscipline.Trim();
        if (value.Length == 0)
        {
            return;
        }

        var options = await _workHourOptionsService.AddDisciplineAsync(value);
        Replace(WorkDisciplines, options.Disciplines);
        NewWorkDiscipline = string.Empty;
        SetNotification($"已添加专业：{value}");
    }

    private async Task AddWorkActivityAsync(object? _)
    {
        var value = NewWorkActivity.Trim();
        if (value.Length == 0)
        {
            return;
        }

        var options = await _workHourOptionsService.AddWorkActivityAsync(value);
        Replace(WorkActivities, options.WorkActivities);
        NewWorkActivity = string.Empty;
        SetNotification($"已添加工作内容：{value}");
    }

    private async Task RemoveWorkDisciplineAsync(object? parameter)
    {
        if (parameter is not string value ||
            !ConfirmDialog.Show(GetDialogOwner(), "移除专业选项", $"从下拉选项中移除“{value}”吗？历史人工时不会受到影响。", "移除"))
        {
            return;
        }

        var options = await _workHourOptionsService.RemoveDisciplineAsync(value);
        Replace(WorkDisciplines, options.Disciplines);
        SetNotification($"已移除专业选项：{value}");
    }

    private async Task RemoveWorkActivityAsync(object? parameter)
    {
        if (parameter is not string value ||
            !ConfirmDialog.Show(GetDialogOwner(), "移除工作内容", $"从下拉选项中移除“{value}”吗？历史人工时不会受到影响。", "移除"))
        {
            return;
        }

        var options = await _workHourOptionsService.RemoveWorkActivityAsync(value);
        Replace(WorkActivities, options.WorkActivities);
        SetNotification($"已移除工作内容选项：{value}");
    }

    private async Task LoadArchiveSectionsAsync()
    {
        var previousSelectedId = SelectedArchiveSection?.Id;
        var sections = await _archiveSectionRepository.GetAllAsync();
        Replace(ArchiveSections, sections);
        OnPropertyChanged(nameof(ArchiveSectionSelectorHint));

        var selected = previousSelectedId.HasValue
            ? ArchiveSections.FirstOrDefault(section => section.Id == previousSelectedId.Value)
            : null;
        SelectedArchiveSection = selected;
        OnPropertyChanged(nameof(ArchiveSectionSelectorText));
    }

    private async Task LoadActiveWorkspaceAsync()
    {
        ClearWorkingSet();
        var tasks = await _taskRepository.GetActiveAsync();
        var notes = await _noteRepository.GetActiveAsync();
        await AttachLoadedAttachmentsAsync(tasks, notes);

        foreach (var task in tasks)
        {
            AddTaskToMemory(task);
        }

        foreach (var note in notes)
        {
            AddNoteToMemory(note);
        }

        _loadedArchiveSectionId = null;
        SelectedArchiveSection = null;
        IsArchiveContentLoaded = false;
    }

    private async Task LoadArchiveSectionAsync(ArchiveSection section)
    {
        try
        {
            _isLoading = true;
            ClearWorkingSet();
            var tasks = await _taskRepository.GetArchivedBySectionAsync(section.Id);
            var notes = await _noteRepository.GetArchivedBySectionAsync(section.Id);
            await AttachLoadedAttachmentsAsync(tasks, notes);

            foreach (var task in tasks)
            {
                AddTaskToMemory(task);
            }

            foreach (var note in notes)
            {
                AddNoteToMemory(note);
            }

            SelectedArchiveSection = ArchiveSections.FirstOrDefault(item => item.Id == section.Id) ?? section;
            _loadedArchiveSectionId = section.Id;
            IsArchiveContentLoaded = true;
            SetNotification($"已打开归档分区：{section.Name}");
        }
        catch (Exception ex)
        {
            ClearWorkingSet();
            _loadedArchiveSectionId = null;
            SelectedArchiveSection = null;
            IsArchiveContentLoaded = false;
            SetNotification($"加载归档失败：{ex.Message}");
        }
        finally
        {
            _isLoading = false;
            RefreshBoard();
        }
    }

    private async Task AttachLoadedAttachmentsAsync(
        IReadOnlyList<TaskItem> tasks,
        IReadOnlyList<NoteItem> notes)
    {
        var taskAttachments = await _attachmentRepository.GetByOwnerIdsAsync(
            AttachmentOwnerType.Task,
            tasks.Select(task => task.Id));
        var noteAttachments = await _attachmentRepository.GetByOwnerIdsAsync(
            AttachmentOwnerType.Note,
            notes.Select(note => note.Id));

        AttachItems(tasks, taskAttachments);
        AttachItems(notes, noteAttachments);
    }

    private static void AttachItems(IEnumerable<TaskItem> tasks, IEnumerable<AttachmentItem> attachments)
    {
        var groupedAttachments = attachments
            .GroupBy(attachment => attachment.OwnerId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.SortOrder).ToArray());

        foreach (var task in tasks)
        {
            if (!groupedAttachments.TryGetValue(task.Id, out var items))
            {
                continue;
            }

            foreach (var attachment in items)
            {
                task.Attachments.Add(attachment);
            }
        }
    }

    private static void AttachItems(IEnumerable<NoteItem> notes, IEnumerable<AttachmentItem> attachments)
    {
        var groupedAttachments = attachments
            .GroupBy(attachment => attachment.OwnerId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.SortOrder).ToArray());

        foreach (var note in notes)
        {
            if (!groupedAttachments.TryGetValue(note.Id, out var items))
            {
                continue;
            }

            foreach (var attachment in items)
            {
                note.Attachments.Add(attachment);
            }
        }
    }

    private void ClearWorkingSet()
    {
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
    }

    private void OpenTaskFromParameter(object? parameter)
    {
        if (parameter is CardOpenPayload { Item: TaskItem task } payload)
        {
            OpenTask(task, payload);
            return;
        }

        OpenTask(parameter as TaskItem, null);
    }

    private void OpenNoteFromParameter(object? parameter)
    {
        if (parameter is CardOpenPayload { Item: NoteItem note } payload)
        {
            OpenNote(note, payload);
            return;
        }

        OpenNote(parameter as NoteItem, null);
    }

    private async Task CreateTaskAsync(object? parameter)
    {
        if (!ConfirmDiscardSpotlightChanges())
        {
            return;
        }

        var createPayload = parameter as CardCreatePayload;
        var columnParameter = createPayload?.ColumnKind ?? parameter;
        var status = columnParameter switch
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
            Description = "在放大的卡片中补充任务说明。",
            Status = status,
            Priority = TaskPriority.Medium,
            SortOrder = NextTaskSortOrder(status)
        };

        if (!await SaveTaskSafelyAsync(task, "已创建任务"))
        {
            return;
        }

        AddTaskToMemory(task);
        RefreshBoard();
        OpenTask(task, CreateOpenPayload(task, createPayload), true);
    }

    private async Task CreateTaskForCalendarDateAsync(object? parameter)
    {
        if (!ConfirmDiscardSpotlightChanges())
        {
            return;
        }

        var date = CalendarDateFromParameter(parameter) ?? SelectedCalendarDate;
        var task = new TaskItem
        {
            Title = "新日程任务",
            Description = $"安排在 {date:yyyy/M/d} 的任务。",
            Status = TaskStatus.Todo,
            Priority = TaskPriority.Medium,
            StartDate = date.Date,
            EndDate = date.Date,
            SortOrder = NextTaskSortOrder(TaskStatus.Todo)
        };

        if (!await SaveTaskSafelyAsync(task, "已创建日程任务"))
        {
            return;
        }

        AddTaskToMemory(task);
        SelectCalendarDate(date);
        RefreshBoard();
        OpenTask(task);
    }

    private async Task CreateNoteAsync(object? parameter)
    {
        if (!ConfirmDiscardSpotlightChanges())
        {
            return;
        }

        var note = new NoteItem
        {
            Title = "新备忘",
            Content = "记录一个想法、提醒或临时片段。",
            SortOrder = NextNoteSortOrder()
        };

        if (!await SaveNoteSafelyAsync(note, "已创建备忘"))
        {
            return;
        }

        AddNoteToMemory(note);
        RefreshBoard();
        OpenNote(note, CreateOpenPayload(note, parameter as CardCreatePayload), true);
    }

    private void OpenTask(TaskItem? task, CardOpenPayload? payload = null, bool edit = false)
    {
        if (task is null)
        {
            return;
        }

        if (!ReferenceEquals(ActiveTask, task) && !ConfirmDiscardSpotlightChanges())
        {
            return;
        }

        SpotlightDetailDataContext = null;
        SelectedNote = null;
        FocusedNote = null;
        SetSpotlightLayout(payload);
        ClearNoteDraft();
        SelectedTask = task;
        IsNoteSpotlightEditing = false;
        IsTaskSpotlightEditing = edit;
        LoadTaskDraft(task);
        FocusedTask = task;
    }

    private void OpenNote(NoteItem? note, CardOpenPayload? payload = null, bool edit = false)
    {
        if (note is null)
        {
            return;
        }

        if (!ReferenceEquals(ActiveNote, note) && !ConfirmDiscardSpotlightChanges())
        {
            return;
        }

        SpotlightDetailDataContext = null;
        SelectedTask = null;
        FocusedTask = null;
        SetSpotlightLayout(payload);
        ClearTaskDraft();
        SelectedNote = note;
        IsTaskSpotlightEditing = false;
        IsNoteSpotlightEditing = edit;
        LoadNoteDraft(note);
        FocusedNote = note;
    }

    private static CardOpenPayload? CreateOpenPayload(object item, CardCreatePayload? payload)
    {
        return payload is null
            ? null
            : new CardOpenPayload(item, payload.AnchorBounds, payload.HostSize);
    }

    private void SetSpotlightLayout(CardOpenPayload? payload)
    {
        const double fallbackWidth = 240;
        const double fallbackHeight = 140;

        var payloadHostSize = payload?.HostSize ?? Size.Empty;
        if (payloadHostSize.Width > 0 && payloadHostSize.Height > 0)
        {
            _spotlightHostSize = payloadHostSize;
        }

        var hostWidth = _spotlightHostSize.Width;
        var hostHeight = _spotlightHostSize.Height;
        var fallbackLeft = Math.Max(0, (hostWidth - fallbackWidth) / 2);
        var fallbackTop = Math.Max(0, (hostHeight - fallbackHeight) / 2);
        var anchor = payload?.AnchorBounds ?? new Rect(fallbackLeft, fallbackTop, fallbackWidth, fallbackHeight);

        if (anchor.Width <= 0 || anchor.Height <= 0)
        {
            anchor = new Rect(fallbackLeft, fallbackTop, fallbackWidth, fallbackHeight);
        }

        SpotlightSourceLeft = anchor.Left;
        SpotlightSourceTop = anchor.Top;
        SpotlightSourceWidth = Math.Max(80, anchor.Width);
        SpotlightSourceHeight = Math.Max(72, anchor.Height);
        ApplySpotlightTargetLayout(new Size(hostWidth, hostHeight));
    }

    public void UpdateSpotlightLayout(Size hostSize)
    {
        if (hostSize.Width <= 0 || hostSize.Height <= 0)
        {
            return;
        }

        _spotlightHostSize = hostSize;

        if (!IsSpotlightOpen)
        {
            return;
        }

        ApplySpotlightTargetLayout(hostSize);
    }

    private void ApplySpotlightTargetLayout(Size hostSize)
    {
        const double margin = 26;
        const double preferredWidth = 640;
        const double minWidth = 420;

        var availableWidth = Math.Max(minWidth, hostSize.Width - margin * 2);
        var width = Math.Min(preferredWidth, availableWidth);
        var height = hostSize.Height * 0.85;

        SpotlightLeft = Math.Max(0, (hostSize.Width - width) / 2);
        SpotlightTop = Math.Max(0, (hostSize.Height - height) / 2);
        SpotlightWidth = width;
        SpotlightHeight = height;
    }

    private void CloseSpotlight()
    {
        if (!ConfirmSpotlightClose())
        {
            return;
        }

        CompleteSpotlightClose();
    }

    public bool ConfirmSpotlightClose()
    {
        return ConfirmDiscardSpotlightChanges();
    }

    public void CompleteSpotlightOpen()
    {
        if (IsSpotlightOpen)
        {
            SpotlightDetailDataContext = this;
        }
    }

    public void CompleteSpotlightClose()
    {
        SpotlightDetailDataContext = null;
        FocusedTask = null;
        FocusedNote = null;
        SelectedTask = null;
        SelectedNote = null;
        IsTaskSpotlightEditing = false;
        IsNoteSpotlightEditing = false;
        ClearTaskDraft();
        ClearNoteDraft();
    }

    private void EnterSpotlightEditMode()
    {
        if (FocusedTask is not null)
        {
            LoadTaskDraft(FocusedTask);
            IsTaskSpotlightEditing = true;
            IsNoteSpotlightEditing = false;
            return;
        }

        if (FocusedNote is null)
        {
            return;
        }

        LoadNoteDraft(FocusedNote);
        IsNoteSpotlightEditing = true;
        IsTaskSpotlightEditing = false;
    }

    private void CancelSpotlightEdit()
    {
        if (FocusedTask is not null)
        {
            LoadTaskDraft(FocusedTask);
            IsTaskSpotlightEditing = false;
        }

        if (FocusedNote is not null)
        {
            LoadNoteDraft(FocusedNote);
            IsNoteSpotlightEditing = false;
        }
    }

    private static Window? GetDialogOwner()
    {
        if (Application.Current is null)
        {
            return null;
        }

        foreach (Window window in Application.Current.Windows)
        {
            if (window.IsActive)
            {
                return window;
            }
        }

        return Application.Current.MainWindow;
    }

    private static bool ShowConfirmationDialog(string title, string message, string confirmText)
    {
        return ConfirmDialog.Show(GetDialogOwner(), title, message, confirmText, "取消");
    }

    private async Task<ArchiveSection?> ChooseArchiveTargetSectionAsync()
    {
        await LoadArchiveSectionsAsync();
        var defaultSection = ArchiveSections.FirstOrDefault(section => section.IsDefault)
            ?? ArchiveSections.FirstOrDefault(section => section.Id == ArchiveSection.DefaultId)
            ?? await _archiveSectionRepository.GetDefaultAsync();
        var selection = ArchiveSectionDialog.Show(GetDialogOwner(), ArchiveSections, defaultSection);

        if (selection is null)
        {
            return null;
        }

        ArchiveSection section;

        if (!string.IsNullOrWhiteSpace(selection.NewSectionName))
        {
            section = await _archiveSectionRepository.GetOrCreateAsync(selection.NewSectionName);
        }
        else
        {
            var sectionId = selection.SectionId ?? defaultSection.Id;
            section = ArchiveSections.FirstOrDefault(item => item.Id == sectionId) ?? defaultSection;
        }

        await LoadArchiveSectionsAsync();
        return ArchiveSections.FirstOrDefault(item => item.Id == section.Id) ?? section;
    }

    private async Task DeleteSelectedTaskAsync()
    {
        if (ActiveTask is null)
        {
            return;
        }

        var task = ActiveTask;

        if (!ShowConfirmationDialog(
                "删除任务",
                "删除后会同时删除这张任务卡片的本地附件，是否继续？",
                "删除"))
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
            FocusedTask = null;
            SelectedTask = null;
            IsTaskSpotlightEditing = false;
            ClearTaskDraft();
            await LoadArchiveSectionsAsync();
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
        if (ActiveNote is null)
        {
            return;
        }

        var note = ActiveNote;

        if (!ShowConfirmationDialog(
                "删除备忘",
                "删除后会同时删除这张备忘的本地附件，是否继续？",
                "删除"))
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
            FocusedNote = null;
            SelectedNote = null;
            IsNoteSpotlightEditing = false;
            ClearNoteDraft();
            await LoadArchiveSectionsAsync();
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

    private async Task ArchiveTaskAsync()
    {
        if (ActiveTask is null)
        {
            return;
        }

        var task = ActiveTask;
        var shouldArchive = !task.IsArchived;
        var oldArchived = task.IsArchived;
        var oldUpdatedAt = task.UpdatedAt;
        var oldArchiveSectionId = task.ArchiveSectionId;
        var oldArchivedAt = task.ArchivedAt;
        ArchiveSection? archiveSection = null;

        if (shouldArchive)
        {
            archiveSection = await ChooseArchiveTargetSectionAsync();

            if (archiveSection is null)
            {
                return;
            }
        }

        try
        {
            _isLoading = true;
            task.IsArchived = shouldArchive;
            task.ArchiveSectionId = shouldArchive ? archiveSection!.Id : null;
            task.ArchivedAt = shouldArchive ? DateTime.Now : null;
            await _taskRepository.UpsertAsync(task);
            _isLoading = false;

            FocusedTask = null;
            SelectedTask = null;
            IsTaskSpotlightEditing = false;
            ClearTaskDraft();
            if (shouldArchive || IsArchiveFilter)
            {
                UntrackTask(task);
                _allTasks.Remove(task);
            }

            await LoadArchiveSectionsAsync();
            RefreshBoard();
            SetNotification(shouldArchive ? "已归档任务" : "已恢复任务");
        }
        catch (Exception ex)
        {
            task.IsArchived = oldArchived;
            task.ArchiveSectionId = oldArchiveSectionId;
            task.ArchivedAt = oldArchivedAt;
            task.UpdatedAt = oldUpdatedAt;
            _isLoading = false;
            RefreshBoard();
            SetNotification($"{(shouldArchive ? "归档" : "恢复")}任务失败：{ex.Message}");
        }
    }

    private async Task ArchiveNoteAsync()
    {
        if (ActiveNote is null)
        {
            return;
        }

        var note = ActiveNote;
        var shouldArchive = !note.IsArchived;
        var oldArchived = note.IsArchived;
        var oldUpdatedAt = note.UpdatedAt;
        var oldArchiveSectionId = note.ArchiveSectionId;
        var oldArchivedAt = note.ArchivedAt;
        ArchiveSection? archiveSection = null;

        if (shouldArchive)
        {
            archiveSection = await ChooseArchiveTargetSectionAsync();

            if (archiveSection is null)
            {
                return;
            }
        }

        try
        {
            _isLoading = true;
            note.IsArchived = shouldArchive;
            note.ArchiveSectionId = shouldArchive ? archiveSection!.Id : null;
            note.ArchivedAt = shouldArchive ? DateTime.Now : null;
            await _noteRepository.UpsertAsync(note);
            _isLoading = false;

            FocusedNote = null;
            SelectedNote = null;
            IsNoteSpotlightEditing = false;
            ClearNoteDraft();
            if (shouldArchive || IsArchiveFilter)
            {
                UntrackNote(note);
                _allNotes.Remove(note);
            }

            await LoadArchiveSectionsAsync();
            RefreshBoard(refreshCalendar: false);
            SetNotification(shouldArchive ? "已归档备忘" : "已恢复备忘");
        }
        catch (Exception ex)
        {
            note.IsArchived = oldArchived;
            note.ArchiveSectionId = oldArchiveSectionId;
            note.ArchivedAt = oldArchivedAt;
            note.UpdatedAt = oldUpdatedAt;
            _isLoading = false;
            RefreshBoard();
            SetNotification($"{(shouldArchive ? "归档" : "恢复")}备忘失败：{ex.Message}");
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
            if (task.IsArchived)
            {
                SetNotification("归档任务不能拖拽排序，先恢复后再移动");
                return;
            }

            await MoveTaskToColumnAsync(task, payload.TargetColumn, payload.TargetIndex);
            return;
        }

        if (payload.Item is NoteItem note && payload.TargetColumn == KanbanColumnKind.Notes)
        {
            if (note.IsArchived)
            {
                SetNotification("归档备忘不能拖拽排序，先恢复后再移动");
                return;
            }

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
        var oldTaskState = TaskPersistenceState.From(task);

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
        var affectedItems = currentList.Concat(sourceList).DistinctBy(item => item.Id).ToArray();
        var oldSortOrders = affectedItems.ToDictionary(item => item.Id, item => item.SortOrder);

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

        try
        {
            await _taskRepository.UpsertRangeAsync(affectedItems);
            SetNotification("任务顺序已更新");
        }
        catch (Exception ex)
        {
            _isLoading = true;
            oldTaskState.Restore(task);

            foreach (var item in affectedItems)
            {
                if (oldSortOrders.TryGetValue(item.Id, out var sortOrder))
                {
                    item.SortOrder = sortOrder;
                }
            }

            _isLoading = false;
            RefreshBoard();
            SetNotification($"更新任务顺序失败：{ex.Message}");
        }
    }

    private async Task MoveNoteAsync(NoteItem note, int targetIndex)
    {
        var oldNoteState = NotePersistenceState.From(note);
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
        var affectedItems = currentList.Append(note).DistinctBy(item => item.Id).ToArray();
        var oldSortOrders = affectedItems.ToDictionary(item => item.Id, item => item.SortOrder);

        _isLoading = true;
        currentList.Insert(insertIndex, note);

        for (var index = 0; index < currentList.Count; index++)
        {
            currentList[index].SortOrder = index;
        }

        _isLoading = false;
        RefreshBoard();

        try
        {
            await _noteRepository.UpsertRangeAsync(currentList);
            SetNotification("备忘顺序已更新");
        }
        catch (Exception ex)
        {
            _isLoading = true;
            oldNoteState.Restore(note);

            foreach (var item in affectedItems)
            {
                if (oldSortOrders.TryGetValue(item.Id, out var sortOrder))
                {
                    item.SortOrder = sortOrder;
                }
            }

            _isLoading = false;
            RefreshBoard();
            SetNotification($"更新备忘顺序失败：{ex.Message}");
        }
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

    private async Task PasteClipboardImageAsync(object? _)
    {
        if (!IsSpotlightEditing)
        {
            return;
        }

        object? owner = IsTaskSpotlightEditing
            ? FocusedTask
            : IsNoteSpotlightEditing
                ? FocusedNote
                : null;

        if (owner is null)
        {
            return;
        }

        BitmapSource? image;

        try
        {
            if (!Clipboard.ContainsImage())
            {
                return;
            }

            image = Clipboard.GetImage();
        }
        catch (Exception ex)
        {
            SetNotification($"读取剪贴板失败：{ex.Message}");
            return;
        }

        if (image is null)
        {
            return;
        }

        AttachmentItem? attachment = null;

        try
        {
            using var stream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
            stream.Position = 0;

            var fileName = $"clipboard-image-{DateTime.Now:yyyyMMdd-HHmmss}.png";

            if (owner is TaskItem task)
            {
                attachment = await _attachmentStorage.SaveStreamAsync(AttachmentOwnerType.Task, task.Id, fileName, stream);
                attachment.SortOrder = task.Attachments.Count;
                await _attachmentRepository.AddRangeAsync([attachment]);
                task.Attachments.Add(attachment);
                RefreshBoard();
                SetNotification("已保存剪贴板图片为附件");
                return;
            }

            if (owner is NoteItem note)
            {
                attachment = await _attachmentStorage.SaveStreamAsync(AttachmentOwnerType.Note, note.Id, fileName, stream);
                attachment.SortOrder = note.Attachments.Count;
                await _attachmentRepository.AddRangeAsync([attachment]);
                note.Attachments.Add(attachment);
                RefreshBoard();
                SetNotification("已保存剪贴板图片为附件");
            }
        }
        catch (Exception ex)
        {
            if (attachment is not null)
            {
                try
                {
                    await _attachmentStorage.DeleteAttachmentFileAsync(attachment);
                }
                catch
                {
                    // Best-effort cleanup after a failed clipboard image attachment.
                }
            }

            SetNotification($"保存剪贴板图片失败：{ex.Message}");
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

    private void OnDateFilterChanged()
    {
        OnPropertyChanged(nameof(HasDateFilter));
        ClearDateFilterCommand.RaiseCanExecuteChanged();
        RefreshBoard();
    }

    private void ClearDateFilter()
    {
        if (!HasDateFilter)
        {
            return;
        }

        _dateFilterStart = null;
        _dateFilterEnd = null;
        OnPropertyChanged(nameof(DateFilterStart));
        OnPropertyChanged(nameof(DateFilterEnd));
        OnDateFilterChanged();
    }

    private Task AddWorkHourAsync(object? _)
    {
        return ShowWorkHourDialogAsync(null);
    }

    private Task OpenWorkHourAsync(object? parameter)
    {
        return parameter is WorkHourEntry entry
            ? ShowWorkHourDialogAsync(entry)
            : Task.CompletedTask;
    }

    private async Task ShowWorkHourDialogAsync(WorkHourEntry? entry)
    {
        var result = WorkHourEntryDialog.Show(
            GetDialogOwner(),
            entry,
            SelectedCalendarDate,
            WorkDisciplines,
            WorkActivities);

        if (result is null || result.Action == WorkHourDialogAction.None)
        {
            return;
        }

        if (result.Action == WorkHourDialogAction.Delete)
        {
            await _workHourRepository.DeleteAsync(result.Id);
            WorkHourSummary.Invalidate();
            await LoadSelectedWorkHoursAsync();
            SetNotification("已删除人工时记录");
            return;
        }

        var otherUnits = await _workHourRepository.GetTotalUnitsByDateAsync(
            result.WorkDate,
            entry?.Id);
        if (otherUnits + result.HourUnits > WorkHourValueConverter.MaximumEntryUnits &&
            !ConfirmDialog.Show(
                GetDialogOwner(),
                "当天工时超过 24 小时",
                $"保存后 {result.WorkDate:yyyy/M/d} 的总工时将超过 24 小时，是否继续？",
                "继续保存"))
        {
            SetNotification("已取消保存人工时");
            return;
        }

        var now = DateTime.Now;
        var savedEntry = new WorkHourEntry
        {
            Id = result.Id,
            WorkDate = result.WorkDate,
            ProjectNumber = WorkHourValueConverter.NormalizeProjectNumber(result.ProjectNumber),
            Discipline = result.Discipline.Trim(),
            WorkActivity = result.WorkActivity.Trim(),
            HourUnits = result.HourUnits,
            Remark = result.Remark.Trim(),
            CreatedAt = result.CreatedAt,
            UpdatedAt = now
        };

        var options = await _workHourOptionsService.AddDisciplineAsync(savedEntry.Discipline);
        options = await _workHourOptionsService.AddWorkActivityAsync(savedEntry.WorkActivity);
        await _workHourRepository.UpsertAsync(savedEntry);
        WorkHourSummary.Invalidate();
        Replace(WorkDisciplines, options.Disciplines);
        Replace(WorkActivities, options.WorkActivities);

        if (savedEntry.WorkDate != SelectedCalendarDate)
        {
            SelectCalendarDate(savedEntry.WorkDate);
        }
        else
        {
            await LoadSelectedWorkHoursAsync();
        }

        SetNotification(entry is null ? "已添加人工时" : "已保存人工时");
    }

    private async Task LoadSelectedWorkHoursAsync()
    {
        var version = ++_workHourLoadVersion;
        var selectedDate = SelectedCalendarDate;
        try
        {
            var entries = await _workHourRepository.GetByDateAsync(selectedDate);
            if (version != _workHourLoadVersion || selectedDate != SelectedCalendarDate)
            {
                return;
            }

            Replace(SelectedCalendarWorkHours, entries);
            NotifySelectedWorkHourSummaryChanged();
        }
        catch (Exception ex)
        {
            if (version == _workHourLoadVersion)
            {
                SetNotification($"加载人工时失败：{ex.Message}");
            }
        }
    }

    private void NotifySelectedWorkHourSummaryChanged()
    {
        OnPropertyChanged(nameof(SelectedCalendarWorkHourCount));
        OnPropertyChanged(nameof(HasSelectedCalendarWorkHours));
        OnPropertyChanged(nameof(SelectedCalendarWorkHourUnits));
        OnPropertyChanged(nameof(SelectedCalendarWorkHourTotalDisplay));
    }

    private void GoToToday()
    {
        var today = DateTime.Today;
        _calendarMonth = new DateTime(today.Year, today.Month, 1);
        _selectedCalendarDate = today;
        OnPropertyChanged(nameof(CalendarMonth));
        OnPropertyChanged(nameof(CalendarMonthTitle));
        OnPropertyChanged(nameof(SelectedCalendarDate));
        OnPropertyChanged(nameof(SelectedCalendarDateDisplay));
        RefreshBoard();
        _ = LoadSelectedWorkHoursAsync();
    }

    private void SelectCalendarDate(object? parameter)
    {
        var date = CalendarDateFromParameter(parameter);

        if (date is null)
        {
            return;
        }

        SelectCalendarDate(date.Value);
    }

    private void SelectCalendarDate(DateTime date)
    {
        var targetDate = date.Date;
        var targetMonth = new DateTime(targetDate.Year, targetDate.Month, 1);
        var monthChanged = targetMonth != CalendarMonth;

        _selectedCalendarDate = targetDate;
        OnPropertyChanged(nameof(SelectedCalendarDate));
        OnPropertyChanged(nameof(SelectedCalendarDateDisplay));

        if (monthChanged)
        {
            _calendarMonth = targetMonth;
            OnPropertyChanged(nameof(CalendarMonth));
            OnPropertyChanged(nameof(CalendarMonthTitle));
            RefreshBoard();
            _ = LoadSelectedWorkHoursAsync();
            return;
        }

        RefreshCalendarSelection();
        _ = LoadSelectedWorkHoursAsync();
    }

    private void SetCalendarViewMode(object? parameter)
    {
        if (parameter is not string mode || mode is not ("Month" or "List"))
        {
            return;
        }

        CalendarViewMode = mode;
    }

    private void OnCommandUnhandledException(Exception exception)
    {
        SetNotification($"操作失败：{exception.Message}");
    }

    private void ScheduleSearchRefresh()
    {
        _searchRefreshCancellation?.Cancel();
        _searchRefreshCancellation?.Dispose();

        var cancellation = new CancellationTokenSource();
        _searchRefreshCancellation = cancellation;
        _ = RefreshSearchAfterDelayAsync(cancellation);
    }

    private async Task RefreshSearchAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(180, cancellation.Token);

            if (!cancellation.IsCancellationRequested)
            {
                RefreshBoard();
            }
        }
        catch (OperationCanceledException)
        {
            // A newer search input superseded this refresh.
        }
        finally
        {
            if (ReferenceEquals(_searchRefreshCancellation, cancellation))
            {
                _searchRefreshCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private async Task MoveTaskToCalendarDateAsync(object? parameter)
    {
        if (parameter is not CalendarTaskDateDropPayload payload)
        {
            return;
        }

        await MoveTaskToCalendarDateAsync(payload.Task, payload.Date);
    }

    private async Task MoveTaskToCalendarDateAsync(TaskItem task, DateTime date)
    {
        var oldStartDate = task.StartDate;
        var oldEndDate = task.EndDate;
        var oldUpdatedAt = task.UpdatedAt;

        try
        {
            _isLoading = true;
            var targetDate = date.Date;
            var (newStartDate, newEndDate) = ShiftTaskDates(task, targetDate);
            task.StartDate = newStartDate;
            task.EndDate = newEndDate;
            await _taskRepository.UpsertAsync(task);
            _isLoading = false;

            if (ReferenceEquals(SelectedTask, task))
            {
                LoadTaskDraft(task);
            }

            SelectCalendarDate(targetDate);
            RefreshBoard();
            SetNotification($"已调整任务日期：{task.Title}");
        }
        catch (Exception ex)
        {
            task.StartDate = oldStartDate;
            task.EndDate = oldEndDate;
            task.UpdatedAt = oldUpdatedAt;
            _isLoading = false;
            RefreshBoard();
            SetNotification($"调整任务日期失败：{ex.Message}");
        }
    }

    private static (DateTime? StartDate, DateTime? EndDate) ShiftTaskDates(TaskItem task, DateTime targetDate)
    {
        if (task.StartDate.HasValue && task.EndDate.HasValue)
        {
            var range = TaskDateRange(task);
            var duration = (range.End - range.Start).Days;
            return (targetDate, targetDate.AddDays(duration));
        }

        if (task.StartDate.HasValue)
        {
            return (targetDate, null);
        }

        if (task.EndDate.HasValue)
        {
            return (null, targetDate);
        }

        return (targetDate, targetDate);
    }

    private static DateTime? CalendarDateFromParameter(object? parameter)
    {
        return parameter switch
        {
            DateTime date => date.Date,
            CalendarDayItem day => day.Date,
            _ => null
        };
    }

    private async Task ChangeFilterAsync(object? parameter)
    {
        var filter = parameter as string ?? "Board";
        var previousFilter = _currentFilter;

        if (!ConfirmDiscardSpotlightChanges())
        {
            return;
        }

        _currentFilter = filter;
        OnPropertyChanged(nameof(CurrentFilter));
        OnPropertyChanged(nameof(IsArchiveFilter));
        FocusedTask = null;
        FocusedNote = null;
        SelectedTask = null;
        SelectedNote = null;
        IsTaskSpotlightEditing = false;
        IsNoteSpotlightEditing = false;
        ClearTaskDraft();
        ClearNoteDraft();
        CurrentFilterLabel = filter switch
        {
            "AllTasks" => "全部任务",
            "Today" => "今日任务",
            "Calendar" => "日历",
            "WorkHourSummary" => "人工时汇总",
            "High" => "高优先级",
            "Overdue" => "超期未完成",
            "WithAttachments" => "有附件",
            "Archived" => "归档",
            "Backup" => "数据备份",
            "Settings" => "设置",
            _ => "看板"
        };

        OnPropertyChanged(nameof(IsBoardViewVisible));
        OnPropertyChanged(nameof(IsCalendarViewVisible));
        OnPropertyChanged(nameof(IsWorkHourSummaryViewVisible));
        OnPropertyChanged(nameof(IsBackupViewVisible));
        OnPropertyChanged(nameof(IsSettingsViewVisible));
        OnPropertyChanged(nameof(IsArchiveWaitingForSection));

        try
        {
            _isLoading = true;

            if (filter == "Archived")
            {
                await LoadArchiveSectionsAsync();
                ClearWorkingSet();
                SelectedArchiveSection = null;
                _loadedArchiveSectionId = null;
                IsArchiveContentLoaded = false;
            }
            else if (previousFilter == "Archived")
            {
                await LoadActiveWorkspaceAsync();
            }

            if (filter == "WorkHourSummary")
            {
                await WorkHourSummary.EnsureLoadedAsync();
            }
        }
        catch (Exception ex)
        {
            SetNotification($"切换视图失败：{ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }

        RefreshBoard();
    }

    private async Task SelectArchiveSectionAsync(object? parameter)
    {
        if (parameter is not ArchiveSection section)
        {
            return;
        }

        if (!ConfirmDiscardSpotlightChanges())
        {
            return;
        }

        FocusedTask = null;
        FocusedNote = null;
        SelectedTask = null;
        SelectedNote = null;
        IsTaskSpotlightEditing = false;
        IsNoteSpotlightEditing = false;
        ClearTaskDraft();
        ClearNoteDraft();

        if (_loadedArchiveSectionId == section.Id && IsArchiveContentLoaded)
        {
            SelectedArchiveSection = section;
            RefreshBoard();
            return;
        }

        await LoadArchiveSectionAsync(section);
    }

    private async Task OpenArchiveSectionPickerAsync(object? _)
    {
        if (!IsArchiveFilter)
        {
            return;
        }

        await LoadArchiveSectionsAsync();

        var selection = ArchiveSectionPickerDialog.Show(GetDialogOwner(), ArchiveSections, SelectedArchiveSection);

        if (selection is null)
        {
            return;
        }

        if (selection.DeleteSectionId.HasValue)
        {
            await DeleteArchiveSectionAsync(selection.DeleteSectionId.Value);
            return;
        }

        ArchiveSection section;

        if (!string.IsNullOrWhiteSpace(selection.NewSectionName))
        {
            section = await _archiveSectionRepository.GetOrCreateAsync(selection.NewSectionName);
            NewArchiveSectionName = string.Empty;
            await LoadArchiveSectionsAsync();
            section = ArchiveSections.FirstOrDefault(item => item.Id == section.Id) ?? section;
        }
        else if (selection.SectionId.HasValue)
        {
            section = ArchiveSections.FirstOrDefault(item => item.Id == selection.SectionId.Value)
                ?? await _archiveSectionRepository.GetDefaultAsync();
        }
        else
        {
            return;
        }

        await SelectArchiveSectionAsync(section);
    }

    private async Task DeleteArchiveSectionAsync(Guid sectionId)
    {
        var section = ArchiveSections.FirstOrDefault(item => item.Id == sectionId);

        if (section?.IsDefault == true || sectionId == ArchiveSection.DefaultId)
        {
            SetNotification("默认分区不能删除");
            return;
        }

        try
        {
            var deletingLoadedSection = _loadedArchiveSectionId == sectionId;
            var defaultSectionWasLoaded = _loadedArchiveSectionId == ArchiveSection.DefaultId;
            var deleted = await _archiveSectionRepository.DeleteAndMoveContentsToDefaultAsync(sectionId);

            if (!deleted)
            {
                SetNotification("分区不存在或不能删除");
                await LoadArchiveSectionsAsync();
                return;
            }

            await LoadArchiveSectionsAsync();
            var defaultSection = ArchiveSections.FirstOrDefault(item => item.IsDefault)
                ?? ArchiveSections.FirstOrDefault(item => item.Id == ArchiveSection.DefaultId)
                ?? await _archiveSectionRepository.GetDefaultAsync();

            if (IsArchiveFilter && (deletingLoadedSection || defaultSectionWasLoaded))
            {
                await LoadArchiveSectionAsync(defaultSection);
            }
            else
            {
                RefreshBoard();
            }

            SetNotification($"已删除归档分区，卡片已移动到默认分区");
        }
        catch (Exception ex)
        {
            SetNotification($"删除归档分区失败：{ex.Message}");
        }
    }

    private async Task CreateArchiveSectionAsync(object? _)
    {
        var sectionName = NewArchiveSectionName.Trim();

        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return;
        }

        try
        {
            var section = await _archiveSectionRepository.GetOrCreateAsync(sectionName);
            NewArchiveSectionName = string.Empty;
            await LoadArchiveSectionsAsync();
            var loadedSection = ArchiveSections.FirstOrDefault(item => item.Id == section.Id) ?? section;

            if (IsArchiveFilter)
            {
                await LoadArchiveSectionAsync(loadedSection);
            }
            else
            {
                SetNotification($"已创建归档分区：{loadedSection.Name}");
            }
        }
        catch (Exception ex)
        {
            SetNotification($"创建归档分区失败：{ex.Message}");
        }
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
        if (!ConfirmDiscardSpotlightChanges())
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

        if (!ShowConfirmationDialog(
                "恢复备份",
                "恢复备份会覆盖当前数据库、附件和人工时选项。恢复前会自动创建一份当前数据的保护备份，是否继续？",
                "恢复"))
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

    private void RefreshBoard(bool refreshCalendar = true)
    {
        var showArchived = _currentFilter == "Archived";
        var tasks = _allTasks
            .Where(task => task.IsArchived == showArchived)
            .Where(PassesTaskFilter)
            .Where(PassesTaskDateFilter)
            .Where(PassesTaskSearch)
            .OrderBy(task => task.SortOrder)
            .ToArray();

        Replace(TodoTasks, tasks.Where(task => task.Status == TaskStatus.Todo));
        Replace(DoingTasks, tasks.Where(task => task.Status == TaskStatus.Doing));
        Replace(BlockedTasks, tasks.Where(task => task.Status == TaskStatus.Blocked));
        Replace(DoneTasks, tasks.Where(task => task.Status == TaskStatus.Done));

        var notes = _allNotes
            .Where(note => note.IsArchived == showArchived)
            .Where(PassesNoteFilter)
            .Where(PassesNoteDateFilter)
            .Where(PassesNoteSearch)
            .OrderBy(note => note.SortOrder);

        Replace(Notes, _currentFilter is "Board" or "WithAttachments" or "Archived" ? notes : Enumerable.Empty<NoteItem>());

        if (refreshCalendar)
        {
            var calendarTasks = _allTasks
                .Where(task => !task.IsArchived && (task.StartDate.HasValue || task.EndDate.HasValue))
                .Where(PassesTaskSearch)
                .OrderBy(task => task.StartDate ?? task.EndDate)
                .ThenBy(task => task.EndDate ?? task.StartDate)
                .ThenBy(task => task.SortOrder)
                .ToArray();

            Replace(CalendarTasks, calendarTasks);
            RefreshCalendar(calendarTasks);
            OnPropertyChanged(nameof(CalendarTaskCount));
            OnPropertyChanged(nameof(HasCalendarTasks));
            OnPropertyChanged(nameof(SelectedCalendarTaskCount));
            OnPropertyChanged(nameof(HasSelectedCalendarTasks));
        }

        OnPropertyChanged(nameof(VisibleTaskCount));
        OnPropertyChanged(nameof(VisibleNoteCount));
        OnPropertyChanged(nameof(IsFocusedTaskAttachmentEmpty));
        OnPropertyChanged(nameof(IsFocusedNoteAttachmentEmpty));
    }

    private void RefreshCalendar(IReadOnlyList<TaskItem> calendarTasks)
    {
        const int visibleChipLimit = 3;
        var firstOfMonth = CalendarMonth;
        var gridStart = firstOfMonth.AddDays(-(((int)firstOfMonth.DayOfWeek + 6) % 7));
        var days = new List<CalendarDayItem>(42);

        for (var offset = 0; offset < 42; offset++)
        {
            var date = gridStart.AddDays(offset);
            var day = new CalendarDayItem
            {
                Date = date,
                IsToday = date == DateTime.Today,
                IsCurrentMonth = date.Month == CalendarMonth.Month && date.Year == CalendarMonth.Year,
                IsSelected = date == SelectedCalendarDate
            };

            var chips = calendarTasks
                .Where(task => IsTaskActiveOnDate(task, date))
                .OrderBy(task => TaskDateRange(task).Start)
                .ThenBy(task => TaskDateRange(task).End)
                .ThenBy(task => task.SortOrder)
                .Select(task => CreateCalendarTaskChip(task, date))
                .ToArray();

            day.TotalTaskCount = chips.Length;
            day.OverflowCount = Math.Max(0, chips.Length - visibleChipLimit);

            foreach (var chip in chips.Take(visibleChipLimit))
            {
                day.VisibleTaskChips.Add(chip);
            }

            days.Add(day);
        }

        Replace(CalendarDays, days);
        RefreshCalendarSelection();
    }

    private void RefreshCalendarSelection()
    {
        foreach (var day in CalendarDays)
        {
            day.IsSelected = day.Date == SelectedCalendarDate;
        }

        Replace(
            SelectedCalendarTasks,
            CalendarTasks
                .Where(task => IsTaskActiveOnDate(task, SelectedCalendarDate))
                .OrderBy(task => TaskDateRange(task).Start)
                .ThenBy(task => TaskDateRange(task).End)
                .ThenBy(task => task.SortOrder));

        OnPropertyChanged(nameof(SelectedCalendarTaskCount));
        OnPropertyChanged(nameof(HasSelectedCalendarTasks));
    }

    private static CalendarTaskChip CreateCalendarTaskChip(TaskItem task, DateTime date)
    {
        var range = TaskDateRange(task);
        var isMultiDay = range.Start != range.End;

        return new CalendarTaskChip
        {
            Task = task,
            Date = date,
            IsMultiDay = isMultiDay,
            StartsOnDate = range.Start == date,
            EndsOnDate = range.End == date
        };
    }

    private static bool IsTaskActiveOnDate(TaskItem task, DateTime date)
    {
        if (task.StartDate is null && task.EndDate is null)
        {
            return false;
        }

        var range = TaskDateRange(task);
        return range.Start <= date.Date && date.Date <= range.End;
    }

    private static (DateTime Start, DateTime End) TaskDateRange(TaskItem task)
    {
        var start = (task.StartDate ?? task.EndDate)!.Value.Date;
        var end = (task.EndDate ?? task.StartDate)!.Value.Date;

        return start <= end
            ? (start, end)
            : (end, start);
    }

    private bool PassesTaskFilter(TaskItem task)
    {
        return _currentFilter switch
        {
            "Today" => IsTodayWithinTaskDateRange(task),
            "High" => task.Priority == TaskPriority.High,
            "Overdue" => task.IsOverdue,
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

    private bool PassesTaskDateFilter(TaskItem task)
    {
        if (!HasDateFilter)
        {
            return true;
        }

        var itemStart = task.StartDate?.Date ?? task.EndDate?.Date;
        var itemEnd = task.EndDate?.Date ?? task.StartDate?.Date;

        if (itemStart is null || itemEnd is null)
        {
            return false;
        }

        return DateRangesOverlap(itemStart.Value, itemEnd.Value);
    }

    private bool PassesNoteDateFilter(NoteItem note)
    {
        return !HasDateFilter || DateRangesOverlap(note.UpdatedAt.Date, note.UpdatedAt.Date);
    }

    private bool DateRangesOverlap(DateTime itemStart, DateTime itemEnd)
    {
        var (filterStart, filterEnd) = NormalizedDateFilter();

        if (itemStart > itemEnd)
        {
            (itemStart, itemEnd) = (itemEnd, itemStart);
        }

        if (filterStart.HasValue && itemEnd < filterStart.Value)
        {
            return false;
        }

        if (filterEnd.HasValue && itemStart > filterEnd.Value)
        {
            return false;
        }

        return true;
    }

    private (DateTime? Start, DateTime? End) NormalizedDateFilter()
    {
        var start = DateFilterStart?.Date;
        var end = DateFilterEnd?.Date;

        if (start.HasValue && end.HasValue && start.Value > end.Value)
        {
            return (end, start);
        }

        return (start, end);
    }

    private bool PassesTaskSearch(TaskItem task)
    {
        if (string.IsNullOrWhiteSpace(_normalizedSearchText))
        {
            return true;
        }

        var query = _normalizedSearchText;
        return task.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
            || task.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
            || task.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase))
            || task.Attachments.Any(attachment => attachment.OriginalFileName.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private bool PassesNoteSearch(NoteItem note)
    {
        if (string.IsNullOrWhiteSpace(_normalizedSearchText))
        {
            return true;
        }

        var query = _normalizedSearchText;
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
            or nameof(TaskItem.DateRangeDisplay)
            or nameof(TaskItem.IsOverdue)
            or nameof(TaskItem.IsExpanded))
        {
            return;
        }

        RefreshBoard(ShouldRefreshCalendarForTaskChange(e.PropertyName));
        await SaveTaskSafelyAsync(task);
    }

    private bool ShouldRefreshCalendarForTaskChange(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName) || !string.IsNullOrWhiteSpace(_normalizedSearchText))
        {
            return true;
        }

        return propertyName is nameof(TaskItem.StartDate)
            or nameof(TaskItem.EndDate)
            or nameof(TaskItem.IsArchived)
            or nameof(TaskItem.ArchiveSectionId)
            or nameof(TaskItem.ArchivedAt)
            or nameof(TaskItem.SortOrder);
    }

    private async void OnNotePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isInitialized || _isLoading || sender is not NoteItem note)
        {
            return;
        }

        if (e.PropertyName is nameof(NoteItem.AttachmentCount)
            or nameof(NoteItem.UpdatedAt)
            or nameof(NoteItem.IsExpanded))
        {
            return;
        }

        RefreshBoard(refreshCalendar: false);
        await SaveNoteSafelyAsync(note);
    }

    private async Task SaveSelectedTaskAsync()
    {
        if (ActiveTask is null)
        {
            return;
        }

        var task = ActiveTask;
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
            IsTaskSpotlightEditing = false;
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
        if (ActiveNote is null)
        {
            return;
        }

        var note = ActiveNote;
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
            IsNoteSpotlightEditing = false;
            RefreshBoard(refreshCalendar: false);
            SetNotification("已保存");
        }
        catch (Exception ex)
        {
            note.Title = oldTitle;
            note.Content = oldContent;
            note.TagsDisplay = oldTags;
            note.UpdatedAt = oldUpdatedAt;
            _isLoading = false;
            RefreshBoard(refreshCalendar: false);
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
        RaiseTaskActionCanExecuteChanged();
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
        RaiseTaskActionCanExecuteChanged();
    }

    private void OnTaskDraftChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedTaskChanges));
        RaiseTaskActionCanExecuteChanged();
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
        RaiseNoteActionCanExecuteChanged();
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
        RaiseNoteActionCanExecuteChanged();
    }

    private void OnNoteDraftChanged()
    {
        OnPropertyChanged(nameof(HasUnsavedNoteChanges));
        RaiseNoteActionCanExecuteChanged();
    }

    private void RaiseTaskActionCanExecuteChanged()
    {
        DeleteTaskCommand.RaiseCanExecuteChanged();
        SaveTaskCommand.RaiseCanExecuteChanged();
        ArchiveTaskCommand.RaiseCanExecuteChanged();
        EditSpotlightCommand.RaiseCanExecuteChanged();
        CancelSpotlightEditCommand.RaiseCanExecuteChanged();
    }

    private void RaiseNoteActionCanExecuteChanged()
    {
        DeleteNoteCommand.RaiseCanExecuteChanged();
        SaveNoteCommand.RaiseCanExecuteChanged();
        ArchiveNoteCommand.RaiseCanExecuteChanged();
        EditSpotlightCommand.RaiseCanExecuteChanged();
        CancelSpotlightEditCommand.RaiseCanExecuteChanged();
    }

    private bool ConfirmDiscardNoteChanges()
    {
        return true;
    }

    private bool ConfirmDiscardTaskChanges()
    {
        return true;
    }

    private bool ConfirmDiscardSpotlightChanges()
    {
        return ConfirmDiscardTaskChanges() && ConfirmDiscardNoteChanges();
    }

    private Task<bool> EnsureDraftsReadyForBackupAsync()
    {
        return Task.FromResult(true);
    }

    private async Task SaveAllAsync()
    {
        await _taskRepository.UpsertRangeAsync(_allTasks);
        await _noteRepository.UpsertRangeAsync(_allNotes);
    }

    private readonly record struct TaskPersistenceState(
        TaskStatus Status,
        int SortOrder,
        DateTime UpdatedAt,
        DateTime? CompletedAt)
    {
        public static TaskPersistenceState From(TaskItem task)
        {
            return new TaskPersistenceState(task.Status, task.SortOrder, task.UpdatedAt, task.CompletedAt);
        }

        public void Restore(TaskItem task)
        {
            task.Status = Status;
            task.SortOrder = SortOrder;
            task.UpdatedAt = UpdatedAt;
            task.CompletedAt = CompletedAt;
        }
    }

    private readonly record struct NotePersistenceState(int SortOrder, DateTime UpdatedAt)
    {
        public static NotePersistenceState From(NoteItem note)
        {
            return new NotePersistenceState(note.SortOrder, note.UpdatedAt);
        }

        public void Restore(NoteItem note)
        {
            note.SortOrder = SortOrder;
            note.UpdatedAt = UpdatedAt;
        }
    }

    private void SetNotification(string message)
    {
        NotificationText = message;
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        var desiredItems = source.ToList();
        var comparer = EqualityComparer<T>.Default;

        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!desiredItems.Contains(target[index], comparer))
            {
                target.RemoveAt(index);
            }
        }

        for (var desiredIndex = 0; desiredIndex < desiredItems.Count; desiredIndex++)
        {
            var desiredItem = desiredItems[desiredIndex];

            if (desiredIndex < target.Count && comparer.Equals(target[desiredIndex], desiredItem))
            {
                continue;
            }

            var existingIndex = IndexOf(target, desiredItem, desiredIndex + 1, comparer);

            if (existingIndex >= 0)
            {
                target.Move(existingIndex, desiredIndex);
                continue;
            }

            target.Insert(desiredIndex, desiredItem);
        }

        while (target.Count > desiredItems.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static int IndexOf<T>(
        ObservableCollection<T> collection,
        T item,
        int startIndex,
        IEqualityComparer<T> comparer)
    {
        for (var index = startIndex; index < collection.Count; index++)
        {
            if (comparer.Equals(collection[index], item))
            {
                return index;
            }
        }

        return -1;
    }
}
