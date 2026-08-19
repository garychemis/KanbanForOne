using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using KanbanForOne.Controls;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Modules.DesignConditions.Services;
using KanbanForOne.Modules.DesignConditions.ViewModels;
using Microsoft.Win32;

namespace KanbanForOne.Modules.DesignConditions.Views;

public enum DesignConditionEditorAction { None, Save, Delete }

public partial class DesignConditionEditorDialog : Window
{
    private readonly DesignConditionEditorViewModel _viewModel;
    private readonly DesignConditionAttachmentStorageService _storage;
    private bool _allowClose;

    private DesignConditionEditorDialog(DesignConditionEditorViewModel viewModel, DesignConditionAttachmentStorageService storage)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _storage = storage;
        DataContext = viewModel;
        Closing += OnClosing;
    }

    public DesignConditionEditorAction Action { get; private set; }

    public static DesignConditionEditorAction Show(Window? owner, DesignConditionEditorViewModel viewModel, DesignConditionAttachmentStorageService storage)
    {
        var dialog = new DesignConditionEditorDialog(viewModel, storage);
        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.Height = Math.Clamp(owner.ActualHeight * 0.82, 620, 720);
        }
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowDialog();
        return dialog.Action;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.TryBuild(out _)) return;
        Action = DesignConditionEditorAction.Save;
        CloseAllowed();
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Source is null || !ConfirmDialog.Show(this, "删除设计条件", "确定删除这条设计条件及其全部文件吗？", "删除")) return;
        Action = DesignConditionEditorAction.Delete;
        CloseAllowed();
    }

    private void OnAddFilesClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "选择设计条件文件", Filter = "所有文件 (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true) _viewModel.AddPendingFiles(dialog.FileNames);
    }

    private void OnOpenAttachmentClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DesignConditionAttachment attachment) return;
        try { _storage.Open(attachment); }
        catch (Exception ex) { _viewModel.ShowOperationError(ex.Message); }
    }

    private void OnRevealAttachmentClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DesignConditionAttachment attachment) return;
        try { _storage.Reveal(attachment); }
        catch (Exception ex) { _viewModel.ShowOperationError(ex.Message); }
    }

    private void OnRemoveAttachmentClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DesignConditionAttachment attachment) _viewModel.RemoveAttachment(attachment);
    }

    private void OnRemovePendingClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is string path) _viewModel.RemovePendingFile(path);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseAllowed();
    private void CloseAllowed() { _allowClose = true; Close(); }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!_allowClose) Action = DesignConditionEditorAction.None; }
    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try { DragMove(); }
        catch (InvalidOperationException)
        {
            // 鼠标状态在 WPF 开始拖动时改变会导致 DragMove 抛出，可安全忽略。
        }
    }
}
