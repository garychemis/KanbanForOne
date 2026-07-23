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

        Assert.Equal(4, entries.Count);
        Assert.Equal("V0.4.2", entries[0].Version);
        Assert.Equal("2026-07-23", entries[0].Date);
        Assert.Contains(
            "新增按项目、专业、工作内容的组合筛选与级联选项更新，空白项表示不筛选，并同步作用于工时分布和汇总明细",
            entries[0].Items);
        Assert.Contains(
            "统一汇总页字体层级、数字与单位排版、列表与表格行高及数值列对齐，并统一侧栏和顶部导航图标",
            entries[0].Items);
        Assert.Equal("V0.4.1", entries[1].Version);
        Assert.Contains("设置页更新日志最高占窗体高度的 60%，超出后可在区域内滚动查看", entries[1].Items);
        Assert.Equal("V0.4", entries[2].Version);
        Assert.Contains("新增超期未完成筛选入口", entries[3].Items);
    }
}
