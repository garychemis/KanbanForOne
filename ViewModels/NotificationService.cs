namespace KanbanForOne.ViewModels;

/// <summary>
/// 应用级通知状态，供主窗口顶部气泡与各 ViewModel 共享。
/// </summary>
public sealed class NotificationService : ObservableObject
{
    private string _notificationText = string.Empty;

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

    public void Notify(string message)
    {
        NotificationText = message;
    }
}
