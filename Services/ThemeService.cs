using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Services;

/// <summary>
/// Keeps brush identities stable so converter results and rendered Markdown update
/// alongside XAML when the theme changes, without rebuilding the workspace.
/// </summary>
public sealed class ThemeService : ObservableObject
{
    private readonly ThemePreferenceStore _preferences;
    private Application? _application;
    private ResourceDictionary? _lightPalette;
    private ResourceDictionary? _darkPalette;
    private readonly Dictionary<object, ThemeColor> _brushColors = new();
    private AppTheme _currentTheme;
    private string _preferenceMessage = "主题会自动保存，下次启动时继续使用。";

    public ThemeService() : this(new ThemePreferenceStore(Path.Combine(AppPaths.DataRoot, "ui-settings.json"))) { }

    public ThemeService(ThemePreferenceStore preferences)
    {
        _preferences = preferences;
        _currentTheme = preferences.Load();
        ToggleThemeCommand = new RelayCommand(() => SetTheme(IsDarkTheme ? AppTheme.Light : AppTheme.Dark));
        UseLightThemeCommand = new RelayCommand(() => SetTheme(AppTheme.Light));
        UseDarkThemeCommand = new RelayCommand(() => SetTheme(AppTheme.Dark));
    }

    public AppTheme CurrentTheme => _currentTheme;
    public bool IsDarkTheme => CurrentTheme == AppTheme.Dark;
    public bool IsLightTheme => !IsDarkTheme;
    public string CurrentThemeName => IsDarkTheme ? "暗色" : "亮色";
    public string ToggleThemeLabel => IsDarkTheme ? "切换到亮色主题" : "切换到暗色主题";
    public string ThemeIcon => IsDarkTheme ? "\uE706" : "\uE708";
    public string PreferenceMessage => _preferenceMessage;
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand UseLightThemeCommand { get; }
    public RelayCommand UseDarkThemeCommand { get; }

    public void Initialize(Application application)
    {
        application.Dispatcher.VerifyAccess();
        if (ReferenceEquals(_application, application)) return;
        _application = application;
        _lightPalette = LoadPalette("Light");
        _darkPalette = LoadPalette("Dark");

        // A Color binding prevents WPF styles/controls from freezing a shared brush.
        // Keep the source alive so converter results and Markdown retain the same brush.
        _brushColors.Clear();
        foreach (var key in _lightPalette.Keys)
        {
            var value = _lightPalette[key];
            if (value is SolidColorBrush brush)
            {
                var color = new ThemeColor(brush.Color);
                _brushColors.Add(key, color);
                var sharedBrush = new SolidColorBrush();
                BindingOperations.SetBinding(sharedBrush, SolidColorBrush.ColorProperty,
                    new Binding(nameof(ThemeColor.Color)) { Source = color, Mode = BindingMode.OneWay });
                application.Resources[key] = sharedBrush;
            }
            else
            {
                application.Resources[key] = value;
            }
        }
        SetTheme(_currentTheme, persist: false);
    }

    public void SetTheme(AppTheme theme, bool persist = true)
    {
        if (!Enum.IsDefined(theme)) throw new ArgumentOutOfRangeException(nameof(theme));
        if (_application is not null)
        {
            _application.Dispatcher.VerifyAccess();
            var palette = theme == AppTheme.Dark ? _darkPalette! : _lightPalette!;
            foreach (var key in palette.Keys)
            {
                if (palette[key] is SolidColorBrush source && _brushColors.TryGetValue(key, out var color))
                    color.Color = source.Color;
                else
                    _application.Resources[key] = palette[key];
            }
        }

        _currentTheme = theme;
        if (persist)
        {
            try
            {
                _preferences.Save(theme);
                _preferenceMessage = "主题会自动保存，下次启动时继续使用。";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _preferenceMessage = "主题已切换，但无法保存偏好。请检查数据目录的写入权限。";
            }
        }
        OnPropertyChanged(nameof(CurrentTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(CurrentThemeName));
        OnPropertyChanged(nameof(ToggleThemeLabel));
        OnPropertyChanged(nameof(ThemeIcon));
        OnPropertyChanged(nameof(PreferenceMessage));
    }

    public static Brush GetBrush(string key, string fallbackHex)
    {
        return Application.Current?.TryFindResource(key) as Brush
            ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));
    }

    private static ResourceDictionary LoadPalette(string name) => new()
    {
        Source = new Uri($"/KanbanForOne;component/Styles/{name}Palette.xaml", UriKind.RelativeOrAbsolute)
    };

    private sealed class ThemeColor(Color color) : ObservableObject
    {
        private Color _color = color;
        public Color Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }
    }
}
