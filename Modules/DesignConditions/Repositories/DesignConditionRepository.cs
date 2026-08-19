using System.Globalization;
using KanbanForOne.Modules.DesignConditions.Data;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Services;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Modules.DesignConditions.Repositories;

public sealed class DesignConditionRepository
{
    private readonly DesignConditionDatabaseService _database;
    private readonly DesignConditionAttachmentRepository _attachments;

    public DesignConditionRepository(
        DesignConditionDatabaseService database,
        DesignConditionAttachmentRepository attachments)
    {
        _database = database;
        _attachments = attachments;
    }

    public async Task<IReadOnlyList<DesignConditionEntry>> GetAllAsync()
    {
        var entries = new List<DesignConditionEntry>();
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql + " ORDER BY IssuedDate DESC, UpdatedAt DESC, Id";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(ReadEntry(reader));
        }
        await AttachFilesAsync(entries);
        return entries;
    }

    public async Task<IReadOnlyList<DesignConditionEntry>> GetByIssuedDateAsync(DateTime date)
    {
        return await GetByIssuedDateRangeAsync(date.Date, date.Date);
    }

    public async Task<IReadOnlyList<DesignConditionEntry>> GetByIssuedDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate.Date > endDate.Date)
        {
            throw new ArgumentException("开始日期不能晚于结束日期。", nameof(startDate));
        }
        var entries = new List<DesignConditionEntry>();
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql + " WHERE IssuedDate >= $startDate AND IssuedDate <= $endDate ORDER BY IssuedDate, ProjectNumber COLLATE NOCASE, ConditionName COLLATE NOCASE";
        command.Parameters.AddWithValue("$startDate", DbDay(startDate));
        command.Parameters.AddWithValue("$endDate", DbDay(endDate));
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(ReadEntry(reader));
        }
        await AttachFilesAsync(entries);
        return entries;
    }

    public async Task<IReadOnlyList<DesignConditionCalendarSummary>> GetCalendarSummariesAsync(DateTime startDate, DateTime endDate)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT IssuedDate, COUNT(*), COALESCE(SUM(DrawingCount), 0) FROM DesignConditions WHERE IssuedDate >= $startDate AND IssuedDate <= $endDate GROUP BY IssuedDate ORDER BY IssuedDate";
        command.Parameters.AddWithValue("$startDate", DbDay(startDate));
        command.Parameters.AddWithValue("$endDate", DbDay(endDate));
        var result = new List<DesignConditionCalendarSummary>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(new DesignConditionCalendarSummary(DateTime.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture), reader.GetInt32(1), reader.GetInt32(2)));
        return result;
    }

    public async Task UpsertAsync(DesignConditionEntry entry)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await UpsertAsync(connection, null, entry);
    }

    public async Task SaveAggregateAsync(
        DesignConditionEntry entry,
        IEnumerable<Guid> deletedAttachmentIds,
        IEnumerable<DesignConditionAttachment> addedAttachments)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var sqliteTransaction = (SqliteTransaction)transaction;
        await UpsertAsync(connection, sqliteTransaction, entry);

        foreach (var id in deletedAttachmentIds)
        {
            await using var delete = connection.CreateCommand();
            delete.Transaction = sqliteTransaction;
            delete.CommandText = "DELETE FROM DesignConditionAttachments WHERE Id = $id";
            delete.Parameters.AddWithValue("$id", id.ToString());
            await delete.ExecuteNonQueryAsync();
        }

        foreach (var item in addedAttachments)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = sqliteTransaction;
            insert.CommandText = "INSERT INTO DesignConditionAttachments (Id, DesignConditionId, OriginalFileName, StoredFileName, RelativePath, FileExtension, FileSizeBytes, CreatedAt, SortOrder) VALUES ($id, $ownerId, $original, $stored, $path, $extension, $size, $createdAt, $sortOrder)";
            insert.Parameters.AddWithValue("$id", item.Id.ToString());
            insert.Parameters.AddWithValue("$ownerId", item.DesignConditionId.ToString());
            insert.Parameters.AddWithValue("$original", item.OriginalFileName);
            insert.Parameters.AddWithValue("$stored", item.StoredFileName);
            insert.Parameters.AddWithValue("$path", item.RelativePath);
            insert.Parameters.AddWithValue("$extension", item.FileExtension);
            insert.Parameters.AddWithValue("$size", item.FileSizeBytes);
            insert.Parameters.AddWithValue("$createdAt", SqliteMapper.DbDate(item.CreatedAt));
            insert.Parameters.AddWithValue("$sortOrder", item.SortOrder);
            await insert.ExecuteNonQueryAsync();
        }

        foreach (var option in new[]
                 {
                     (Type: "Discipline", Value: entry.IssuingDiscipline),
                     (Type: "Discipline", Value: entry.ReceivingDiscipline),
                     (Type: "Receiver", Value: entry.Receiver),
                     (Type: "DrawingSize", Value: entry.DrawingSize)
                 }.Where(item => !string.IsNullOrWhiteSpace(item.Value)))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = sqliteTransaction;
            command.CommandText = "INSERT OR IGNORE INTO DesignConditionOptions (Id, OptionType, Value, SortOrder, CreatedAt) VALUES ($id, $type, $value, 1000, $createdAt)";
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$type", option.Type);
            command.Parameters.AddWithValue("$value", option.Value.Trim());
            command.Parameters.AddWithValue("$createdAt", DateTime.Now.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task UpsertAsync(SqliteConnection connection, SqliteTransaction? transaction, DesignConditionEntry entry)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO DesignConditions (
                Id, ProjectNumber, IssuingDiscipline, ReceivingDiscipline, Receiver,
                IssuedDate, ConditionName, Revision, DrawingSize, DrawingCount,
                CreatedAt, UpdatedAt
            ) VALUES (
                $id, $projectNumber, $issuingDiscipline, $receivingDiscipline, $receiver,
                $issuedDate, $conditionName, $revision, $drawingSize, $drawingCount,
                $createdAt, $updatedAt
            )
            ON CONFLICT(Id) DO UPDATE SET
                ProjectNumber = excluded.ProjectNumber,
                IssuingDiscipline = excluded.IssuingDiscipline,
                ReceivingDiscipline = excluded.ReceivingDiscipline,
                Receiver = excluded.Receiver,
                IssuedDate = excluded.IssuedDate,
                ConditionName = excluded.ConditionName,
                Revision = excluded.Revision,
                DrawingSize = excluded.DrawingSize,
                DrawingCount = excluded.DrawingCount,
                UpdatedAt = excluded.UpdatedAt
            """;
        AddParameters(command, entry);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM DesignConditions WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private async Task AttachFilesAsync(IReadOnlyList<DesignConditionEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }
        var files = await _attachments.GetByConditionIdsAsync(entries.Select(item => item.Id));
        var lookup = files.ToLookup(file => file.DesignConditionId);
        foreach (var entry in entries)
        {
            foreach (var file in lookup[entry.Id])
            {
                entry.Attachments.Add(file);
            }
        }
    }

    private static void AddParameters(SqliteCommand command, DesignConditionEntry entry)
    {
        command.Parameters.AddWithValue("$id", entry.Id.ToString());
        command.Parameters.AddWithValue("$projectNumber", entry.ProjectNumber);
        command.Parameters.AddWithValue("$issuingDiscipline", entry.IssuingDiscipline);
        command.Parameters.AddWithValue("$receivingDiscipline", entry.ReceivingDiscipline);
        command.Parameters.AddWithValue("$receiver", entry.Receiver);
        command.Parameters.AddWithValue("$issuedDate", DbDay(entry.IssuedDate));
        command.Parameters.AddWithValue("$conditionName", entry.ConditionName);
        command.Parameters.AddWithValue("$revision", entry.Revision);
        command.Parameters.AddWithValue("$drawingSize", entry.DrawingSize);
        command.Parameters.AddWithValue("$drawingCount", entry.DrawingCount);
        command.Parameters.AddWithValue("$createdAt", SqliteMapper.DbDate(entry.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", SqliteMapper.DbDate(entry.UpdatedAt));
    }

    private static DesignConditionEntry ReadEntry(SqliteDataReader reader)
    {
        return new DesignConditionEntry
        {
            Id = Guid.Parse(reader.GetString(0)),
            ProjectNumber = reader.GetString(1),
            IssuingDiscipline = reader.GetString(2),
            ReceivingDiscipline = reader.GetString(3),
            Receiver = reader.GetString(4),
            IssuedDate = DateTime.ParseExact(reader.GetString(5), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            ConditionName = reader.GetString(6),
            Revision = reader.GetString(7),
            DrawingSize = reader.GetString(8),
            DrawingCount = reader.GetInt32(9),
            CreatedAt = SqliteMapper.ReadDate(reader, 10),
            UpdatedAt = SqliteMapper.ReadDate(reader, 11)
        };
    }

    private static string DbDay(DateTime date) => date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private const string SelectSql =
        "SELECT Id, ProjectNumber, IssuingDiscipline, ReceivingDiscipline, Receiver, IssuedDate, ConditionName, Revision, DrawingSize, DrawingCount, CreatedAt, UpdatedAt FROM DesignConditions";
}
