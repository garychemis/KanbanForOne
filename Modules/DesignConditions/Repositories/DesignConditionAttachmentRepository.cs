using KanbanForOne.Modules.DesignConditions.Data;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Services;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Modules.DesignConditions.Repositories;

public sealed class DesignConditionAttachmentRepository
{
    private readonly DesignConditionDatabaseService _database;

    public DesignConditionAttachmentRepository(DesignConditionDatabaseService database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<DesignConditionAttachment>> GetByConditionIdsAsync(IEnumerable<Guid> conditionIds)
    {
        var ids = conditionIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }
        var result = new List<DesignConditionAttachment>();
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        foreach (var chunk in ids.Chunk(250))
        {
            await using var command = connection.CreateCommand();
            var names = new List<string>();
            for (var i = 0; i < chunk.Length; i++)
            {
                var name = $"$id{i}";
                names.Add(name);
                command.Parameters.AddWithValue(name, chunk[i].ToString());
            }
            command.CommandText = $"SELECT Id, DesignConditionId, OriginalFileName, StoredFileName, RelativePath, FileExtension, FileSizeBytes, CreatedAt, SortOrder FROM DesignConditionAttachments WHERE DesignConditionId IN ({string.Join(",", names)}) ORDER BY DesignConditionId, SortOrder, CreatedAt";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(Read(reader));
            }
        }
        return result;
    }

    public async Task AddRangeAsync(IEnumerable<DesignConditionAttachment> attachments)
    {
        var items = attachments.ToArray();
        if (items.Length == 0)
        {
            return;
        }
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        foreach (var item in items)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                "INSERT INTO DesignConditionAttachments (Id, DesignConditionId, OriginalFileName, StoredFileName, RelativePath, FileExtension, FileSizeBytes, CreatedAt, SortOrder) VALUES ($id, $ownerId, $original, $stored, $path, $extension, $size, $createdAt, $sortOrder)";
            command.Parameters.AddWithValue("$id", item.Id.ToString());
            command.Parameters.AddWithValue("$ownerId", item.DesignConditionId.ToString());
            command.Parameters.AddWithValue("$original", item.OriginalFileName);
            command.Parameters.AddWithValue("$stored", item.StoredFileName);
            command.Parameters.AddWithValue("$path", item.RelativePath);
            command.Parameters.AddWithValue("$extension", item.FileExtension);
            command.Parameters.AddWithValue("$size", item.FileSizeBytes);
            command.Parameters.AddWithValue("$createdAt", SqliteMapper.DbDate(item.CreatedAt));
            command.Parameters.AddWithValue("$sortOrder", item.SortOrder);
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM DesignConditionAttachments WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static DesignConditionAttachment Read(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        DesignConditionId = Guid.Parse(reader.GetString(1)),
        OriginalFileName = reader.GetString(2),
        StoredFileName = reader.GetString(3),
        RelativePath = reader.GetString(4),
        FileExtension = reader.GetString(5),
        FileSizeBytes = reader.GetInt64(6),
        CreatedAt = SqliteMapper.ReadDate(reader, 7),
        SortOrder = reader.GetInt32(8)
    };
}
