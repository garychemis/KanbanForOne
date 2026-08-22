using System.IO;
using KanbanForOne.Models;
using KanbanForOne.Services;
using Microsoft.Data.Sqlite;
using TaskStatus = KanbanForOne.Models.TaskStatus;

namespace KanbanForOne.Tests;

/// <summary>
/// 归档业务端到端测试：任务归档/恢复、按分区查询、删除分区后的内容迁移。
/// </summary>
public sealed class ArchiveIntegrationTests
{
    [Fact]
    public async Task Archive_and_restore_task_roundtrip()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var taskRepository = new TaskRepository(database);
            var sectionRepository = new ArchiveSectionRepository(database);
            var section = await sectionRepository.GetOrCreateAsync("项目X");

            var task = new TaskItem
            {
                Title = "设计评审",
                Description = "整理评审意见",
                Status = TaskStatus.Doing,
                Priority = TaskPriority.High,
                SortOrder = 1
            };
            await taskRepository.UpsertAsync(task);

            // 归档到自定义分区
            task.IsArchived = true;
            task.ArchiveSectionId = section.Id;
            task.ArchivedAt = DateTime.Now;
            await taskRepository.UpsertAsync(task);

            var archived = await taskRepository.GetArchivedBySectionAsync(section.Id);
            var archivedTask = Assert.Single(archived);
            Assert.Equal(task.Id, archivedTask.Id);
            Assert.True(archivedTask.IsArchived);
            Assert.Empty(await taskRepository.GetActiveAsync());

            // 恢复
            archivedTask.IsArchived = false;
            archivedTask.ArchiveSectionId = null;
            archivedTask.ArchivedAt = null;
            await taskRepository.UpsertAsync(archivedTask);

            var active = await taskRepository.GetActiveAsync();
            var restoredTask = Assert.Single(active);
            Assert.Equal(task.Id, restoredTask.Id);
            Assert.False(restoredTask.IsArchived);
            Assert.Null(restoredTask.ArchiveSectionId);
            Assert.Empty(await taskRepository.GetArchivedBySectionAsync(section.Id));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Delete_section_moves_tasks_to_default_and_preserves_restore()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var taskRepository = new TaskRepository(database);
            var sectionRepository = new ArchiveSectionRepository(database);
            var section = await sectionRepository.GetOrCreateAsync("临时分区");
            var defaultSection = await sectionRepository.GetDefaultAsync();

            var task = new TaskItem
            {
                Title = "待迁移任务",
                IsArchived = true,
                ArchiveSectionId = section.Id,
                ArchivedAt = DateTime.Now
            };
            await taskRepository.UpsertAsync(task);

            await sectionRepository.DeleteAndMoveContentsToDefaultAsync(section.Id);

            var moved = Assert.Single(await taskRepository.GetArchivedBySectionAsync(defaultSection.Id));
            Assert.Equal(task.Id, moved.Id);
            Assert.Equal(defaultSection.Id, moved.ArchiveSectionId);

            // 迁移后的卡片仍可正常恢复
            moved.IsArchived = false;
            moved.ArchiveSectionId = null;
            moved.ArchivedAt = null;
            await taskRepository.UpsertAsync(moved);
            Assert.Contains(await taskRepository.GetActiveAsync(), item => item.Id == task.Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Section_counts_update_after_archiving_and_restoring()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var taskRepository = new TaskRepository(database);
            var sectionRepository = new ArchiveSectionRepository(database);
            var section = await sectionRepository.GetOrCreateAsync("统计");

            var task = new TaskItem { Title = "计数任务" };
            await taskRepository.UpsertAsync(task);

            // 归档后计数为 1
            task.IsArchived = true;
            task.ArchiveSectionId = section.Id;
            task.ArchivedAt = DateTime.Now;
            await taskRepository.UpsertAsync(task);
            Assert.Equal(1, (await sectionRepository.GetAllAsync()).Single(s => s.Id == section.Id).TotalCount);

            // 恢复后计数为 0
            task.IsArchived = false;
            task.ArchiveSectionId = null;
            task.ArchivedAt = null;
            await taskRepository.UpsertAsync(task);
            Assert.Equal(0, (await sectionRepository.GetAllAsync()).Single(s => s.Id == section.Id).TotalCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Restore_explicit_archived_task_updates_section_count()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var taskRepository = new TaskRepository(database);
            var sectionRepository = new ArchiveSectionRepository(database);
            var section = await sectionRepository.GetOrCreateAsync("恢复分区");

            var task = new TaskItem
            {
                Title = "待恢复任务",
                IsArchived = true,
                ArchiveSectionId = section.Id,
                ArchivedAt = DateTime.Now,
                Status = TaskStatus.Doing
            };
            await taskRepository.UpsertAsync(task);

            // 模拟“以具体卡片为参数恢复”：直接对归档任务执行恢复落库
            task.IsArchived = false;
            task.ArchiveSectionId = null;
            task.ArchivedAt = null;
            await taskRepository.UpsertAsync(task);

            // 恢复后归档分区不再包含该任务，且分区计数回零
            Assert.DoesNotContain(
                await taskRepository.GetArchivedBySectionAsync(section.Id),
                item => item.Id == task.Id);
            Assert.Equal(0, (await sectionRepository.GetAllAsync()).Single(s => s.Id == section.Id).TotalCount);

            // 任务回到活动列表
            Assert.Contains(await taskRepository.GetActiveAsync(), item => item.Id == task.Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
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
