using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using KanbanForOne.ViewModels;

namespace KanbanForOne
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            Loaded += OnLoaded;
            SourceInitialized += OnSourceInitialized;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            await _viewModel.InitializeAsync();
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            ApplyRoundedWindowCorners();
        }

        private void OnWindowRootPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.GetPosition(this).Y > 64)
            {
                return;
            }

            if (IsInteractiveElement(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                ToggleWindowState();
                e.Handled = true;
                return;
            }

            RestoreMaximizedWindowForDrag(e.GetPosition(this));

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // DragMove can throw if the mouse state changes while WPF is starting a drag.
            }

            e.Handled = true;
        }

        private void RestoreMaximizedWindowForDrag(Point mousePosition)
        {
            if (WindowState != WindowState.Maximized)
            {
                return;
            }

            var screenPosition = PointToScreen(mousePosition);
            var horizontalRatio = ActualWidth <= 0 ? 0.5 : mousePosition.X / ActualWidth;
            var restoreWidth = RestoreBounds.Width > 0 ? RestoreBounds.Width : Width;

            WindowState = WindowState.Normal;
            Left = screenPosition.X - restoreWidth * horizontalRatio;
            Top = 0;
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private static bool IsInteractiveElement(DependencyObject? source)
        {
            while (source is not null)
            {
                if (source is ButtonBase
                    or TextBoxBase
                    or PasswordBox
                    or ComboBox
                    or DatePicker
                    or ScrollBar
                    or Slider)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void ApplyRoundedWindowCorners()
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                return;
            }

            try
            {
                var handle = new WindowInteropHelper(this).Handle;
                var preference = 2;
                _ = DwmSetWindowAttribute(handle, 33, ref preference, sizeof(int));
            }
            catch
            {
                // Rounded corners are a visual enhancement; the window must still open if DWM rejects it.
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attribute,
            ref int pvAttribute,
            int cbAttribute);
    }
}
