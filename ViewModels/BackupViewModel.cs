using System.IO;
using KanbanForOne.Modules.DesignConditions.Data;
using KanbanForOne.Services;

namespace KanbanForOne.ViewModels;

/// <summary>
/// 完整数据备份页：一次创建或恢复全部模块数据。
/// </summary>
public sealed class BackupViewModel : ObservableObject
{
    private readonly UnifiedBackupService _backupService;
    private readonly BoardViewModel _board;
    private readonly NotificationService _notifications;
    private readonly DesignConditionStorageOptions _designConditionPaths;
    private readonly IDialogService _dialogs;
    private readonly IFilePickerService _filePickers;
    private string _lastBackupPath = string.Empty;
    private int _lastBackupAttachmentCount;
    private long _lastBackupSizeBytes;
    private DateTime? _lastBackupCreatedAt;
    private string _lastRestoreSourcePath = string.Empty;
    private string _lastRestoreProtectiveBackupPath = string.Empty;
    private int _lastRestoreAttachmentCount;
    private DateTime? _lastRestoreAt;

    public BackupViewModel(
        UnifiedBackupService backupService,
        BoardViewModel board,
        NotificationService notifications,
        DesignConditionStorageOptions designConditionPaths,
        IDialogService dialogs,
        IFilePickerService filePickers)
    {
        _backupService = backupService;
        _board = board;
        _notifications = notifications;
        _designConditionPaths = designConditionPaths;
        _dialogs = dialogs;
        _filePickers = filePickers;
        CreateBackupCommand = new RelayCommand(CreateBackupAsync);
        RestoreBackupCommand = new RelayCommand(RestoreBackupAsync);
    }

    /// <summary>恢复完成后触发，由主窗口重新加载全部模块数据。</summary>
    public event Func<Task>? RestoreCompleted;

    public RelayCommand CreateBackupCommand { get; }
    public RelayCommand RestoreBackupCommand { get; }
    public string DataDirectory => AppPaths.DataRoot;
    public string DatabasePath => AppPaths.DatabasePath;
    public string AttachmentDirectory => AppPaths.AttachmentRoot;
    public string WorkHourOptionsPath => AppPaths.WorkHourOptionsPath;
    public string BackupDirectory => AppPaths.BackupRoot;
    public string DesignConditionDatabasePath => _designConditionPaths.DatabasePath;
    public string DesignConditionAttachmentDirectory => _designConditionPaths.AttachmentRoot;

    public string LastBackupPath
    {
        get => _lastBackupPath;
        private set
        {
            if (SetProperty(ref _lastBackupPath, value)) OnPropertyChanged(nameof(HasLastBackup));
        }
    }

    public bool HasLastBackup => !string.IsNullOrWhiteSpace(LastBackupPath);
    public int LastBackupAttachmentCount { get => _lastBackupAttachmentCount; private set => SetProperty(ref _lastBackupAttachmentCount, value); }
    public long LastBackupSizeBytes { get => _lastBackupSizeBytes; private set => SetProperty(ref _lastBackupSizeBytes, value); }
    public DateTime? LastBackupCreatedAt { get => _lastBackupCreatedAt; private set => SetProperty(ref _lastBackupCreatedAt, value); }

    public string LastRestoreSourcePath
    {
        get => _lastRestoreSourcePath;
        private set
        {
            if (SetProperty(ref _lastRestoreSourcePath, value)) OnPropertyChanged(nameof(HasLastRestore));
        }
    }

    public string LastRestoreProtectiveBackupPath { get => _lastRestoreProtectiveBackupPath; private set => SetProperty(ref _lastRestoreProtectiveBackupPath, value); }
    public int LastRestoreAttachmentCount { get => _lastRestoreAttachmentCount; private set => SetProperty(ref _lastRestoreAttachmentCount, value); }
    public DateTime? LastRestoreAt { get => _lastRestoreAt; private set => SetProperty(ref _lastRestoreAt, value); }
    public bool HasLastRestore => !string.IsNullOrWhiteSpace(LastRestoreSourcePath);

    private async Task CreateBackupAsync()
    {
        try
        {
            await _board.SaveAllAsync();
            var result = await _backupService.CreateBackupAsync();
            LastBackupPath = result.BackupPath;
            LastBackupAttachmentCount = result.AttachmentFileCount;
            LastBackupSizeBytes = result.BackupSizeBytes;
            LastBackupCreatedAt = result.CreatedAt;
            _notifications.Notify($"完整备份已创建：{result.BackupPath}");
        }
        catch (Exception ex)
        {
            _notifications.Notify($"创建完整备份失败：{ex.Message}");
        }
    }

    private async Task RestoreBackupAsync()
    {
        if (!_board.ConfirmDiscardSpotlightChanges()) return;

        var file = _filePickers.PickOpenFile(
            "选择 Kanban41 完整备份文件",
            Directory.Exists(BackupDirectory) ? BackupDirectory : DataDirectory,
            "Kanban41 完整备份 (*.zip)|*.zip|所有文件 (*.*)|*.*");
        if (file is null) return;

        if (!_dialogs.Confirm(
                "恢复完整备份",
                "恢复会覆盖看板、任务、人工时、全部附件和设计条件数据。恢复前会自动创建一份当前全部数据的保护备份，是否继续？",
                "恢复")) return;

        try
        {
            _board.ClearSpotlightState();
            var result = await _backupService.RestoreBackupAsync(file);
            if (RestoreCompleted is not null) await RestoreCompleted();

            LastRestoreSourcePath = result.SourceBackupPath;
            LastRestoreProtectiveBackupPath = result.ProtectiveBackupPath;
            LastRestoreAttachmentCount = result.AttachmentFileCount;
            LastRestoreAt = result.RestoredAt;
            _notifications.Notify($"已恢复完整备份：{Path.GetFileName(result.SourceBackupPath)}");
        }
        catch (Exception ex)
        {
            _notifications.Notify($"恢复完整备份失败：{ex.Message}");
        }
    }
}
