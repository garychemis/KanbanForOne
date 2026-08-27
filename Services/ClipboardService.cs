using System.Windows.Media.Imaging;

namespace KanbanForOne.Services;

/// <summary>
/// <see cref="IClipboardService"/> 的 WPF 实现：包装 System.Windows.Clipboard。
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    public bool ContainsImage() => System.Windows.Clipboard.ContainsImage();

    public BitmapSource? GetImage() => System.Windows.Clipboard.GetImage();
}
