using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace KanbanForOne.Controls;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmText, string cancelText)
    {
        InitializeComponent();

        Title = title;
        DialogTitleText.Text = title;
        DialogMessageText.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;

        Loaded += (_, _) =>
        {
            PlayOpenAnimation();
            CancelButton.Focus();
        };
    }

    public static bool Show(
        Window? owner,
        string title,
        string message,
        string confirmText = "确定",
        string cancelText = "取消")
    {
        var dialog = new ConfirmDialog(title, message, confirmText, cancelText);

        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        CloseWithResult(true);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        CloseWithResult(false);
    }

    private void OnDialogKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseWithResult(false);
            e.Handled = true;
        }
    }

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // DragMove can throw if the mouse state changes during the drag handoff.
        }
    }

    private void CloseWithResult(bool result)
    {
        DialogResult = result;
        Close();
    }

    private void PlayOpenAnimation()
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(150);

        DialogChrome.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0, 1, duration)
            {
                EasingFunction = easing
            });

        if (DialogChrome.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        var scaleAnimation = new DoubleAnimation(0.96, 1, duration)
        {
            EasingFunction = easing
        };

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
    }
}
