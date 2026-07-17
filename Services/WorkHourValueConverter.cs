using System.Globalization;

namespace KanbanForOne.Services;

public static class WorkHourValueConverter
{
    public const int UnitsPerHour = 100;
    public const int MaximumEntryUnits = 24 * UnitsPerHour;

    public static string NormalizeProjectNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
    }

    public static bool TryParseHours(string? value, out int units)
    {
        units = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var hours) ||
            hours <= 0 ||
            hours > 24 ||
            hours != decimal.Round(hours, 2, MidpointRounding.AwayFromZero))
        {
            return false;
        }

        units = decimal.ToInt32(hours * UnitsPerHour);
        return units is > 0 and <= MaximumEntryUnits;
    }

    public static decimal FromUnits(int units)
    {
        return units / (decimal)UnitsPerHour;
    }

    public static string FormatHours(int units)
    {
        return FromUnits(units).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
