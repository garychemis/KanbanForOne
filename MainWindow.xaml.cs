using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using KanbanForOne.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace KanbanForOne
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel = new();
        private Forms.NotifyIcon? _trayIcon;
        private Drawing.Icon? _trayIconImage;
        private bool _isExitRequested;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            Loaded += OnLoaded;
            StateChanged += OnWindowStateChanged;
            WindowRoot.SizeChanged += OnWindowRootSizeChanged;
            SourceInitialized += OnSourceInitialized;
            InitializeTrayIcon();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            await _viewModel.InitializeAsync();
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            ApplyWindowRootClip();
        }

        private void OnWindowRootSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyWindowRootClip();
            _viewModel.UpdateSpotlightLayout(e.NewSize);
        }

        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            ApplyWindowRootClip();
        }

        private void OnWindowRootPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.IsSpotlightOpen)
            {
                return;
            }

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

        private void ApplyWindowRootClip()
        {
            if (WindowRoot.ActualWidth <= 0 || WindowRoot.ActualHeight <= 0)
            {
                return;
            }

            if (WindowState == WindowState.Maximized)
            {
                WindowFrame.CornerRadius = new CornerRadius(0);
                WindowRoot.Clip = null;
                return;
            }

            WindowFrame.CornerRadius = new CornerRadius(28);
            var radius = WindowFrame.CornerRadius.TopLeft;
            WindowRoot.Clip = new RectangleGeometry(
                new Rect(0, 0, WindowRoot.ActualWidth, WindowRoot.ActualHeight),
                radius,
                radius);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExitRequested)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            DisposeTrayIcon();
            base.OnClosed(e);
        }

        private void InitializeTrayIcon()
        {
            var loadedIcon = LoadTrayIcon();
            _trayIconImage = (Drawing.Icon)(loadedIcon ?? Drawing.SystemIcons.Application).Clone();
            loadedIcon?.Dispose();

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开", null, (_, _) => RestoreFromTray());
            menu.Items.Add("退出", null, (_, _) => ExitFromTray());

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = _trayIconImage,
                Text = "Kanban41",
                Visible = true,
                ContextMenuStrip = menu
            };
            _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        }

        private static Drawing.Icon? LoadTrayIcon()
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "icon", "icon-transparent.ico");

            if (File.Exists(iconPath))
            {
                return new Drawing.Icon(iconPath);
            }

            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/icon/icon-transparent.ico"));

            if (resource is null)
            {
                return null;
            }

            using var stream = resource.Stream;
            return new Drawing.Icon(stream);
        }

        private void HideToTray()
        {
            ShowInTaskbar = false;
            Hide();
        }

        private void RestoreFromTray()
        {
            ShowInTaskbar = true;
            Show();

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
        }

        private void ExitFromTray()
        {
            _isExitRequested = true;

            if (_trayIcon is not null)
            {
                _trayIcon.Visible = false;
            }

            Close();
        }

        private void DisposeTrayIcon()
        {
            if (_trayIcon is not null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _trayIconImage?.Dispose();
            _trayIconImage = null;
        }

    }
}
