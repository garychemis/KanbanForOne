using System.IO;
using System.Text.Json;

namespace KanbanForOne.Services;

public enum AppTheme
{
    Light,
    Dark
}

/// <summary>Appearance preferences are separate from the workspace databases.</summary>
public sealed class ThemePreferenceStore(string path)
{
    public AppTheme Load()
    {
        try
        {
            if (!File.Exists(path)) return AppTheme.Light;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("theme", out var theme)
                && theme.ValueKind == JsonValueKind.String
                && theme.GetString() == "Dark"
                    ? AppTheme.Dark : AppTheme.Light;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return AppTheme.Light;
        }
    }

    public void Save(AppTheme theme)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".ui-settings-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new { theme = theme.ToString() }));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
