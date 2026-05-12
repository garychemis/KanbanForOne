using KanbanForOne.Models;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Services;

public sealed class NoteRepository
{
    private readonly DatabaseService _database;

    public NoteRepository(DatabaseService database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<NoteItem>> GetAllAsync()
    {
        var notes = new List<NoteItem>();

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Title, Content, TagsJson, CreatedAt, UpdatedAt, IsArchived, SortOrder
            FROM Notes
            ORDER BY SortOrder, CreatedAt
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var note = new NoteItem
            {
                Id = Guid.Parse(reader.GetString(0)),
                Title = reader.GetString(1),
                Content = reader.GetString(2),
                CreatedAt = SqliteMapper.ReadDate(reader, 4),
                IsArchived = reader.GetInt32(6) == 1,
                SortOrder = reader.GetInt32(7)
            };

            foreach (var tag in SqliteMapper.TagsFromJson(reader.GetString(3)))
            {
                note.Tags.Add(tag);
            }

            note.UpdatedAt = SqliteMapper.ReadDate(reader, 5);
            notes.Add(note);
        }

        return notes;
    }

    public async Task UpsertAsync(NoteItem note)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Notes (
                Id, Title, Content, TagsJson, CreatedAt, UpdatedAt, IsArchived, SortOrder
            )
            VALUES (
                $id, $title, $content, $tagsJson, $createdAt, $updatedAt, $isArchived, $sortOrder
            )
            ON CONFLICT(Id) DO UPDATE SET
                Title = excluded.Title,
                Content = excluded.Content,
                TagsJson = excluded.TagsJson,
                UpdatedAt = excluded.UpdatedAt,
                IsArchived = excluded.IsArchived,
                SortOrder = excluded.SortOrder
            """;
        AddParameters(command, note);

        await command.ExecuteNonQueryAsync();
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
        command.Parameters.AddWithValue("$sortOrder", note.SortOrder);
    }
}
