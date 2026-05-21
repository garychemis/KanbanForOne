using KanbanForOne.Models;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Services;

public sealed class ArchiveSectionRepository
{
    private readonly DatabaseService _database;

    public ArchiveSectionRepository(DatabaseService database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<ArchiveSection>> GetAllAsync()
    {
        var sections = new List<ArchiveSection>();

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT s.Id, s.Name, s.SortOrder, s.CreatedAt, s.UpdatedAt, s.IsDefault,
                   (SELECT COUNT(*) FROM Tasks t WHERE t.IsArchived = 1 AND t.ArchiveSectionId = s.Id) AS TaskCount,
                   (SELECT COUNT(*) FROM Notes n WHERE n.IsArchived = 1 AND n.ArchiveSectionId = s.Id) AS NoteCount
            FROM ArchiveSections s
            ORDER BY s.SortOrder, s.CreatedAt
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sections.Add(ReadSection(reader));
        }

        return sections;
    }

    public async Task<ArchiveSection> GetDefaultAsync()
    {
        var sections = await GetAllAsync();
        return sections.FirstOrDefault(section => section.IsDefault)
            ?? sections.First(section => section.Id == ArchiveSection.DefaultId);
    }

    public async Task<ArchiveSection> GetOrCreateAsync(string name)
    {
        var normalizedName = NormalizeName(name);

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        var existing = await FindByNameAsync(connection, normalizedName);

        if (existing is not null)
        {
            return existing;
        }

        var now = DateTime.Now;
        var section = new ArchiveSection
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            SortOrder = await NextSortOrderAsync(connection),
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ArchiveSections (Id, Name, SortOrder, CreatedAt, UpdatedAt, IsDefault)
            VALUES ($id, $name, $sortOrder, $createdAt, $updatedAt, 0)
            """;
        AddParameters(command, section);
        await command.ExecuteNonQueryAsync();

        return section;
    }

    public async Task<bool> DeleteAndMoveContentsToDefaultAsync(Guid sectionId)
    {
        if (sectionId == ArchiveSection.DefaultId)
        {
            return false;
        }

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        try
        {
            var isDefault = await IsDefaultSectionAsync(connection, transaction, sectionId);

            if (isDefault is null)
            {
                await transaction.RollbackAsync();
                return false;
            }

            if (isDefault.Value)
            {
                await transaction.RollbackAsync();
                return false;
            }

            await MoveSectionContentAsync(connection, transaction, "Tasks", sectionId);
            await MoveSectionContentAsync(connection, transaction, "Notes", sectionId);

            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM ArchiveSections WHERE Id = $id AND IsDefault = 0";
            deleteCommand.Parameters.AddWithValue("$id", sectionId.ToString());
            var deletedCount = await deleteCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return deletedCount > 0;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static string NormalizeName(string name)
    {
        var normalizedName = name.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            normalizedName = "默认";
        }

        return normalizedName;
    }

    private static async Task<ArchiveSection?> FindByNameAsync(SqliteConnection connection, string name)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT s.Id, s.Name, s.SortOrder, s.CreatedAt, s.UpdatedAt, s.IsDefault,
                   (SELECT COUNT(*) FROM Tasks t WHERE t.IsArchived = 1 AND t.ArchiveSectionId = s.Id) AS TaskCount,
                   (SELECT COUNT(*) FROM Notes n WHERE n.IsArchived = 1 AND n.ArchiveSectionId = s.Id) AS NoteCount
            FROM ArchiveSections s
            WHERE s.Name = $name COLLATE NOCASE
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$name", name);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? ReadSection(reader)
            : null;
    }

    private static async Task<int> NextSortOrderAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM ArchiveSections";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<bool?> IsDefaultSectionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid sectionId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT IsDefault FROM ArchiveSections WHERE Id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", sectionId.ToString());

        var value = await command.ExecuteScalarAsync();

        return value is null || value == DBNull.Value
            ? null
            : Convert.ToInt32(value) == 1;
    }

    private static async Task MoveSectionContentAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        Guid sectionId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            UPDATE {tableName}
            SET ArchiveSectionId = $defaultSectionId
            WHERE IsArchived = 1 AND ArchiveSectionId = $sectionId
            """;
        command.Parameters.AddWithValue("$defaultSectionId", ArchiveSection.DefaultId.ToString());
        command.Parameters.AddWithValue("$sectionId", sectionId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static ArchiveSection ReadSection(SqliteDataReader reader)
    {
        return new ArchiveSection
        {
            Id = Guid.Parse(reader.GetString(0)),
            Name = reader.GetString(1),
            SortOrder = reader.GetInt32(2),
            CreatedAt = SqliteMapper.ReadDate(reader, 3),
            UpdatedAt = SqliteMapper.ReadDate(reader, 4),
            IsDefault = reader.GetInt32(5) == 1,
            TaskCount = reader.GetInt32(6),
            NoteCount = reader.GetInt32(7)
        };
    }

    private static void AddParameters(SqliteCommand command, ArchiveSection section)
    {
        command.Parameters.AddWithValue("$id", section.Id.ToString());
        command.Parameters.AddWithValue("$name", section.Name);
        command.Parameters.AddWithValue("$sortOrder", section.SortOrder);
        command.Parameters.AddWithValue("$createdAt", SqliteMapper.DbDate(section.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", SqliteMapper.DbDate(section.UpdatedAt));
    }
}
