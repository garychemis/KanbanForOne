using KanbanForOne.Modules.DesignConditions.Data;

namespace KanbanForOne.Modules.DesignConditions.Repositories;

public sealed class DesignConditionOptionRepository
{
    private readonly DesignConditionDatabaseService _database;

    public DesignConditionOptionRepository(DesignConditionDatabaseService database)
    {
        _database = database;
    }

    public async Task<IReadOnlyList<string>> GetAsync(string type)
    {
        var values = new List<string>();
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM DesignConditionOptions WHERE OptionType = $type ORDER BY SortOrder, Value COLLATE NOCASE";
        command.Parameters.AddWithValue("$type", type);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }
        return values;
    }

    public async Task AddAsync(string type, string value)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            return;
        }
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO DesignConditionOptions (Id, OptionType, Value, SortOrder, CreatedAt) VALUES ($id, $type, $value, 1000, $createdAt)";
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$value", normalized);
        command.Parameters.AddWithValue("$createdAt", DateTime.Now.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }
}
