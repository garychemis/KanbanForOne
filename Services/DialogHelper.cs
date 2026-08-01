using System.Windows;
using KanbanForOne.Controls;

namespace KanbanForOne.Services;

/// <summary>
/// 对话框宿主与通用确认框辅助。
/// </summary>
public static class DialogHelper
{
    public static Window? GetDialogOwner()
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

    public static bool Confirm(string title, string message, string confirmText)
    {
        return ConfirmDialog.Show(GetDialogOwner(), title, message, confirmText, "取消");
    }
}
