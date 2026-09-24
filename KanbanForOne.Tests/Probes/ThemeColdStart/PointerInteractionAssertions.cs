using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using KanbanForOne.Controls;
using KanbanForOne.Models;
using KanbanForOne.ViewModels;
using TaskStatus = KanbanForOne.Models.TaskStatus;

/// <summary>
/// Opt-in desktop-session probe. Unlike CardHoverAssertions, this moves the OS
/// pointer and lets WPF update IsMouseOver through its real input manager.
/// All cards are synthetic; none are attached to a repository or saved.
/// </summary>
internal static class PointerInteractionAssertions
{
    private const string Details = "第一行详情\n第二行详情\n第三行详情\n第四行详情\n第五行不应出现";

    public static void Verify(string outputPath)
    {
        if (!Native.GetCursorPos(out var originalCursor)) throw new InvalidOperationException("No interactive desktop pointer is available.");
        var previousForeground = Native.GetForegroundWindow();
        var opened = 0;
        var task = new TaskItem
        {
            Title = "隔离指针验证任务", Description = Details, Status = TaskStatus.Done,
            Priority = TaskPriority.High, StartDate = DateTime.Today.AddDays(-3), EndDate = DateTime.Today.AddDays(-1)
        };
        task.Tags.Add("指针验证");
        task.Attachments.Add(new AttachmentItem { OriginalFileName = "验证附件.pdf" });
        var card = new TaskCardControl
        {
            Width = 320, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            DataContext = task, OpenCommand = new RelayCommand(_ => opened++)
        };
        var calendar = new CalendarView();
        var days = Enumerable.Range(0, 42).Select(index =>
        {
            var day = new CalendarDayItem { Date = new DateTime(2026, 9, 1).AddDays(index), IsCurrentMonth = true, TotalTaskCount = 3 };
            foreach (var number in Enumerable.Range(1, 3))
                day.VisibleTaskChips.Add(new CalendarTaskChip { Task = new TaskItem { Title = $"第 {number} 张完成任务", Description = Details, Status = TaskStatus.Done } });
            return day;
        }).ToArray();
        var panelFactory = new FrameworkElementFactory(typeof(CalendarMonthPanel));
        panelFactory.SetValue(CalendarMonthPanel.ViewportHeightProperty, 480d);
        var items = new ItemsControl
        {
            ItemsSource = days, ItemTemplate = (DataTemplate)calendar.Resources["CalendarDayTemplate"],
            ItemsPanel = new ItemsPanelTemplate(panelFactory)
        };
        var viewer = new ScrollViewer
        {
            Content = items, Height = 480, VerticalAlignment = VerticalAlignment.Top,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Visible
        };
        var root = new Grid { Margin = new Thickness(16), Background = (Brush)Application.Current.FindResource("AppBackgroundBrush") };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(130) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(480) });
        root.RowDefinitions.Add(new RowDefinition());
        root.Children.Add(card);
        var nativeInputs = new StackPanel { Width = 520, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        var datePicker = new DatePicker
        {
            Width = 240, Height = 32, HorizontalAlignment = HorizontalAlignment.Left,
            SelectedDate = new DateTime(2026, 9, 24), DisplayDate = new DateTime(2026, 9, 24),
            Style = (Style)Application.Current.FindResource("DrawerDatePickerStyle")
        };
        var textBox = new TextBox
        {
            Text = "隔离验证文本：全选不会修改剪贴板", Width = 420, Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0)
        };
        nativeInputs.Children.Add(datePicker);
        nativeInputs.Children.Add(textBox);
        root.Children.Add(nativeInputs);
        Grid.SetRow(viewer, 1);
        root.Children.Add(viewer);
        var window = new Window
        {
            Title = "KanbanForOne isolated pointer verification", Width = 1100, Height = 710,
            Left = SystemParameters.WorkArea.Left + 12, Top = SystemParameters.WorkArea.Top + 12,
            WindowStartupLocation = WindowStartupLocation.Manual, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false, Topmost = true, Content = new AdornerDecorator { Child = root }
        };
        var checks = new List<string>();
        try
        {
            window.Show();
            window.Activate();
            var probeWindowHandle = new WindowInteropHelper(window).Handle;
            Native.SetForegroundWindow(probeWindowHandle);
            PumpUntil(() => window.IsVisible && card.ActualHeight > 0, "isolated window layout");
            Native.RequireForeground(probeWindowHandle);
            MoveOutsideCards(window);
            var collapsedHeight = card.ActualHeight;
            var preview = Descendants<MarkdownPreviewControl>(card).Single(control => control.PreviewMaxHeight == 68);
            Move(card, 28, 16);
            PumpUntil(() => card.IsMouseOver && preview.Visibility == Visibility.Visible, "real pointer entered task");
            Require(card.ActualHeight > collapsedHeight + 40, "Task did not expand after actual pointer entry.");
            Require(preview.ActualHeight <= 68.1, "Task preview exceeded four rendered lines.");
            Native.RequireForeground(probeWindowHandle);
            Native.MouseLeftDown();
            Native.MouseLeftUp();
            PumpUntil(() => opened == 1, "actual task click command");
            MoveOutsideCards(window);
            PumpUntil(() => !card.IsMouseOver && preview.Visibility == Visibility.Collapsed, "real pointer left task");
            Require(Math.Abs(card.ActualHeight - collapsedHeight) < 0.2, "Task height did not restore after actual pointer leave.");
            checks.Add("Completed task: actual pointer enter/leave, four-line height, click target and restored height");

            foreach (var status in new[] { TaskStatus.Todo, TaskStatus.Doing, TaskStatus.Blocked })
            {
                root.RowDefinitions[0].Height = new GridLength(220);
                task.Status = status;
                PumpUntil(() => Descendants<MarkdownPreviewControl>(card).Any(control => control.IsVisible && control.ActualHeight > 0 && control.ActualHeight <= 36.1),
                    "unfinished task details became visible without hover");
                window.UpdateLayout();
                var activePreview = Descendants<MarkdownPreviewControl>(card).Single(control => control.IsVisible);
                Require(activePreview.ActualHeight > 0 && activePreview.ActualHeight <= 36.1, "Unfinished task must use its original always-visible description size.");
                var text = VisibleText(card);
                Require(text.Contains("优先级 高") && text.Contains("附件 1") && text.Contains("指针验证") && text.Contains(task.DateRangeDisplay),
                    "Unfinished task metadata must be visible before actual pointer entry.");
                var activeHeight = card.ActualHeight;
                Move(card, 28, 16);
                PumpUntil(() => card.IsMouseOver, "real pointer entered unfinished task");
                PumpFor(100);
                Require(Math.Abs(card.ActualHeight - activeHeight) < 0.2 && VisibleText(card) == text,
                    $"Unfinished task must keep its full content and height on actual pointer entry: {status}, height {activeHeight} -> {card.ActualHeight}, text unchanged={VisibleText(card) == text}.");
                MoveOutsideCards(window);
                PumpUntil(() => !card.IsMouseOver, "real pointer left unfinished task");
                Require(Math.Abs(card.ActualHeight - activeHeight) < 0.2 && activePreview.IsVisible,
                    "Unfinished task must keep its description on actual pointer leave.");
                task.Status = TaskStatus.Done;
                PumpUntil(() => Math.Abs(card.ActualHeight - collapsedHeight) < 0.2 && !preview.IsVisible,
                    "completed task returned to title-only after a live status change");
                root.RowDefinitions[0].Height = new GridLength(130);
                PumpFor(40);
            }
            checks.Add("Todo/Doing/Blocked: live status changes restore description, dates, priority, attachments and tags; actual pointer enter/leave keeps content and height; Done restores title-only");

            var panel = Descendants<CalendarMonthPanel>(viewer).Single();
            var firstDay = (FrameworkElement)panel.Children[3];
            var firstDayCards = Descendants<Border>(firstDay).Where(border => border.Name == "MonthTaskCard").ToArray();
            foreach (var index in new[] { 0, 1, 2, 1, 0, 2 })
            {
                MoveToReachableTitle(firstDayCards[index], viewer);
                AssertStableHover(panel, viewer, 180);
            }
            checks.Add("Month: actual pointer moved repeatedly among three cards in one day with stable layout");

            MoveOutsideCards(window);
            PumpUntil(() => Math.Abs(viewer.ExtentHeight - 480) < 0.2, "month restored before last-week check");
            var lastDay = (FrameworkElement)panel.Children[38];
            var lastCards = Descendants<Border>(lastDay).Where(border => border.Name == "MonthTaskCard").ToArray();
            MoveToReachableTitle(lastCards[0], viewer);
            Require(viewer.ExtentHeight > viewer.ViewportHeight, "Last-week hover did not create a scrollable extent.");
            Native.RequireForeground(probeWindowHandle);
            Native.MouseWheel(-120);
            PumpFor(160);
            Require(viewer.VerticalOffset > 0, "Actual mouse wheel did not scroll the expanded last week.");
            AssertStableHover(panel, viewer, 300);
            var scrolledOffset = viewer.VerticalOffset;
            checks.Add("Month: actual wheel scrolled the expanded last week and hover/extent settled");

            MoveOutsideCards(window);
            PumpUntil(() => Math.Abs(viewer.ExtentHeight - 480) < 0.2 && viewer.VerticalOffset < 0.2, "month collapse and scroll-offset clamp");
            Move(card, 28, 16);
            PumpUntil(() => preview.Visibility == Visibility.Visible, "task expanded before drag");
            var dragStarted = false;
            var escapeObserved = false;
            card.QueryContinueDrag += (_, e) =>
            {
                dragStarted = true;
                escapeObserved |= e.EscapePressed;
                // Never continue dragging if another app takes the foreground.
                if (Native.GetForegroundWindow() != probeWindowHandle) e.Action = DragAction.Cancel;
            };
            Native.RequireForeground(probeWindowHandle);
            Native.MouseLeftDown();
            PumpFor(50);
            var cancel = Task.Run(async () =>
            {
                await Task.Delay(350);
                if (Native.GetForegroundWindow() == probeWindowHandle)
                {
                    Native.EscapeDown();
                    await Task.Delay(40);
                    Native.EscapeUp();
                }
                Native.MouseLeftUp();
            });
            Move(card, 70, 35);
            PumpUntil(() => cancel.IsCompleted, "real drag cancellation", 4000);
            PumpFor(120);
            Require(dragStarted && escapeObserved, "The native drag loop did not observe the Escape cancellation.");
            Require(opened == 1, "Cancelling a drag accidentally opened the task.");
            var cardBorder = (Border)card.FindName("CardBorder");
            Require(Math.Abs(cardBorder.Opacity - 1) < 0.001, "Cancelled drag left a translucent card.");
            Require(!Descendants<Adorner>(window).Any(adorner => adorner.GetType().Name == "DragGhostAdorner"), "Cancelled drag left a drag ghost.");
            MoveOutsideCards(window);
            PumpUntil(() => preview.Visibility == Visibility.Collapsed, "task collapsed after cancelled drag");
            checks.Add("Task: native drag loop entered from expanded card, Escape cancelled, no accidental open or drag ghost");
            VerifyNativeInputEntries(datePicker, textBox, probeWindowHandle, checks);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(new
            {
                inputMethod = "Win32 OS cursor/button/wheel/key input -> WPF InputManager (no reflection mouse state)",
                processId = Environment.ProcessId, checks, lastWeekWheelOffset = scrolledOffset,
                dragStarted, escapeObserved, taskOpenCount = opened, syntheticCardsOnly = true
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            Native.MouseLeftUp();
            Native.MouseRightUp();
            Native.EscapeUp();
            window.Close();
            Native.SetCursorPos(originalCursor.X, originalCursor.Y);
            if (previousForeground != IntPtr.Zero) Native.SetForegroundWindow(previousForeground);
        }
    }

    private static string VisibleText(DependencyObject root) => string.Join("\n", Descendants<TextBlock>(root)
        .Where(text => text.IsVisible).Select(text => new TextRange(text.ContentStart, text.ContentEnd).Text));

    private static void VerifyNativeInputEntries(DatePicker picker, TextBox textBox, IntPtr windowHandle, List<string> checks)
    {
        var originalDate = picker.SelectedDate!.Value;
        var openButton = (Button)picker.Template.FindName("PART_Button", picker);
        Click(openButton, windowHandle);
        PumpUntil(() => picker.IsDropDownOpen, "datepicker opened by actual click");
        var popup = (Popup)picker.Template.FindName("PART_Popup", picker);
        var calendar = popup.Child as Calendar ?? Descendants<Calendar>(popup.Child).Single();
        PumpUntil(() => calendar.IsKeyboardFocusWithin, "datepicker keyboard focus");
        PressKey(windowHandle, 0x27); // Right
        PumpUntil(() => picker.SelectedDate == originalDate.AddDays(1), "Right selected the next date");
        PressKey(windowHandle, 0x1B); // Escape
        PumpUntil(() => !picker.IsDropDownOpen, "Escape closed the datepicker");
        Require(picker.SelectedDate == originalDate, "Escape must cancel the date change and restore the original selected date.");

        Click(openButton, windowHandle);
        PumpUntil(() => picker.IsDropDownOpen && calendar.IsKeyboardFocusWithin, "datepicker reopened with focus");
        PressKey(windowHandle, 0x27); // Right
        PumpUntil(() => picker.SelectedDate == originalDate.AddDays(1), "Right selected the next date for confirmation");
        PressKey(windowHandle, 0x0D); // Enter
        PumpUntil(() => !picker.IsDropDownOpen, "Enter confirmed and closed the datepicker");
        Require(picker.SelectedDate == originalDate.AddDays(1), "Enter must retain the confirmed date.");
        checks.Add("DatePicker: production style opened by real click; Right+Escape cancelled/restored and Right+Enter confirmed the next date");

        Click(textBox, windowHandle);
        var originalText = textBox.Text;
        Native.RequireForeground(windowHandle);
        Native.KeyDown(0x11); // Control
        try { PressKey(windowHandle, 0x41); } // A
        finally { Native.KeyUp(0x11); }
        PumpUntil(() => textBox.SelectionLength == textBox.Text.Length, "actual Ctrl+A selected the textbox text");
        Move(textBox, 28, 14);
        Native.RequireForeground(windowHandle);
        Native.MouseRightDown();
        Native.MouseRightUp();
        ContextMenu? menu = null;
        PumpUntil(() => (menu = OpenNativeContextMenu()) is not null, "native TextBox right-click menu");
        // WPF's default text menu has Cut/Copy/Paste, but no Select All item.
        // Verify command targeting and keyboard navigation without writing to the clipboard.
        var copy = menu!.Items.OfType<MenuItem>().Single(item => item.Command == ApplicationCommands.Copy);
        Require(copy.IsEnabled, "Native Copy must target the selected text in the owning textbox.");
        PressKey(windowHandle, 0x28); // Down
        PumpUntil(() => menu.Items.OfType<MenuItem>().Any(item => item.IsHighlighted), "native menu keyboard navigation");
        PressKey(windowHandle, 0x1B);
        PumpUntil(() => !menu!.IsOpen, "Escape closed native context menu");
        Require(textBox.Text == originalText && textBox.SelectionLength == originalText.Length,
            "Closing the native context menu must preserve text and selection.");
        checks.Add("TextBox: actual Ctrl+A selected text, right-click opened native menu with enabled Copy, Down highlighted an item, Escape closed without changing text/selection; clipboard untouched");
    }

    private static ContextMenu? OpenNativeContextMenu()
    {
        foreach (var source in PresentationSource.CurrentSources.OfType<HwndSource>())
        {
            if (source.RootVisual is not { } root) continue;
            if (root is ContextMenu { IsOpen: true } rootMenu) return rootMenu;
            var menu = Descendants<ContextMenu>(root).FirstOrDefault(candidate => candidate.IsOpen);
            if (menu is not null) return menu;
        }
        return null;
    }

    private static void Click(FrameworkElement target, IntPtr windowHandle)
    {
        Move(target, target.ActualWidth / 2, target.ActualHeight / 2);
        PumpFor(35);
        Native.RequireForeground(windowHandle);
        Native.MouseLeftDown();
        Native.MouseLeftUp();
        PumpFor(45);
    }

    private static void PressKey(IntPtr windowHandle, byte key)
    {
        Native.RequireForeground(windowHandle);
        Native.KeyDown(key);
        try { PumpFor(35); }
        finally { Native.KeyUp(key); }
        PumpFor(50);
    }

    private static void MoveToReachableTitle(FrameworkElement card, ScrollViewer viewer)
    {
        // A preceding expanded card can move this title when it collapses.
        // Follow its actual laid-out title up to three times, just as a pointer
        // crossing its moving boundary would, then require a stable target.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            Move(card, Math.Min(card.ActualWidth / 2, 50), 10);
            PumpFor(65);
            if (card.IsMouseOver && Descendants<MarkdownPreviewControl>(card).Single().Visibility == Visibility.Visible) return;
        }
        throw new InvalidOperationException("Could not hover the intended calendar title through real input after its layout settled.");
    }

    private static void AssertStableHover(CalendarMonthPanel panel, ScrollViewer viewer, int milliseconds)
    {
        var states = new List<string>();
        var until = Environment.TickCount64 + milliseconds;
        do
        {
            PumpFor(30);
            var cards = Descendants<Border>(panel).Where(border => border.Name == "MonthTaskCard").ToArray();
            var hovered = cards.Select((card, index) => (card, index)).Where(pair => pair.card.IsMouseOver).ToArray();
            Require(hovered.Length == 1, "Real pointer should stay over exactly one calendar task during stability sampling.");
            var preview = Descendants<MarkdownPreviewControl>(hovered[0].card).Single();
            Require(preview.Visibility == Visibility.Visible && Math.Abs(preview.ActualHeight - 64) < 0.2,
                "Hovered calendar preview should display four rendered lines.");
            states.Add($"{hovered[0].index}:{viewer.ExtentHeight:F2}:{viewer.VerticalOffset:F2}");
        } while (Environment.TickCount64 < until);
        Require(states.Distinct().Count() == 1, "Stationary real pointer produced recurring hover/scroll layout changes: " + string.Join(",", states));
    }

    private static void Move(FrameworkElement target, double x, double y)
    {
        var point = target.PointToScreen(new Point(x, y));
        if (!Native.SetCursorPos((int)Math.Round(point.X), (int)Math.Round(point.Y)))
            throw new InvalidOperationException("Could not position the real desktop pointer.");
    }

    private static void MoveOutsideCards(Window window) => Move(window, window.ActualWidth - 25, window.ActualHeight - 30);

    private static void PumpUntil(Func<bool> predicate, string reason, int timeoutMilliseconds = 2500)
    {
        var watch = Stopwatch.StartNew();
        while (!predicate())
        {
            if (watch.ElapsedMilliseconds > timeoutMilliseconds) throw new TimeoutException(reason);
            PumpFor(10);
        }
    }

    private static void PumpFor(int milliseconds)
    {
        var until = Environment.TickCount64 + milliseconds;
        do
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
            Thread.Sleep(4);
        } while (Environment.TickCount64 < until);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Point { internal int X; internal int Y; }
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] internal static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
        internal static void MouseLeftDown() => mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
        internal static void MouseLeftUp() => mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        internal static void MouseRightDown() => mouse_event(0x0008, 0, 0, 0, UIntPtr.Zero);
        internal static void MouseRightUp() => mouse_event(0x0010, 0, 0, 0, UIntPtr.Zero);
        internal static void MouseWheel(int delta) => mouse_event(0x0800, 0, 0, unchecked((uint)delta), UIntPtr.Zero);
        internal static void EscapeDown() => keybd_event(0x1B, 0, 0, UIntPtr.Zero);
        internal static void EscapeUp() => keybd_event(0x1B, 0, 0x0002, UIntPtr.Zero);
        internal static void KeyDown(byte key) => keybd_event(key, 0, 0, UIntPtr.Zero);
        internal static void KeyUp(byte key) => keybd_event(key, 0, 0x0002, UIntPtr.Zero);
        internal static void RequireForeground(IntPtr expected)
        {
            if (GetForegroundWindow() != expected)
                throw new InvalidOperationException("The isolated probe could not own the foreground; no button or key input will be sent to another application.");
        }
    }
}
