using System.IO;
using KanbanForOne.Services;

namespace KanbanForOne.Tests;

public sealed class ThemePreferenceStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KanbanThemeTests", Guid.NewGuid().ToString("N"));
    private string PreferencePath => Path.Combine(_directory, "ui-settings.json");

    [Fact]
    public void New_installation_uses_light_theme()
    {
        Assert.Equal(AppTheme.Light, new ThemePreferenceStore(PreferencePath).Load());
    }

    [Theory]
    [InlineData("{broken json")]
    [InlineData("{\"theme\":\"Unknown\"}")]
    [InlineData("{\"theme\":42}")]
    [InlineData("null")]
    public void Invalid_preferences_fall_back_to_light(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(PreferencePath, json);
        Assert.Equal(AppTheme.Light, new ThemePreferenceStore(PreferencePath).Load());
    }

    [Fact]
    public void Last_selected_theme_survives_restarting_the_service()
    {
        var service = new ThemeService(new ThemePreferenceStore(PreferencePath));
        service.UseDarkThemeCommand.Execute(null);
        var restarted = new ThemeService(new ThemePreferenceStore(PreferencePath));
        Assert.True(restarted.IsDarkTheme);
        restarted.ToggleThemeCommand.Execute(null);
        Assert.Equal(AppTheme.Light, new ThemePreferenceStore(PreferencePath).Load());
        Assert.Single(Directory.GetFiles(_directory));
    }

    [Fact]
    public void Unwritable_preferences_do_not_prevent_the_current_theme_switch()
    {
        Directory.CreateDirectory(PreferencePath);
        var service = new ThemeService(new ThemePreferenceStore(PreferencePath));
        service.UseDarkThemeCommand.Execute(null);
        Assert.True(service.IsDarkTheme);
        Assert.Contains("无法保存", service.PreferenceMessage);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
