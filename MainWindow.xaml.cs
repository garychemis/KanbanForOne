using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
            StateChanged += OnWindowStateChanged;
            WindowRoot.SizeChanged += OnWindowRootSizeChanged;
            SourceInitialized += OnSourceInitialized;
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

    }
}
