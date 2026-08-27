using Microsoft.Win32;

namespace KanbanForOne.Services;

/// <summary>
/// <see cref="IFilePickerService"/> 的 WPF 实现：包装 Microsoft.Win32 文件对话框。
/// </summary>
public sealed class FilePickerService : IFilePickerService
{
    public string? PickOpenFile(string title, string initialDirectory, string filter)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Title = title,
            InitialDirectory = string.IsNullOrEmpty(initialDirectory) ? null : initialDirectory,
            Filter = filter
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public IReadOnlyList<string> PickOpenFiles(string title, string initialDirectory, string filter)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = true,
            Title = title,
            InitialDirectory = string.IsNullOrEmpty(initialDirectory) ? null : initialDirectory,
            Filter = filter
        };
        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public string? PickSaveFile(string title, string defaultFileName, string filter, string defaultExtension)
    {
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = defaultExtension,
            Filter = filter,
            FileName = defaultFileName,
            Title = title
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
