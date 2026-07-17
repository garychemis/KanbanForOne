using System.Globalization;
using KanbanForOne.Models;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Services;

public sealed class WorkHourRepository
{
    private readonly DatabaseService _database;

    public WorkHourRepository(DatabaseService database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<WorkHourEntry>> GetByDateAsync(DateTime date)
    {
        var entries = new List<WorkHourEntry>();
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, WorkDate, ProjectNumber, Discipline, WorkActivity, HourUnits, Remark, CreatedAt, UpdatedAt
            FROM WorkHourEntries
            WHERE WorkDate = $workDate
            ORDER BY CreatedAt, Id
            """;
        command.Parameters.AddWithValue("$workDate", DbDate(date));

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    public async Task<int> GetTotalUnitsByDateAsync(DateTime date, Guid? excludeId = null)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COALESCE(SUM(HourUnits), 0)
            FROM WorkHourEntries
            WHERE WorkDate = $workDate
              AND ($excludeId IS NULL OR Id <> $excludeId)
            """;
        command.Parameters.AddWithValue("$workDate", DbDate(date));
        command.Parameters.AddWithValue("$excludeId", excludeId?.ToString() ?? (object)DBNull.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<IReadOnlyList<WorkHourSummaryItem>> GetSummaryAsync(DateTime startDate, DateTime endDate)
    {
        ValidateDateRange(startDate, endDate);
        var summaries = new List<WorkHourSummaryItem>();
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT ProjectNumber, Discipline, WorkActivity, SUM(HourUnits), COUNT(*)
            FROM WorkHourEntries
            WHERE WorkDate >= $startDate AND WorkDate <= $endDate
            GROUP BY ProjectNumber, Discipline, WorkActivity
            ORDER BY SUM(HourUnits) DESC, ProjectNumber COLLATE NOCASE, Discipline COLLATE NOCASE, WorkActivity COLLATE NOCASE
            """;
        command.Parameters.AddWithValue("$startDate", DbDate(startDate));
        command.Parameters.AddWithValue("$endDate", DbDate(endDate));

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            summaries.Add(new WorkHourSummaryItem(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4)));
        }

        return summaries;
    }

    public async Task<IReadOnlyList<WorkHourEntry>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        ValidateDateRange(startDate, endDate);
        var entries = new List<WorkHourEntry>();
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, WorkDate, ProjectNumber, Discipline, WorkActivity, HourUnits, Remark, CreatedAt, UpdatedAt
            FROM WorkHourEntries
            WHERE WorkDate >= $startDate AND WorkDate <= $endDate
            ORDER BY WorkDate, CreatedAt, Id
            """;
        command.Parameters.AddWithValue("$startDate", DbDate(startDate));
        command.Parameters.AddWithValue("$endDate", DbDate(endDate));

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    public async Task UpsertAsync(WorkHourEntry entry)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO WorkHourEntries (
                Id, WorkDate, ProjectNumber, Discipline, WorkActivity, HourUnits, Remark, CreatedAt, UpdatedAt
            ) VALUES (
                $id, $workDate, $projectNumber, $discipline, $workActivity, $hourUnits, $remark, $createdAt, $updatedAt
            )
            ON CONFLICT(Id) DO UPDATE SET
                WorkDate = excluded.WorkDate,
                ProjectNumber = excluded.ProjectNumber,
                Discipline = excluded.Discipline,
                WorkActivity = excluded.WorkActivity,
                HourUnits = excluded.HourUnits,
                Remark = excluded.Remark,
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
        command.CommandText = "DELETE FROM WorkHourEntries WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static void AddParameters(SqliteCommand command, WorkHourEntry entry)
    {
        command.Parameters.AddWithValue("$id", entry.Id.ToString());
        command.Parameters.AddWithValue("$workDate", DbDate(entry.WorkDate));
        command.Parameters.AddWithValue("$projectNumber", entry.ProjectNumber);
        command.Parameters.AddWithValue("$discipline", entry.Discipline);
        command.Parameters.AddWithValue("$workActivity", entry.WorkActivity);
        command.Parameters.AddWithValue("$hourUnits", entry.HourUnits);
        command.Parameters.AddWithValue("$remark", entry.Remark);
        command.Parameters.AddWithValue("$createdAt", SqliteMapper.DbDate(entry.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", SqliteMapper.DbDate(entry.UpdatedAt));
    }

    private static WorkHourEntry ReadEntry(SqliteDataReader reader)
    {
        return new WorkHourEntry
        {
            Id = Guid.Parse(reader.GetString(0)),
            WorkDate = DateTime.ParseExact(reader.GetString(1), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            ProjectNumber = reader.GetString(2),
            Discipline = reader.GetString(3),
            WorkActivity = reader.GetString(4),
            HourUnits = reader.GetInt32(5),
            Remark = reader.GetString(6),
            CreatedAt = SqliteMapper.ReadDate(reader, 7),
            UpdatedAt = SqliteMapper.ReadDate(reader, 8)
        };
    }

    private static string DbDate(DateTime date) => date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static void ValidateDateRange(DateTime startDate, DateTime endDate)
    {
        if (startDate.Date > endDate.Date)
        {
            throw new ArgumentException("开始日期不能晚于结束日期。", nameof(startDate));
        }
    }
}
