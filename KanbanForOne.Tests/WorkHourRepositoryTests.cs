using KanbanForOne.Models;
using KanbanForOne.Services;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Tests;

public sealed class WorkHourRepositoryTests
{
    [Fact]
    public async Task Repository_keeps_duplicate_dimensions_as_separate_entries()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var repository = new WorkHourRepository(database);
            var date = new DateTime(2026, 7, 17);

            await repository.UpsertAsync(CreateEntry(date, 125, "上午设计"));
            await repository.UpsertAsync(CreateEntry(date, 250, "下午设计"));

            var entries = await repository.GetByDateAsync(date);
            Assert.Equal(2, entries.Count);
            Assert.Equal(375, await repository.GetTotalUnitsByDateAsync(date));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Repository_updates_and_deletes_by_id()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var repository = new WorkHourRepository(database);
            var entry = CreateEntry(new DateTime(2026, 7, 17), 100, string.Empty);
            await repository.UpsertAsync(entry);

            entry.ProjectNumber = "AB12";
            entry.HourUnits = 175;
            entry.UpdatedAt = DateTime.Now;
            await repository.UpsertAsync(entry);

            var loaded = Assert.Single(await repository.GetByDateAsync(entry.WorkDate));
            Assert.Equal("AB12", loaded.ProjectNumber);
            Assert.Equal(175, loaded.HourUnits);

            await repository.DeleteAsync(entry.Id);
            Assert.Empty(await repository.GetByDateAsync(entry.WorkDate));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Repository_summarizes_dimensions_within_inclusive_date_range()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var repository = new WorkHourRepository(database);

            var first = CreateEntry(new DateTime(2026, 7, 1), 125, "first");
            var second = CreateEntry(new DateTime(2026, 7, 31), 275, "second");
            var different = CreateEntry(new DateTime(2026, 7, 15), 300, string.Empty);
            different.WorkActivity = "Review";
            var outside = CreateEntry(new DateTime(2026, 8, 1), 800, string.Empty);

            await repository.UpsertAsync(first);
            await repository.UpsertAsync(second);
            await repository.UpsertAsync(different);
            await repository.UpsertAsync(outside);

            var summaries = await repository.GetSummaryAsync(
                new DateTime(2026, 7, 1),
                new DateTime(2026, 7, 31));
            var details = await repository.GetByDateRangeAsync(
                new DateTime(2026, 7, 1),
                new DateTime(2026, 7, 31));

            Assert.Equal(2, summaries.Count);
            var design = summaries.Single(item => item.WorkActivity == first.WorkActivity);
            Assert.Equal(400, design.TotalHourUnits);
            Assert.Equal(2, design.EntryCount);
            Assert.Equal(3, details.Count);
            Assert.Equal(new DateTime(2026, 7, 1), details.First().WorkDate);
            Assert.Equal(new DateTime(2026, 7, 31), details.Last().WorkDate);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static WorkHourEntry CreateEntry(DateTime date, int units, string remark)
    {
        return new WorkHourEntry
        {
            WorkDate = date,
            ProjectNumber = "P1001",
            Discipline = "管道",
            WorkActivity = "设计",
            HourUnits = units,
            Remark = remark,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    private static string CreateTestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "KanbanForOne.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
