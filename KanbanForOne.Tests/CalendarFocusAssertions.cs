using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace KanbanForOne.Tests;

/// <summary>
/// Runs against an open DatePicker calendar in UiSmokeTests' existing STA window.
/// Uses WPF keyboard focus and routed key events, without altering read-only focus properties.
/// </summary>
internal static class CalendarFocusAssertions
{
    private static readonly DateTime SeedDate = new(2026, 6, 15);

    public static void Verify(Calendar calendar, Action pump, Action<string>? captureState = null)
    {
        var originalDate = calendar.SelectedDate;
        var originalDisplayDate = calendar.DisplayDate;
        var originalMode = calendar.DisplayMode;
        try
        {
            VerifyMonthOrYear(calendar, CalendarMode.Year, pump, captureState);
            VerifyMonthOrYear(calendar, CalendarMode.Decade, pump, captureState);
            VerifyDay(calendar, pump, captureState);
        }
        finally
        {
            calendar.SelectedDate = originalDate;
            calendar.DisplayDate = originalDisplayDate;
            calendar.DisplayMode = originalMode;
            pump();
        }
    }

    private static void VerifyMonthOrYear(Calendar calendar, CalendarMode mode, Action pump, Action<string>? captureState)
    {
        Reset(calendar, mode, pump);
        var unselected = Descendants<CalendarButton>(calendar)
            .First(button => button.IsVisible && button.IsEnabled && !button.IsInactive && !button.HasSelectedDays);
        Focus(unselected, pump);
        Assert.False(unselected.HasSelectedDays, "Focusing an unselected month/year directly must not require its selected fill.");
        VerifyFocusVisual(unselected, "CalendarButtonFocusVisual", visible: true);

        // Calendar's normal directional-key handler can move HasSelectedDays along with
        // its displayed month/year. Verify the focus ring independently of that fill.
        Reset(calendar, mode, pump);
        var selected = Descendants<CalendarButton>(calendar)
            .Single(button => button.IsVisible && button.HasSelectedDays);
        Focus(selected, pump);
        VerifyFocusVisual(unselected, "CalendarButtonFocusVisual", visible: false);
        VerifyFocusVisual(selected, "CalendarButtonFocusVisual", visible: true);
        Assert.Same(Application.Current.FindResource("SelectedBackgroundBrush"), selected.Background);

        Press(selected, Key.Right, pump);
        var next = Assert.IsType<CalendarButton>(Keyboard.FocusedElement);
        Assert.NotSame(selected, next);
        Assert.True(next.IsKeyboardFocused);
        VerifyFocusVisual(selected, "CalendarButtonFocusVisual", visible: false);
        VerifyFocusVisual(next, "CalendarButtonFocusVisual", visible: true);
        captureState?.Invoke(mode == CalendarMode.Year ? "month" : "year");

        var nextDate = Assert.IsType<DateTime>(next.DataContext);
        Press(next, Key.Enter, pump);
        Assert.Equal(mode == CalendarMode.Decade ? CalendarMode.Year : CalendarMode.Month, calendar.DisplayMode);
        Assert.Equal(nextDate.Year, calendar.DisplayDate.Year);
        if (mode == CalendarMode.Year)
            Assert.Equal(nextDate.Month, calendar.DisplayDate.Month);
    }

    private static void VerifyDay(Calendar calendar, Action pump, Action<string>? captureState)
    {
        Reset(calendar, CalendarMode.Month, pump);
        var selected = Descendants<CalendarDayButton>(calendar)
            .Single(button => button.IsVisible && button.IsSelected);
        Focus(selected, pump);
        VerifyFocusVisual(selected, "CalendarDayButtonFocusVisual", visible: true);
        Press(selected, Key.Right, pump);
        var next = Assert.IsType<CalendarDayButton>(Keyboard.FocusedElement);
        Assert.NotSame(selected, next);
        Assert.Equal(SeedDate.AddDays(1), calendar.SelectedDate);
        VerifyFocusVisual(selected, "CalendarDayButtonFocusVisual", visible: false);
        VerifyFocusVisual(next, "CalendarDayButtonFocusVisual", visible: true);
        captureState?.Invoke("day");

        // Moving focus out of the date grid must remove its focus ring.
        var item = Descendants<CalendarItem>(calendar).Single();
        var header = (Button)item.Template.FindName("PART_HeaderButton", item);
        Focus(header, pump);
        VerifyFocusVisual(next, "CalendarDayButtonFocusVisual", visible: false);
    }

    private static void Reset(Calendar calendar, CalendarMode mode, Action pump)
    {
        calendar.SelectedDate = SeedDate;
        calendar.DisplayDate = SeedDate;
        calendar.DisplayMode = mode;
        pump();
        calendar.UpdateLayout();
    }

    private static void Focus(UIElement element, Action pump)
    {
        Keyboard.Focus(element);
        pump();
        Assert.True(element.IsKeyboardFocused, "The visible date button must acquire real WPF keyboard focus.");
    }

    private static void Press(UIElement target, Key key, Action pump)
    {
        var source = PresentationSource.FromVisual(target);
        Assert.NotNull(source);
        target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        });
        pump();
    }

    private static void VerifyFocusVisual(Control button, string name, bool visible)
    {
        var ring = Assert.IsType<Border>(button.Template.FindName(name, button));
        Assert.Equal(visible ? Visibility.Visible : Visibility.Collapsed, ring.Visibility);
        Assert.False(ring.IsHitTestVisible);
        Assert.Equal(new Thickness(2), ring.BorderThickness);
        var focusBrush = Assert.IsType<SolidColorBrush>(ring.BorderBrush);
        var selectedBrush = Assert.IsType<SolidColorBrush>(Application.Current.FindResource("SelectedBackgroundBrush"));
        Assert.NotEqual(selectedBrush.Color, focusBrush.Color);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T found) yield return found;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
