using KanbanForOne.Models;
using KanbanForOne.Services;
using KanbanForOne.ViewModels;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Tests;

public sealed class WorkHourSummaryViewModelTests
{
    [Fact]
    public async Task Dimension_filters_update_visible_summaries_and_totals_with_and_semantics()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var repository = new WorkHourRepository(database);
            var date = DateTime.Today;
            await repository.UpsertAsync(CreateEntry(date, "P1001", "Pipe", "Design", 100));
            await repository.UpsertAsync(CreateEntry(date, "P1001", "Pipe", "Review", 200));
            await repository.UpsertAsync(CreateEntry(date, "P2002", "Electrical", "Review", 300));

            var viewModel = CreateViewModel(repository);
            await viewModel.EnsureLoadedAsync();

            Assert.Equal([string.Empty, "P1001", "P2002"], viewModel.ProjectFilterOptions);
            Assert.Equal([string.Empty, "Electrical", "Pipe"], viewModel.DisciplineFilterOptions);
            Assert.Equal([string.Empty, "Design", "Review"], viewModel.WorkActivityFilterOptions);
            Assert.Equal(3, viewModel.CombinationCount);
            Assert.Equal(600, viewModel.TotalHourUnits);

            viewModel.SelectedProjectFilter = "P1001";
            Assert.Equal(2, viewModel.CombinationCount);
            Assert.Equal(300, viewModel.TotalHourUnits);
            Assert.Equal(2, viewModel.TotalEntryCount);
            Assert.Equal([string.Empty, "Pipe"], viewModel.DisciplineFilterOptions);
            Assert.Equal([string.Empty, "Design", "Review"], viewModel.WorkActivityFilterOptions);

            viewModel.SelectedWorkActivityFilter = "Review";
            Assert.Single(viewModel.Summaries);
            Assert.Equal(200, viewModel.TotalHourUnits);
            Assert.Equal("P1001", viewModel.Summaries[0].ProjectNumber);
            Assert.Equal("Review", viewModel.Summaries[0].WorkActivity);
            Assert.Equal([string.Empty, "P1001", "P2002"], viewModel.ProjectFilterOptions);
            Assert.Equal([string.Empty, "Pipe"], viewModel.DisciplineFilterOptions);

            viewModel.SelectedProjectFilter = string.Empty;
            Assert.Equal(2, viewModel.CombinationCount);
            Assert.Equal(500, viewModel.TotalHourUnits);

            viewModel.SelectedDisciplineFilter = "Pipe";
            Assert.Single(viewModel.Summaries);
            Assert.Equal(200, viewModel.TotalHourUnits);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Dimension_filters_reset_invalid_selection_and_missing_selection_after_refresh()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var repository = new WorkHourRepository(database);
            var date = DateTime.Today;
            var projectOne = CreateEntry(date, "P1001", "Pipe", "Design", 100);
            await repository.UpsertAsync(projectOne);
            await repository.UpsertAsync(CreateEntry(date, "P2002", "Electrical", "Review", 300));

            var viewModel = CreateViewModel(repository);
            await viewModel.EnsureLoadedAsync();

            viewModel.SelectedProjectFilter = "P1001";
            Assert.DoesNotContain("Electrical", viewModel.DisciplineFilterOptions);
            Assert.Single(viewModel.Summaries);

            await repository.DeleteAsync(projectOne.Id);
            viewModel.Invalidate();
            await viewModel.EnsureLoadedAsync();

            Assert.Equal(string.Empty, viewModel.SelectedProjectFilter);
            Assert.Single(viewModel.Summaries);
            Assert.Equal("P2002", viewModel.Summaries[0].ProjectNumber);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Dimension_filter_refresh_ignores_transient_null_selection_from_two_way_binding()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var database = new DatabaseService(Path.Combine(testRoot, "Kanban41.db"));
            await database.InitializeAsync();
            var repository = new WorkHourRepository(database);
            var date = DateTime.Today;
            await repository.UpsertAsync(CreateEntry(date, "P1001", "Pipe", "Design", 100));
            await repository.UpsertAsync(CreateEntry(date, "P2002", "Electrical", "Review", 300));

            var viewModel = CreateViewModel(repository);
            await viewModel.EnsureLoadedAsync();

            var simulateBindingReset = false;
            viewModel.PropertyChanged += (_, args) =>
            {
                if (!simulateBindingReset
                    || args.PropertyName is not (
                        nameof(WorkHourSummaryViewModel.ProjectFilterOptions)
                        or nameof(WorkHourSummaryViewModel.DisciplineFilterOptions)
                        or nameof(WorkHourSummaryViewModel.WorkActivityFilterOptions)))
                {
                    return;
                }

                viewModel.SelectedProjectFilter = null!;
                viewModel.SelectedDisciplineFilter = null!;
                viewModel.SelectedWorkActivityFilter = null!;
            };

            simulateBindingReset = true;
            viewModel.SelectedProjectFilter = "P1001";

            Assert.Equal("P1001", viewModel.SelectedProjectFilter);
            Assert.Single(viewModel.Summaries);
            Assert.Equal("P1001", viewModel.Summaries[0].ProjectNumber);
            Assert.Equal([string.Empty, "Pipe"], viewModel.DisciplineFilterOptions);
            Assert.Equal([string.Empty, "Design"], viewModel.WorkActivityFilterOptions);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static WorkHourSummaryViewModel CreateViewModel(WorkHourRepository repository)
    {
        return new WorkHourSummaryViewModel(repository, new WorkHourExportService(), _ => { });
    }

    private static WorkHourEntry CreateEntry(
        DateTime date,
        string projectNumber,
        string discipline,
        string workActivity,
        int hourUnits)
    {
        return new WorkHourEntry
        {
            WorkDate = date,
            ProjectNumber = projectNumber,
            Discipline = discipline,
            WorkActivity = workActivity,
            HourUnits = hourUnits,
            Remark = string.Empty,
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
