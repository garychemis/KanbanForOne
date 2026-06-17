using System.Reflection;

namespace KanbanForOne.Services;

public sealed record ReleaseNoteEntry(string Version, string Date, IReadOnlyList<string> Items);

public static class ReleaseNotesService
{
    public const string MetadataKey = "ReleaseNotes";

    public static IReadOnlyList<ReleaseNoteEntry> FromAssembly(Assembly assembly)
    {
        var source = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == MetadataKey)
            ?.Value;

        return Parse(source);
    }

    public static IReadOnlyList<ReleaseNoteEntry> Parse(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return [];
        }

        return source
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseLine)
            .Where(entry => entry is not null)
            .Cast<ReleaseNoteEntry>()
            .ToArray();
    }

    private static ReleaseNoteEntry? ParseLine(string line)
    {
        var parts = line.Split('|', 3, StringSplitOptions.TrimEntries);

        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return null;
        }

        var items = parts[2]
            .Split('；', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

        return new ReleaseNoteEntry(parts[0], parts[1], items);
    }
}
