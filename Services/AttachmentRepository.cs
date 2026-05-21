using KanbanForOne.Models;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Services;

public sealed class AttachmentRepository
{
    private readonly DatabaseService _database;

    public AttachmentRepository(DatabaseService database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<AttachmentItem>> GetAllAsync()
    {
        var attachments = new List<AttachmentItem>();

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, OwnerType, OwnerId, OriginalFileName, StoredFileName, RelativePath,
                   FileExtension, FileSizeBytes, CreatedAt, SortOrder
            FROM Attachments
            ORDER BY OwnerType, OwnerId, SortOrder, CreatedAt
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            attachments.Add(ReadAttachment(reader));
        }

        return attachments;
    }

    public async Task<IReadOnlyList<AttachmentItem>> GetByOwnerIdsAsync(
        AttachmentOwnerType ownerType,
        IEnumerable<Guid> ownerIds)
    {
        var ownerIdItems = ownerIds.Distinct().ToArray();

        if (ownerIdItems.Length == 0)
        {
            return [];
        }

        var attachments = new List<AttachmentItem>();

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        foreach (var chunk in ownerIdItems.Chunk(250))
        {
            await using var command = connection.CreateCommand();
            var parameterNames = new List<string>(chunk.Length);
            var index = 0;

            foreach (var ownerId in chunk)
            {
                var parameterName = $"$ownerId{index++}";
                parameterNames.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, ownerId.ToString());
            }

            command.CommandText =
                $"""
                SELECT Id, OwnerType, OwnerId, OriginalFileName, StoredFileName, RelativePath,
                       FileExtension, FileSizeBytes, CreatedAt, SortOrder
                FROM Attachments
                WHERE OwnerType = $ownerType
                  AND OwnerId IN ({string.Join(", ", parameterNames)})
                ORDER BY OwnerType, OwnerId, SortOrder, CreatedAt
                """;
            command.Parameters.AddWithValue("$ownerType", ownerType.ToString());

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                attachments.Add(ReadAttachment(reader));
            }
        }

        return attachments;
    }

    public async Task AddRangeAsync(IEnumerable<AttachmentItem> attachments)
    {
        var items = attachments.ToArray();

        if (items.Length == 0)
        {
            return;
        }

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var attachment in items)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT INTO Attachments (
                        Id, OwnerType, OwnerId, OriginalFileName, StoredFileName, RelativePath,
                        FileExtension, FileSizeBytes, CreatedAt, SortOrder
                    )
                    VALUES (
                        $id, $ownerType, $ownerId, $originalFileName, $storedFileName, $relativePath,
                        $fileExtension, $fileSizeBytes, $createdAt, $sortOrder
                    )
                    """;
                AddParameters(command, attachment);
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
        command.CommandText = "DELETE FROM Attachments WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteByOwnerAsync(AttachmentOwnerType ownerType, Guid ownerId)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Attachments WHERE OwnerType = $ownerType AND OwnerId = $ownerId";
        command.Parameters.AddWithValue("$ownerType", ownerType.ToString());
        command.Parameters.AddWithValue("$ownerId", ownerId.ToString());

        await command.ExecuteNonQueryAsync();
    }

    private static void AddParameters(SqliteCommand command, AttachmentItem attachment)
    {
        command.Parameters.AddWithValue("$id", attachment.Id.ToString());
        command.Parameters.AddWithValue("$ownerType", attachment.OwnerType.ToString());
        command.Parameters.AddWithValue("$ownerId", attachment.OwnerId.ToString());
        command.Parameters.AddWithValue("$originalFileName", attachment.OriginalFileName);
        command.Parameters.AddWithValue("$storedFileName", attachment.StoredFileName);
        command.Parameters.AddWithValue("$relativePath", attachment.RelativePath);
        command.Parameters.AddWithValue("$fileExtension", attachment.FileExtension);
        command.Parameters.AddWithValue("$fileSizeBytes", attachment.FileSizeBytes);
        command.Parameters.AddWithValue("$createdAt", SqliteMapper.DbDate(attachment.CreatedAt));
        command.Parameters.AddWithValue("$sortOrder", attachment.SortOrder);
    }

    private static AttachmentItem ReadAttachment(SqliteDataReader reader)
    {
        return new AttachmentItem
        {
            Id = Guid.Parse(reader.GetString(0)),
            OwnerType = Enum.Parse<AttachmentOwnerType>(reader.GetString(1)),
            OwnerId = Guid.Parse(reader.GetString(2)),
            OriginalFileName = reader.GetString(3),
            StoredFileName = reader.GetString(4),
            RelativePath = reader.GetString(5),
            FileExtension = reader.GetString(6),
            FileSizeBytes = reader.GetInt64(7),
            CreatedAt = SqliteMapper.ReadDate(reader, 8),
            SortOrder = reader.GetInt32(9)
        };
    }
}
