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

        Assert.Equal(8, entries.Count);
        Assert.Equal("V0.5.0.2", entries[0].Version);
        Assert.Equal("2026-08-22", entries[0].Date);
        Assert.Contains(entries[0].Items, item => item.Contains("略微优化归档部分UI"));
        Assert.Equal("V0.5.0.1", entries[1].Version);
        Assert.Contains(entries[1].Items, item => item.Contains("修复可以启动多个进程的问题"));
        Assert.Equal("V0.5.0", entries[2].Version);
        Assert.Equal("2026-08-19", entries[2].Date);
        Assert.Contains(entries[2].Items, item => item.Contains("新增设计条件归档模块"));
        Assert.Equal("V0.4.3", entries[3].Version);
        Assert.Equal("2026-08-01", entries[3].Date);
        Assert.Contains(entries[3].Items, item => item.Contains("归档页 UI 重做"));
        Assert.Contains(entries[3].Items, item => item.Contains("人工时汇总页 UI 美化"));
        Assert.Equal("V0.4.2", entries[4].Version);
        Assert.Contains(entries[4].Items, item => item.Contains("新增按项目、专业、工作内容的组合筛选"));
        Assert.Equal("V0.4.1", entries[5].Version);
        Assert.Contains(entries[5].Items, item => item.Contains("设置页更新日志最高占窗体高度的 60%"));
        Assert.Equal("V0.4", entries[6].Version);
        Assert.Contains(entries[6].Items, item => item.Contains("新增人工时汇总页面"));
        Assert.Equal("v0.3.3", entries[7].Version);
        Assert.Contains(entries[7].Items, item => item.Contains("新增超期未完成筛选入口"));
    }
}
