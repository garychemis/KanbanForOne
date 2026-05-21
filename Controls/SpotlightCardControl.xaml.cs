using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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
        if (IsPasteShortcut(e) && TryPasteClipboardImage())
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        RequestAnimatedClose();
        e.Handled = true;
    }

    private bool TryPasteClipboardImage()
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.IsSpotlightEditing)
        {
            return false;
        }

        if (!ClipboardContainsImage())
        {
            return false;
        }

        if (!viewModel.PasteClipboardImageCommand.CanExecute(null))
        {
            return false;
        }

        viewModel.PasteClipboardImageCommand.Execute(null);
        return true;
    }

    private static bool IsPasteShortcut(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
    }

    private static bool ClipboardContainsImage()
    {
        try
        {
            return Clipboard.ContainsImage();
        }
        catch
        {
            return false;
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true)
        {
            _isClosing = false;
            Backdrop.Opacity = 0;
            TaskShell.Opacity = 0;
            NoteShell.Opacity = 0;
            TaskDetailContent.Visibility = Visibility.Collapsed;
            NoteDetailContent.Visibility = Visibility.Collapsed;
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
        var shellTransform = PrepareShellTransform(shell);
        var fromTransform = ShellTransformState.FromRects(from, target);
        var toTransform = ShellTransformState.FromRects(to, target);

        UseTransitionShadow(shell);
        Canvas.SetLeft(shell, target.Left);
        Canvas.SetTop(shell, target.Top);
        shell.Width = target.Width;
        shell.Height = target.Height;
        shellTransform.Scale.ScaleX = fromTransform.ScaleX;
        shellTransform.Scale.ScaleY = fromTransform.ScaleY;
        shellTransform.Translate.X = fromTransform.TranslateX;
        shellTransform.Translate.Y = fromTransform.TranslateY;
        shell.Opacity = 1;
        Backdrop.Opacity = opening ? 0 : 1;
        detail.Opacity = opening ? 0 : 1;
        transform.Y = opening ? -8 : 0;
        detail.Visibility = Visibility.Collapsed;

        var storyboard = new Storyboard();
        AddAnimation(storyboard, shellTransform.Scale, nameof(ScaleTransform.ScaleX), fromTransform.ScaleX, toTransform.ScaleX, duration, easing);
        AddAnimation(storyboard, shellTransform.Scale, nameof(ScaleTransform.ScaleY), fromTransform.ScaleY, toTransform.ScaleY, duration, easing);
        AddAnimation(storyboard, shellTransform.Translate, nameof(TranslateTransform.X), fromTransform.TranslateX, toTransform.TranslateX, duration, easing);
        AddAnimation(storyboard, shellTransform.Translate, nameof(TranslateTransform.Y), fromTransform.TranslateY, toTransform.TranslateY, duration, easing);
        AddAnimation(storyboard, Backdrop, nameof(Opacity), opening ? 0 : 1, opening ? 1 : 0, duration, easing);

        storyboard.Completed += (_, _) =>
        {
            ClearTransitionAnimations(shell, Backdrop, detail, transform, shellTransform);
            RestoreShellShadow(shell);
            Canvas.SetLeft(shell, target.Left);
            Canvas.SetTop(shell, target.Top);
            shell.Width = target.Width;
            shell.Height = target.Height;
            ResetShellTransform(shellTransform);
            Backdrop.Opacity = opening ? 1 : 0;

            if (opening)
            {
                viewModel.CompleteSpotlightOpen();
                Dispatcher.BeginInvoke(() => FadeDetailIn(detail, transform), DispatcherPriority.Loaded);
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

    private sealed record ShellTransformState(double ScaleX, double ScaleY, double TranslateX, double TranslateY)
    {
        public static ShellTransformState FromRects(Rect visualRect, Rect layoutRect)
        {
            var width = Math.Max(1, layoutRect.Width);
            var height = Math.Max(1, layoutRect.Height);

            return new ShellTransformState(
                Math.Max(0.01, visualRect.Width / width),
                Math.Max(0.01, visualRect.Height / height),
                visualRect.Left - layoutRect.Left,
                visualRect.Top - layoutRect.Top);
        }
    }

    private sealed record ShellTransitionTransform(ScaleTransform Scale, TranslateTransform Translate);

    private static ShellTransitionTransform PrepareShellTransform(Border shell)
    {
        shell.RenderTransformOrigin = new Point(0, 0);

        if (shell.RenderTransform is TransformGroup group &&
            group.Children.Count >= 2 &&
            group.Children[0] is ScaleTransform scale &&
            group.Children[1] is TranslateTransform translate)
        {
            return new ShellTransitionTransform(scale, translate);
        }

        scale = new ScaleTransform(1, 1);
        translate = new TranslateTransform();
        shell.RenderTransform = new TransformGroup
        {
            Children =
            {
                scale,
                translate
            }
        };

        return new ShellTransitionTransform(scale, translate);
    }

    private static void ResetShellTransform(ShellTransitionTransform shellTransform)
    {
        shellTransform.Scale.ScaleX = 1;
        shellTransform.Scale.ScaleY = 1;
        shellTransform.Translate.X = 0;
        shellTransform.Translate.Y = 0;
    }

    private static void UseTransitionShadow(Border shell)
    {
        shell.Effect = new DropShadowEffect
        {
            BlurRadius = 12,
            ShadowDepth = 4,
            Direction = 270,
            Opacity = 0.08
        };
    }

    private static void RestoreShellShadow(Border shell)
    {
        shell.ClearValue(UIElement.EffectProperty);
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
        TranslateTransform transform,
        ShellTransitionTransform shellTransform)
    {
        shell.BeginAnimation(Canvas.LeftProperty, null);
        shell.BeginAnimation(Canvas.TopProperty, null);
        shell.BeginAnimation(FrameworkElement.WidthProperty, null);
        shell.BeginAnimation(FrameworkElement.HeightProperty, null);
        shell.BeginAnimation(UIElement.OpacityProperty, null);
        shellTransform.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        shellTransform.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        shellTransform.Translate.BeginAnimation(TranslateTransform.XProperty, null);
        shellTransform.Translate.BeginAnimation(TranslateTransform.YProperty, null);
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
