using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KanbanForOne.Models;

namespace KanbanForOne.Controls;

public partial class ArchiveSectionPickerDialog : Window
{
    private readonly IReadOnlyList<ArchiveSection> _sections;
    private readonly ObservableCollection<ArchiveSection> _filteredSections = new();
    private readonly Guid? _selectedSectionId;

    public ArchiveSectionPickerDialog(IEnumerable<ArchiveSection> sections, ArchiveSection? selectedSection)
    {
        InitializeComponent();

        _sections = sections.ToArray();
        _selectedSectionId = selectedSection?.Id;
        SectionListBox.ItemsSource = _filteredSections;
        ApplyFilter();

        Loaded += (_, _) =>
        {
            PlayOpenAnimation();
            SearchTextBox.Focus();
        };
    }

    public ArchiveSectionDialogResult? Result { get; private set; }

    public static ArchiveSectionDialogResult? Show(
        Window? owner,
        IEnumerable<ArchiveSection> sections,
        ArchiveSection? selectedSection)
    {
        var dialog = new ArchiveSectionPickerDialog(sections, selectedSection);

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

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnNewSectionTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateConfirmButton();
    }

    private void OnSectionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateConfirmButton();
    }

    private void ApplyFilter()
    {
        var query = SearchTextBox?.Text.Trim() ?? string.Empty;
        var previousSelectedId = (SectionListBox?.SelectedItem as ArchiveSection)?.Id ?? _selectedSectionId;
        var filtered = _sections
            .Where(section => string.IsNullOrWhiteSpace(query) ||
                section.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(section => section.SortOrder)
            .ThenBy(section => section.Name)
            .ToArray();

        _filteredSections.Clear();

        foreach (var section in filtered)
        {
            _filteredSections.Add(section);
        }

        if (SectionListBox is not null)
        {
            SectionListBox.SelectedItem = previousSelectedId.HasValue
                ? _filteredSections.FirstOrDefault(section => section.Id == previousSelectedId.Value) ?? _filteredSections.FirstOrDefault()
                : _filteredSections.FirstOrDefault();
        }

        if (EmptyResultText is not null)
        {
            EmptyResultText.Visibility = _filteredSections.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        UpdateConfirmButton();
    }

    private void UpdateConfirmButton()
    {
        if (ConfirmButton is null)
        {
            return;
        }

        var hasNewName = !string.IsNullOrWhiteSpace(NewSectionText?.Text);
        ConfirmButton.Content = hasNewName ? "新建并打开" : "打开";
        ConfirmButton.IsEnabled = hasNewName || SectionListBox?.SelectedItem is ArchiveSection;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void OnSectionMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SectionListBox.SelectedItem is ArchiveSection)
        {
            ConfirmSelection();
            e.Handled = true;
        }
    }

    private void OnDeleteSectionClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not Button { CommandParameter: ArchiveSection section } || section.IsDefault)
        {
            return;
        }

        var confirmed = ConfirmDialog.Show(
            this,
            "删除归档分区",
            $"删除“{section.Name}”后，其中 {section.TotalCount} 张卡片会移动到“默认”分区。是否继续？",
            "删除",
            "取消");

        if (!confirmed)
        {
            return;
        }

        Result = new ArchiveSectionDialogResult(null, null, section.Id);
        DialogResult = true;
        Close();
    }

    private void ConfirmSelection()
    {
        var newSectionName = NewSectionText.Text.Trim();

        if (!string.IsNullOrWhiteSpace(newSectionName))
        {
            Result = new ArchiveSectionDialogResult(null, newSectionName);
            DialogResult = true;
            Close();
            return;
        }

        if (SectionListBox.SelectedItem is not ArchiveSection section)
        {
            return;
        }

        Result = new ArchiveSectionDialogResult(section.Id, null);
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnDialogKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            ConfirmSelection();
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
