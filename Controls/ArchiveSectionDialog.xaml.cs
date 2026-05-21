using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KanbanForOne.Models;

namespace KanbanForOne.Controls;

public sealed record ArchiveSectionDialogResult(Guid? SectionId, string? NewSectionName, Guid? DeleteSectionId = null);

public partial class ArchiveSectionDialog : Window
{
    private readonly IReadOnlyList<ArchiveSection> _sections;

    public ArchiveSectionDialog(IEnumerable<ArchiveSection> sections, ArchiveSection defaultSection)
    {
        InitializeComponent();

        _sections = sections.ToArray();
        SectionListBox.ItemsSource = _sections;
        SectionListBox.SelectedItem = _sections.FirstOrDefault(section => section.Id == defaultSection.Id)
            ?? _sections.FirstOrDefault();

        Loaded += (_, _) =>
        {
            PlayOpenAnimation();
            SectionListBox.Focus();
        };
    }

    public ArchiveSectionDialogResult? Result { get; private set; }

    public static ArchiveSectionDialogResult? Show(
        Window? owner,
        IEnumerable<ArchiveSection> sections,
        ArchiveSection defaultSection)
    {
        var dialog = new ArchiveSectionDialog(sections, defaultSection);

        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        var newSectionName = NewSectionText.Text.Trim();

        Result = string.IsNullOrWhiteSpace(newSectionName)
            ? new ArchiveSectionDialogResult((SectionListBox.SelectedItem as ArchiveSection)?.Id, null)
            : new ArchiveSectionDialogResult(null, newSectionName);

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnNewSectionTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ConfirmButton.Content = string.IsNullOrWhiteSpace(NewSectionText.Text)
            ? "归档"
            : "新建并归档";
    }

    private void OnDialogKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
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
