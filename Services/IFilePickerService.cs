namespace KanbanForOne.Services;

/// <summary>
/// 文件选择对话框抽象：ViewModel 通过本接口选择文件，不直接依赖 Microsoft.Win32 对话框。
/// </summary>
public interface IFilePickerService
{
    /// <summary>选择单个文件，返回 null 表示取消。</summary>
    string? PickOpenFile(string title, string initialDirectory, string filter);

    /// <summary>选择多个文件，返回空集合表示取消。</summary>
    IReadOnlyList<string> PickOpenFiles(string title, string initialDirectory, string filter);

    /// <summary>选择保存位置，返回 null 表示取消。</summary>
    string? PickSaveFile(string title, string defaultFileName, string filter, string defaultExtension);
}
