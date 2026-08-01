using System.IO;
using KanbanForOne.Models;
using KanbanForOne.Services;
using Microsoft.Data.Sqlite;
using TaskStatus = KanbanForOne.Models.TaskStatus;

namespace KanbanForOne.Tests;

public sealed class ArchiveSectionRepositoryTests
{
    [Fact]
    public async Task Initialize_creates_default_section()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var repository = new ArchiveSectionRepository(database);

            var sections = await repository.GetAllAsync();
            var defaultSection = Assert.Single(sections);
            Assert.Equal("默认", defaultSection.Name);
            Assert.True(defaultSection.IsDefault);
            Assert.Equal(ArchiveSection.DefaultId, defaultSection.Id);

            var byDefaultLookup = await repository.GetDefaultAsync();
            Assert.Equal(defaultSection.Id, byDefaultLookup.Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetOrCreate_reuses_existing_section_case_insensitive()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var repository = new ArchiveSectionRepository(database);

            var first = await repository.GetOrCreateAsync(" 项目A ");
            var second = await repository.GetOrCreateAsync("项目a");
            var third = await repository.GetOrCreateAsync("项目A");

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(first.Id, third.Id);
            Assert.Equal("项目A", first.Name);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetAll_reports_counts_for_tasks_and_notes_per_section()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var sectionRepository = new ArchiveSectionRepository(database);
            var taskRepository = new TaskRepository(database);
            var noteRepository = new NoteRepository(database);

            var section = await sectionRepository.GetOrCreateAsync("工作");
            var defaultSection = await sectionRepository.GetDefaultAsync();

            await ArchiveTaskAsync(taskRepository, section.Id, "任务1");
            await ArchiveTaskAsync(taskRepository, section.Id, "任务2");
            await ArchiveTaskAsync(taskRepository, defaultSection.Id, "任务3");
            await ArchiveNoteAsync(noteRepository, section.Id, "备忘1");

            var sections = await sectionRepository.GetAllAsync();
            var loadedSection = sections.Single(item => item.Id == section.Id);
            Assert.Equal(2, loadedSection.TaskCount);
            Assert.Equal(1, loadedSection.NoteCount);
            Assert.Equal(3, loadedSection.TotalCount);

            var loadedDefault = sections.Single(item => item.Id == defaultSection.Id);
            Assert.Equal(1, loadedDefault.TaskCount);
            Assert.Equal(0, loadedDefault.NoteCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteSection_moves_archived_content_to_default()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var sectionRepository = new ArchiveSectionRepository(database);
            var taskRepository = new TaskRepository(database);
            var noteRepository = new NoteRepository(database);

            var section = await sectionRepository.GetOrCreateAsync("临时");
            var defaultSection = await sectionRepository.GetDefaultAsync();

            var task1 = await ArchiveTaskAsync(taskRepository, section.Id, "任务1");
            var task2 = await ArchiveTaskAsync(taskRepository, section.Id, "任务2");
            var note1 = await ArchiveNoteAsync(noteRepository, section.Id, "备忘1");

            var deleted = await sectionRepository.DeleteAndMoveContentsToDefaultAsync(section.Id);
            Assert.True(deleted);

            var movedTasks = await taskRepository.GetArchivedBySectionAsync(defaultSection.Id);
            Assert.Equal(2, movedTasks.Count);
            Assert.Contains(movedTasks, item => item.Id == task1.Id);
            Assert.Contains(movedTasks, item => item.Id == task2.Id);
            Assert.All(movedTasks, item => Assert.Equal(defaultSection.Id, item.ArchiveSectionId));

            var movedNotes = await noteRepository.GetArchivedBySectionAsync(defaultSection.Id);
            Assert.Single(movedNotes);
            Assert.Equal(note1.Id, movedNotes[0].Id);

            Assert.Empty(await taskRepository.GetArchivedBySectionAsync(section.Id));
            Assert.Empty(await noteRepository.GetArchivedBySectionAsync(section.Id));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteSection_refuses_default_section()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var repository = new ArchiveSectionRepository(database);
            var defaultSection = await repository.GetDefaultAsync();

            var deleted = await repository.DeleteAndMoveContentsToDefaultAsync(defaultSection.Id);
            Assert.False(deleted);

            var sections = await repository.GetAllAsync();
            Assert.Contains(sections, section => section.Id == defaultSection.Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static async Task<TaskItem> ArchiveTaskAsync(TaskRepository repository, Guid sectionId, string title)
    {
        var task = new TaskItem
        {
            Title = title,
            Status = TaskStatus.Todo,
            Priority = TaskPriority.Medium,
            IsArchived = true,
            ArchiveSectionId = sectionId,
            ArchivedAt = DateTime.Now,
            SortOrder = 0
        };
        await repository.UpsertAsync(task);
        return task;
    }

    private static async Task<NoteItem> ArchiveNoteAsync(NoteRepository repository, Guid sectionId, string title)
    {
        var note = new NoteItem
        {
            Title = title,
            Content = string.Empty,
            IsArchived = true,
            ArchiveSectionId = sectionId,
            ArchivedAt = DateTime.Now,
            SortOrder = 0
        };
        await repository.UpsertAsync(note);
        return note;
    }

    private static string CreateTestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "KanbanForOne.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
