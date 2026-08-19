using System.Reflection;
using KanbanForOne.Services;

namespace KanbanForOne.ViewModels;

/// <summary>
/// 关于页：应用身份、联系信息、其他作品和版本日志。
/// </summary>
public sealed class AboutViewModel
{
    private static readonly Assembly AppAssembly = typeof(AboutViewModel).Assembly;

    public string AppVersion => AppAssembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion
        ?? AppAssembly.GetName().Version?.ToString()
        ?? string.Empty;

    public string CopyrightText => AppAssembly
        .GetCustomAttribute<AssemblyCopyrightAttribute>()?
        .Copyright
        ?? string.Empty;

    public string ContactEmail => "pipingcalculator@163.com";

    public string PortfolioUrl => "https://pipingcalculator.site";

    public IReadOnlyList<ReleaseNoteEntry> ReleaseNotes => ReleaseNotesService.FromAssembly(AppAssembly);
}
