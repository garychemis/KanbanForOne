using KanbanForOne.Models;
using Microsoft.Data.Sqlite;
using TaskStatus = KanbanForOne.Models.TaskStatus;

namespace KanbanForOne.Services;

public sealed class TaskRepository
{
    private readonly DatabaseService _database;
    private const string UpsertCommandText =
        """
        INSERT INTO Tasks (
            Id, Title, Description, Status, Priority, TagsJson, StartDate, EndDate,
            CreatedAt, UpdatedAt, CompletedAt, IsArchived, ArchiveSectionId, ArchivedAt, SortOrder
        )
        VALUES (
            $id, $title, $description, $status, $priority, $tagsJson, $startDate, $endDate,
            $createdAt, $updatedAt, $completedAt, $isArchived, $archiveSectionId, $archivedAt, $sortOrder
        )
        ON CONFLICT(Id) DO UPDATE SET
            Title = excluded.Title,
            Description = excluded.Description,
            Status = excluded.Status,
            Priority = excluded.Priority,
            TagsJson = excluded.TagsJson,
            StartDate = excluded.StartDate,
            EndDate = excluded.EndDate,
            UpdatedAt = excluded.UpdatedAt,
            CompletedAt = excluded.CompletedAt,
            IsArchived = excluded.IsArchived,
            ArchiveSectionId = excluded.ArchiveSectionId,
            ArchivedAt = excluded.ArchivedAt,
            SortOrder = excluded.SortOrder
        """;

    public TaskRepository(DatabaseService database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync()
    {
        return await QueryAsync(null, null);
    }

    public async Task<IReadOnlyList<TaskItem>> GetActiveAsync()
    {
        return await QueryAsync("IsArchived = 0", null);
    }

    public async Task<IReadOnlyList<TaskItem>> GetArchivedBySectionAsync(Guid archiveSectionId)
    {
        return await QueryAsync(
            "IsArchived = 1 AND COALESCE(ArchiveSectionId, $defaultArchiveSectionId) = $archiveSectionId",
            command =>
            {
                command.Parameters.AddWithValue("$archiveSectionId", archiveSectionId.ToString());
                command.Parameters.AddWithValue("$defaultArchiveSectionId", ArchiveSection.DefaultId.ToString());
            });
    }

    private async Task<IReadOnlyList<TaskItem>> QueryAsync(
        string? whereClause,
        Action<SqliteCommand>? configureCommand)
    {
        var tasks = new List<TaskItem>();

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        var whereSql = string.IsNullOrWhiteSpace(whereClause)
            ? string.Empty
            : $"WHERE {whereClause}";
        command.CommandText =
            """
            SELECT Id, Title, Description, Status, Priority, TagsJson, StartDate, EndDate,
                   CreatedAt, UpdatedAt, CompletedAt, IsArchived, ArchiveSectionId, ArchivedAt, SortOrder
            FROM Tasks
            {0}
            ORDER BY SortOrder, CreatedAt
            """.Replace("{0}", whereSql, StringComparison.Ordinal);
        configureCommand?.Invoke(command);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tasks.Add(ReadTask(reader));
        }

        return tasks;
    }

    public async Task UpsertAsync(TaskItem task)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = UpsertCommandText;
        AddParameters(command, task);

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpsertRangeAsync(IEnumerable<TaskItem> tasks)
    {
        var items = tasks.ToArray();

        if (items.Length == 0)
        {
            return;
        }

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var task in items)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = UpsertCommandText;
                AddParameters(command, task);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tasks WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());

        await command.ExecuteNonQueryAsync();
    }

    private static void AddParameters(SqliteCommand command, TaskItem task)
    {
        command.Parameters.AddWithValue("$id", task.Id.ToString());
        command.Parameters.AddWithValue("$title", task.Title);
        command.Parameters.AddWithValue("$description", task.Description);
        command.Parameters.AddWithValue("$status", task.Status.ToString());
        command.Parameters.AddWithValue("$priority", task.Priority.ToString());
        command.Parameters.AddWithValue("$tagsJson", SqliteMapper.TagsToJson(task.Tags));
        command.Parameters.AddWithValue("$startDate", SqliteMapper.DbNullableDate(task.StartDate));
        command.Parameters.AddWithValue("$endDate", SqliteMapper.DbNullableDate(task.EndDate));
        command.Parameters.AddWithValue("$createdAt", SqliteMapper.DbDate(task.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", SqliteMapper.DbDate(task.UpdatedAt));
        command.Parameters.AddWithValue("$completedAt", SqliteMapper.DbNullableDate(task.CompletedAt));
        command.Parameters.AddWithValue("$isArchived", task.IsArchived ? 1 : 0);
        command.Parameters.AddWithValue("$archiveSectionId", task.ArchiveSectionId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$archivedAt", SqliteMapper.DbNullableDate(task.ArchivedAt));
        command.Parameters.AddWithValue("$sortOrder", task.SortOrder);
    }

    private static TaskItem ReadTask(SqliteDataReader reader)
    {
        var task = new TaskItem
        {
            Id = Guid.Parse(reader.GetString(0)),
            Title = reader.GetString(1),
            Description = reader.GetString(2),
            Status = Enum.Parse<TaskStatus>(reader.GetString(3)),
            Priority = Enum.Parse<TaskPriority>(reader.GetString(4)),
            StartDate = SqliteMapper.ReadNullableDate(reader, 6),
            EndDate = SqliteMapper.ReadNullableDate(reader, 7),
            CreatedAt = SqliteMapper.ReadDate(reader, 8),
            CompletedAt = SqliteMapper.ReadNullableDate(reader, 10),
            IsArchived = reader.GetInt32(11) == 1,
            ArchiveSectionId = ReadNullableGuid(reader, 12),
            ArchivedAt = SqliteMapper.ReadNullableDate(reader, 13),
            SortOrder = reader.GetInt32(14)
        };

        foreach (var tag in SqliteMapper.TagsFromJson(reader.GetString(5)))
        {
            task.Tags.Add(tag);
        }

        task.UpdatedAt = SqliteMapper.ReadDate(reader, 9);
        return task;
    }

    private static Guid? ReadNullableGuid(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : Guid.Parse(reader.GetString(ordinal));
    }
}
