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

        Assert.Equal(3, entries.Count);
        Assert.Equal("V0.41", entries[0].Version);
        Assert.Equal("2026-07-22", entries[0].Date);
        Assert.Contains("修复人工时汇总“工时分布”列表的滚动条遮挡文字问题，预留滚动条安全间距", entries[0].Items);
        Assert.Contains("设置页更新日志最高占窗体高度的 60%，超出后可在区域内滚动查看", entries[0].Items);
        Assert.Equal("V0.4", entries[1].Version);
        Assert.Contains("新增超期未完成筛选入口", entries[2].Items);
    }
}
