using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace KanbanForOne.Services;

internal static class SqliteMapper
{
    public static object DbDate(DateTime value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    public static object DbNullableDate(DateTime? value)
    {
        return value is null ? DBNull.Value : value.Value.ToString("O", CultureInfo.InvariantCulture);
    }

    public static DateTime ReadDate(SqliteDataReader reader, int ordinal)
    {
        return DateTime.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public static DateTime? ReadNullableDate(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : DateTime.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public static string TagsToJson(IEnumerable<string> tags)
    {
        return JsonSerializer.Serialize(tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray());
    }

    public static IReadOnlyList<string> TagsFromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
