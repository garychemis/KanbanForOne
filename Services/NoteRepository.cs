using KanbanForOne.Models;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Services;

public sealed class NoteRepository
{
    private readonly DatabaseService _database;
    private const string UpsertCommandText =
        """
        INSERT INTO Notes (
            Id, Title, Content, TagsJson, CreatedAt, UpdatedAt, IsArchived, ArchiveSectionId, ArchivedAt, SortOrder
        )
        VALUES (
            $id, $title, $content, $tagsJson, $createdAt, $updatedAt, $isArchived, $archiveSectionId, $archivedAt, $sortOrder
        )
        ON CONFLICT(Id) DO UPDATE SET
            Title = excluded.Title,
            Content = excluded.Content,
            TagsJson = excluded.TagsJson,
            UpdatedAt = excluded.UpdatedAt,
            IsArchived = excluded.IsArchived,
            ArchiveSectionId = excluded.ArchiveSectionId,
            ArchivedAt = excluded.ArchivedAt,
            SortOrder = excluded.SortOrder
        """;

    public NoteRepository(DatabaseService database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<NoteItem>> GetAllAsync()
    {
        return await QueryAsync(null, null);
    }

    public async Task<IReadOnlyList<NoteItem>> GetActiveAsync()
    {
        return await QueryAsync("IsArchived = 0", null);
    }

    public async Task<IReadOnlyList<NoteItem>> GetArchivedBySectionAsync(Guid archiveSectionId)
    {
        return await QueryAsync(
            "IsArchived = 1 AND COALESCE(ArchiveSectionId, $defaultArchiveSectionId) = $archiveSectionId",
            command =>
            {
                command.Parameters.AddWithValue("$archiveSectionId", archiveSectionId.ToString());
                command.Parameters.AddWithValue("$defaultArchiveSectionId", ArchiveSection.DefaultId.ToString());
            });
    }

    private async Task<IReadOnlyList<NoteItem>> QueryAsync(
        string? whereClause,
        Action<SqliteCommand>? configureCommand)
    {
        var notes = new List<NoteItem>();

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        var whereSql = string.IsNullOrWhiteSpace(whereClause)
            ? string.Empty
            : $"WHERE {whereClause}";
        command.CommandText =
            """
            SELECT Id, Title, Content, TagsJson, CreatedAt, UpdatedAt, IsArchived, ArchiveSectionId, ArchivedAt, SortOrder
            FROM Notes
            {0}
            ORDER BY SortOrder, CreatedAt
            """.Replace("{0}", whereSql, StringComparison.Ordinal);
        configureCommand?.Invoke(command);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            notes.Add(ReadNote(reader));
        }

        return notes;
    }

    public async Task UpsertAsync(NoteItem note)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = UpsertCommandText;
        AddParameters(command, note);

        await command.ExecuteNonQueryAsync();
    }

    public async Task UpsertRangeAsync(IEnumerable<NoteItem> notes)
    {
        var items = notes.ToArray();

        if (items.Length == 0)
        {
            return;
        }

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var note in items)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = UpsertCommandText;
                AddParameters(command, note);
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
        command.CommandText = "DELETE FROM Notes WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());

        await command.ExecuteNonQueryAsync();
    }

    private static void AddParameters(SqliteCommand command, NoteItem note)
    {
        command.Parameters.AddWithValue("$id", note.Id.ToString());
        command.Parameters.AddWithValue("$title", note.Title);
        command.Parameters.AddWithValue("$content", note.Content);
        command.Parameters.AddWithValue("$tagsJson", SqliteMapper.TagsToJson(note.Tags));
        command.Parameters.AddWithValue("$createdAt", SqliteMapper.DbDate(note.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", SqliteMapper.DbDate(note.UpdatedAt));
        command.Parameters.AddWithValue("$isArchived", note.IsArchived ? 1 : 0);
        command.Parameters.AddWithValue("$archiveSectionId", note.ArchiveSectionId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$archivedAt", SqliteMapper.DbNullableDate(note.ArchivedAt));
        command.Parameters.AddWithValue("$sortOrder", note.SortOrder);
    }

    private static NoteItem ReadNote(SqliteDataReader reader)
    {
        var note = new NoteItem
        {
            Id = Guid.Parse(reader.GetString(0)),
            Title = reader.GetString(1),
            Content = reader.GetString(2),
            CreatedAt = SqliteMapper.ReadDate(reader, 4),
            IsArchived = reader.GetInt32(6) == 1,
            ArchiveSectionId = ReadNullableGuid(reader, 7),
            ArchivedAt = SqliteMapper.ReadNullableDate(reader, 8),
            SortOrder = reader.GetInt32(9)
        };

        foreach (var tag in SqliteMapper.TagsFromJson(reader.GetString(3)))
        {
            note.Tags.Add(tag);
        }

        note.UpdatedAt = SqliteMapper.ReadDate(reader, 5);
        return note;
    }

    private static Guid? ReadNullableGuid(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : Guid.Parse(reader.GetString(ordinal));
    }
}
