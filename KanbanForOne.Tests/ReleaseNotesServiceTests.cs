using KanbanForOne.Services;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Tests;

public sealed class ReleaseNotesServiceTests
{
    [Fact]
    public void Parse_creates_release_entries_from_pipe_and_semicolon_text()
    {
        const string source = "v0.3.3|2026-06-17|增加超期未完成检查；新增超期未完成筛选入口；卡片显示超期标注";

        var entries = ReleaseNotesService.Parse(source);

        var entry = Assert.Single(entries);
        Assert.Equal("v0.3.3", entry.Version);
        Assert.Equal("2026-06-17", entry.Date);
        Assert.Equal(
            ["增加超期未完成检查", "新增超期未完成筛选入口", "卡片显示超期标注"],
            entry.Items);
    }

    [Fact]
    public void FromAssembly_reads_release_notes_from_project_metadata()
    {
        var entries = ReleaseNotesService.FromAssembly(typeof(MainWindowViewModel).Assembly);

        Assert.Equal(2, entries.Count);
        Assert.Equal("V0.4", entries[0].Version);
        Assert.Equal("2026-07-17", entries[0].Date);
        Assert.Contains("新增个人工时录入与管理，支持项目号、专业、工作内容、工时及可选备注", entries[0].Items);
        Assert.Contains("新增超期未完成筛选入口", entries[1].Items);
    }
}
