using System.IO;

namespace KanbanForOne.Services;

internal static class AttachmentOpenSafety
{
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".appref-ms", ".appx", ".application", ".bat", ".cmd", ".com", ".cpl",
        ".exe", ".gadget", ".hta", ".inf", ".ins", ".isp", ".jar", ".js",
        ".jse", ".lnk", ".msi", ".msix", ".msp", ".pif", ".ps1", ".psm1",
        ".reg", ".scr", ".sct", ".shb", ".shs", ".url", ".vb", ".vba",
        ".vbe", ".vbs", ".wsc", ".wsf", ".wsh"
    };

    public static void EnsureSafeToOpen(string path)
    {
        if (BlockedExtensions.Contains(Path.GetExtension(path)))
        {
            throw new InvalidOperationException(
                "出于安全考虑，不能从应用内直接运行可执行文件、脚本或系统快捷方式。请使用“定位”并确认来源后自行处理。");
        }
    }

    internal static bool IsBlocked(string path) => BlockedExtensions.Contains(Path.GetExtension(path));
}
