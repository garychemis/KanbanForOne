using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KanbanForOne.ViewModels;

namespace KanbanForOne.Controls;

public partial class SpotlightCardControl : UserControl
{
    private bool _isClosing;

    public SpotlightCardControl()
    {
        InitializeComponent();
    }

    private void OnOverlayMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, OverlaySurface) &&
            !ReferenceEquals(e.OriginalSource, Backdrop))
        {
            return;
        }

        RequestAnimatedClose();
        e.Handled = true;
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        RequestAnimatedClose();
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        RequestAnimatedClose();
        e.Handled = true;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true)
        {
            _isClosing = false;
            Backdrop.Opacity = 0;
            TaskShell.Opacity = 0;
            NoteShell.Opacity = 0;
            return;
        }

        _isClosing = false;
        Dispatcher.BeginInvoke(() =>
        {
            Focus();
            StartPopupTransition(opening: true);
        }, DispatcherPriority.DataBind);
    }

    private void OnEditorIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true || sender is not TextBox editor)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            editor.Focus();
            editor.CaretIndex = editor.Text.Length;
        }, DispatcherPriority.Input);
    }

    private void RequestAnimatedClose()
    {
        if (_isClosing)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            ExecuteClose();
            return;
        }

        if (!viewModel.ConfirmSpotlightClose())
        {
            return;
        }

        _isClosing = true;
        StartPopupTransition(opening: false, viewModel.CompleteSpotlightClose);
    }

    private void StartPopupTransition(bool opening, Action? completed = null)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            completed?.Invoke();
            return;
        }

        var shell = ActiveShell(viewModel);
        var detail = ActiveDetail(viewModel);
        var transform = ActiveDetailTransform(viewModel);

        if (shell is null || detail is null || transform is null)
        {
            completed?.Invoke();
            return;
        }

        var source = new Rect(
            viewModel.SpotlightSourceLeft,
            viewModel.SpotlightSourceTop,
            Math.Max(80, viewModel.SpotlightSourceWidth),
            Math.Max(72, viewModel.SpotlightSourceHeight));
        var target = new Rect(
            viewModel.SpotlightLeft,
            viewModel.SpotlightTop,
            viewModel.SpotlightWidth,
            viewModel.SpotlightHeight);
        var from = opening ? source : target;
        var to = opening ? target : source;
        var duration = new Duration(TimeSpan.FromMilliseconds(opening ? 250 : 180));
        var easing = new CubicEase
        {
            EasingMode = opening ? EasingMode.EaseOut : EasingMode.EaseIn
        };

        Canvas.SetLeft(shell, from.Left);
        Canvas.SetTop(shell, from.Top);
        shell.Width = from.Width;
        shell.Height = from.Height;
        shell.Opacity = 1;
        Backdrop.Opacity = opening ? 0 : 1;
        detail.Opacity = opening ? 0 : 1;
        transform.Y = opening ? -8 : 0;
        detail.Visibility = Visibility.Collapsed;

        var storyboard = new Storyboard();
        AddAnimation(storyboard, shell, "(Canvas.Left)", from.Left, to.Left, duration, easing);
        AddAnimation(storyboard, shell, "(Canvas.Top)", from.Top, to.Top, duration, easing);
        AddAnimation(storyboard, shell, nameof(Width), from.Width, to.Width, duration, easing);
        AddAnimation(storyboard, shell, nameof(Height), from.Height, to.Height, duration, easing);
        AddAnimation(storyboard, Backdrop, nameof(Opacity), opening ? 0 : 1, opening ? 1 : 0, duration, easing);

        storyboard.Completed += (_, _) =>
        {
            ClearTransitionAnimations(shell, Backdrop, detail, transform);
            Canvas.SetLeft(shell, to.Left);
            Canvas.SetTop(shell, to.Top);
            shell.Width = to.Width;
            shell.Height = to.Height;
            Backdrop.Opacity = opening ? 1 : 0;

            if (opening)
            {
                FadeDetailIn(detail, transform);
                completed?.Invoke();
                return;
            }

            detail.Opacity = 0;
            transform.Y = -8;
            detail.Visibility = Visibility.Collapsed;
            completed?.Invoke();
        };

        storyboard.Begin();
    }

    private Border? ActiveShell(MainWindowViewModel viewModel)
    {
        return viewModel.IsTaskSpotlightOpen
            ? TaskShell
            : viewModel.IsNoteSpotlightOpen
                ? NoteShell
                : null;
    }

    private FrameworkElement? ActiveDetail(MainWindowViewModel viewModel)
    {
        return viewModel.IsTaskSpotlightOpen
            ? TaskDetailContent
            : viewModel.IsNoteSpotlightOpen
                ? NoteDetailContent
                : null;
    }

    private TranslateTransform? ActiveDetailTransform(MainWindowViewModel viewModel)
    {
        return viewModel.IsTaskSpotlightOpen
            ? TaskDetailTransform
            : viewModel.IsNoteSpotlightOpen
                ? NoteDetailTransform
                : null;
    }

    private static void FadeDetailIn(FrameworkElement detail, TranslateTransform transform)
    {
        var duration = new Duration(TimeSpan.FromMilliseconds(120));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        detail.Visibility = Visibility.Visible;
        detail.Opacity = 0;
        transform.Y = -8;

        var storyboard = new Storyboard();
        AddAnimation(storyboard, detail, nameof(Opacity), 0, 1, duration, easing);
        AddAnimation(storyboard, transform, nameof(TranslateTransform.Y), -8, 0, duration, easing);
        storyboard.Completed += (_, _) =>
        {
            detail.BeginAnimation(UIElement.OpacityProperty, null);
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            detail.Opacity = 1;
            transform.Y = 0;
        };
        storyboard.Begin();
    }

    private static void ClearTransitionAnimations(
        Border shell,
        FrameworkElement backdrop,
        FrameworkElement detail,
        TranslateTransform transform)
    {
        shell.BeginAnimation(Canvas.LeftProperty, null);
        shell.BeginAnimation(Canvas.TopProperty, null);
        shell.BeginAnimation(FrameworkElement.WidthProperty, null);
        shell.BeginAnimation(FrameworkElement.HeightProperty, null);
        shell.BeginAnimation(UIElement.OpacityProperty, null);
        backdrop.BeginAnimation(UIElement.OpacityProperty, null);
        detail.BeginAnimation(UIElement.OpacityProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private static void AddAnimation(
        Storyboard storyboard,
        DependencyObject target,
        string propertyPath,
        double from,
        double to,
        Duration duration,
        IEasingFunction easing,
        double delayMilliseconds = 0)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds),
            Duration = duration,
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(propertyPath));
        storyboard.Children.Add(animation);
    }

    private void ExecuteClose()
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            !viewModel.CloseSpotlightCommand.CanExecute(null))
        {
            return;
        }

        viewModel.CloseSpotlightCommand.Execute(null);
    }
}
