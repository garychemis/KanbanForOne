using System.Text.Json;
using KanbanForOne.Services;

namespace KanbanForOne.Tests;

public sealed class WorkHourOptionsServiceTests
{
    [Fact]
    public async Task LoadAsync_creates_default_json_configuration()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var path = Path.Combine(testRoot, "workhour-options.json");
            var service = new WorkHourOptionsService(path);

            var options = await service.LoadAsync();

            Assert.Equal(["工艺", "管道", "外管", "管材", "管机"], options.Disciplines);
            Assert.Equal(["设计", "校核", "审核", "审定", "设计管理", "会议"], options.WorkActivities);
            Assert.True(File.Exists(path));
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("Disciplines").ValueKind);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Added_options_are_deduplicated_and_persisted()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var path = Path.Combine(testRoot, "workhour-options.json");
            var service = new WorkHourOptionsService(path);
            await service.LoadAsync();

            await service.AddDisciplineAsync("  电气  ");
            await service.AddDisciplineAsync("电气");
            await service.AddWorkActivityAsync("现场服务");

            var reloaded = await new WorkHourOptionsService(path).LoadAsync();
            Assert.Equal(1, reloaded.Disciplines.Count(value => value == "电气"));
            Assert.Contains("现场服务", reloaded.WorkActivities);

            var removed = await service.RemoveDisciplineAsync("电气");
            Assert.DoesNotContain("电气", removed.Disciplines);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static string CreateTestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "KanbanForOne.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
