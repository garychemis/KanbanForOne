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

        Assert.Equal(5, entries.Count);
        Assert.Equal("V0.4.3", entries[0].Version);
        Assert.Equal("2026-08-01", entries[0].Date);
        Assert.Contains(
            "归档页 UI 重做：顶部简化为分区选择器，新建与管理分区统一通过弹窗完成，配色、圆角与整体主题对齐",
            entries[0].Items);
        Assert.Contains(
            "人工时汇总页 UI 美化：自定义配色全部替换为全局主题色板，面板圆角与柔和投影质感统一",
            entries[0].Items);
        Assert.Equal("V0.4.2", entries[1].Version);
        Assert.Contains(
            "新增按项目、专业、工作内容的组合筛选与级联选项更新，空白项表示不筛选，并同步作用于工时分布和汇总明细",
            entries[1].Items);
        Assert.Equal("V0.4.1", entries[2].Version);
        Assert.Contains("设置页更新日志最高占窗体高度的 60%，超出后可在区域内滚动查看", entries[2].Items);
        Assert.Equal("V0.4", entries[3].Version);
        Assert.Contains("新增超期未完成筛选入口", entries[4].Items);
    }
}
