using System.IO;
using KanbanForOne.Services;
using Microsoft.Win32;

namespace KanbanForOne.ViewModels;

/// <summary>
/// 数据备份页：创建 ZIP 备份与恢复备份。
/// </summary>
public sealed class BackupViewModel : ObservableObject
{
    private readonly BackupService _backupService;
    private readonly BoardViewModel _board;
    private readonly NotificationService _notifications;
    private string _lastBackupPath = string.Empty;
    private int _lastBackupAttachmentCount;
    private long _lastBackupSizeBytes;
    private DateTime? _lastBackupCreatedAt;
    private string _lastRestoreSourcePath = string.Empty;
    private string _lastRestoreProtectiveBackupPath = string.Empty;
    private int _lastRestoreAttachmentCount;
    private DateTime? _lastRestoreAt;

    public BackupViewModel(
        BackupService backupService,
        BoardViewModel board,
        NotificationService notifications)
    {
        _backupService = backupService;
        _board = board;
        _notifications = notifications;

        CreateBackupCommand = new RelayCommand(CreateBackupAsync);
        RestoreBackupCommand = new RelayCommand(RestoreBackupAsync);
    }

    /// <summary>恢复完成后触发，由主窗口重新加载全部数据。</summary>
    public event Func<Task>? RestoreCompleted;

    public RelayCommand CreateBackupCommand { get; }

    public RelayCommand RestoreBackupCommand { get; }

    public string DataDirectory => AppPaths.DataRoot;

    public string DatabasePath => AppPaths.DatabasePath;

    public string AttachmentDirectory => AppPaths.AttachmentRoot;

    public string BackupDirectory => AppPaths.BackupRoot;

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
            _notifications.Notify($"备份已创建：{result.BackupPath}");
        }
        catch (Exception ex)
        {
            _notifications.Notify($"创建备份失败：{ex.Message}");
        }
    }

    private async Task RestoreBackupAsync()
    {
        if (!_board.ConfirmDiscardSpotlightChanges())
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

        if (!DialogHelper.Confirm(
                "恢复备份",
                "恢复备份会覆盖当前数据库、附件和人工时选项。恢复前会自动创建一份当前数据的保护备份，是否继续？",
                "恢复"))
        {
            return;
        }

        try
        {
            _board.ClearSpotlightState();

            var result = await _backupService.RestoreBackupAsync(dialog.FileName);
            if (RestoreCompleted is not null)
            {
                await RestoreCompleted();
            }

            LastRestoreSourcePath = result.SourceBackupPath;
            LastRestoreProtectiveBackupPath = result.ProtectiveBackupPath;
            LastRestoreAttachmentCount = result.AttachmentFileCount;
            LastRestoreAt = result.RestoredAt;
            _notifications.Notify($"已恢复备份：{Path.GetFileName(result.SourceBackupPath)}");
        }
        catch (Exception ex)
        {
            _notifications.Notify($"恢复备份失败：{ex.Message}");
        }
    }
}
