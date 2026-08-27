using System.Windows.Media.Imaging;

namespace KanbanForOne.Services;

/// <summary>
/// 剪贴板抽象：ViewModel 通过本接口读取剪贴板图片，不直接依赖 System.Windows.Clipboard。
/// </summary>
public interface IClipboardService
{
    bool ContainsImage();

    BitmapSource? GetImage();
}
