using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KanbanForOne;
using KanbanForOne.Services;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("save-dark" or "verify-dark" or "verify-pointer")) return 2;
        var expectedRoot = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var actualRoot = Path.GetFullPath(AppPaths.AppRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (!actualRoot.Equals(expectedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The probe must run through its isolated apphost executable, never dotnet.exe.");
        if (!File.Exists(Path.Combine(expectedRoot, ".isolated-theme-probe")))
            throw new InvalidOperationException("Missing isolated probe directory marker.");
        if (Mutex.TryOpenExisting("Local\\KanbanForOne.Application.Mutex", out var existing))
        {
            existing.Dispose();
            throw new InvalidOperationException("Close the running KanbanForOne instance before using this probe; it will never activate or modify it.");
        }

        var expectedInitialTheme = args[0] == "save-dark" ? AppTheme.Light : AppTheme.Dark;
        var outputPath = Path.GetFullPath(args[1]);
        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var initializedBeforeWindow = false;
        var startupTheme = AppTheme.Light;
        Exception? failure = null;
        var captured = false;

        app.Startup += (_, _) =>
        {
            var theme = (ThemeService)App.Services.GetService(typeof(ThemeService))!;
            startupTheme = theme.CurrentTheme;
            initializedBeforeWindow = app.MainWindow is null;
            if (startupTheme != expectedInitialTheme || !initializedBeforeWindow)
                throw new InvalidOperationException($"Theme was not initialized before the main window: {startupTheme}, window={app.MainWindow}.");
        };
        app.DispatcherUnhandledException += (_, e) =>
        {
            failure = e.Exception;
            e.Handled = true;
            app.Shutdown(1);
        };
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                var window = (MainWindow)sender;
                window.ContentRendered += (_, _) =>
                {
                    if (captured) return;
                    captured = true;
                    try
                    {
                        var theme = (ThemeService)App.Services.GetService(typeof(ThemeService))!;
                        var firstFrameBackground = ((SolidColorBrush)app.FindResource("AppBackgroundBrush")).Color;
                        var palette = new ResourceDictionary
                        {
                            Source = new Uri($"/KanbanForOne;component/Styles/{expectedInitialTheme}Palette.xaml", UriKind.Relative)
                        };
                        var expectedBackground = ((SolidColorBrush)palette["AppBackgroundBrush"]).Color;
                        if (firstFrameBackground != expectedBackground)
                            throw new InvalidOperationException($"First rendered frame used {firstFrameBackground}, expected {expectedBackground}.");
                        var frame = (Border)window.FindName("WindowFrame");
                        if (frame.Background is not SolidColorBrush actualWindowBackground || actualWindowBackground.Color != expectedBackground)
                            throw new InvalidOperationException("The main window did not use the initialized theme on its first rendered frame.");
                        var firstFrameWindowBackground = actualWindowBackground.Color;
                        if (args[0] == "save-dark") theme.SetTheme(AppTheme.Dark);
                        if (theme.CurrentTheme != AppTheme.Dark || new ThemePreferenceStore(Path.Combine(AppPaths.DataRoot, "ui-settings.json")).Load() != AppTheme.Dark)
                            throw new InvalidOperationException("The dark preference was not persisted.");

                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                        if (args[0] == "verify-pointer")
                        {
                            window.Hide();
                            PointerInteractionAssertions.Verify(outputPath);
                            app.Shutdown();
                            return;
                        }
                        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using (var imageStream = File.Create(Path.ChangeExtension(outputPath, ".png"))) encoder.Save(imageStream);
                        File.WriteAllText(outputPath, JsonSerializer.Serialize(new
                        {
                            mode = args[0], processId = Environment.ProcessId, startupTheme = startupTheme.ToString(),
                            initializedBeforeWindow, firstFrameBackground = firstFrameBackground.ToString(),
                            windowBackground = firstFrameWindowBackground.ToString(), persistedTheme = theme.CurrentTheme.ToString(),
                            isolatedAppRoot = AppPaths.AppRoot, windowType = window.GetType().FullName,
                            startupUri = app.StartupUri.OriginalString
                        }, new JsonSerializerOptions { WriteIndented = true }));
                        // Loaded handlers can still have pending database work; exit after
                        // the dispatcher has processed the current startup callbacks.
                        app.Dispatcher.BeginInvoke(() => app.Shutdown(), DispatcherPriority.ApplicationIdle);
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                        app.Shutdown(1);
                    }
                };
            }));
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        watchdog.Tick += (_, _) =>
        {
            failure = new TimeoutException("The main window did not render within 20 seconds.");
            app.Shutdown(1);
        };
        watchdog.Start();
        var result = app.Run();
        watchdog.Stop();
        if (failure is not null) Console.Error.WriteLine(failure);
        return captured && failure is null ? result : 1;
    }
}
