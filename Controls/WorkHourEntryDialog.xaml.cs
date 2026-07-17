using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KanbanForOne.Models;
using KanbanForOne.Services;

namespace KanbanForOne.Controls;

public enum WorkHourDialogAction
{
    None,
    Save,
    Delete
}

public sealed record WorkHourEntryDialogResult(
    WorkHourDialogAction Action,
    Guid Id,
    DateTime WorkDate,
    string ProjectNumber,
    string Discipline,
    string WorkActivity,
    int HourUnits,
    string Remark,
    DateTime CreatedAt);

public partial class WorkHourEntryDialog : Window
{
    private readonly WorkHourEntry? _sourceEntry;
    private readonly bool _isNew;
    private bool _isEditing;
    private bool _allowClose;

    private WorkHourEntryDialog(
        WorkHourEntry? entry,
        DateTime defaultDate,
        IReadOnlyList<string> disciplines,
        IReadOnlyList<string> workActivities)
    {
        InitializeComponent();
        _sourceEntry = entry;
        _isNew = entry is null;

        DisciplineComboBox.ItemsSource = disciplines;
        ActivityComboBox.ItemsSource = workActivities;

        if (entry is null)
        {
            WorkDatePicker.SelectedDate = defaultDate.Date;
            DialogSubtitleText.Text = defaultDate.ToString("yyyy年 M月 d日");
            SetEditing(true);
        }
        else
        {
            PopulatePreview(entry);
            PopulateDraft(entry);
            SetEditing(false);
        }

        Loaded += (_, _) =>
        {
            PlayOpenAnimation();
            if (_isEditing)
            {
                ProjectNumberTextBox.Focus();
            }
        };
    }

    public WorkHourEntryDialogResult? Result { get; private set; }

    public static WorkHourEntryDialogResult? Show(
        Window? owner,
        WorkHourEntry? entry,
        DateTime defaultDate,
        IReadOnlyList<string> disciplines,
        IReadOnlyList<string> workActivities)
    {
        var dialog = new WorkHourEntryDialog(entry, defaultDate, disciplines, workActivities);
        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.Height = Math.Clamp(owner.ActualHeight * 0.82, 680, 760);
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.ShowDialog();
        return dialog.Result;
    }

    private void PopulatePreview(WorkHourEntry entry)
    {
        DialogTitleText.Text = "人工时详情";
        DialogSubtitleText.Text = entry.WorkDate.ToString("yyyy年 M月 d日");
        PreviewDateText.Text = entry.WorkDate.ToString("yyyy/M/d");
        PreviewHoursText.Text = entry.HoursDisplay;
        PreviewProjectNumberText.Text = entry.ProjectNumber;
        PreviewDisciplineText.Text = entry.Discipline;
        PreviewActivityText.Text = entry.WorkActivity;
        PreviewRemarkText.Text = entry.HasRemark ? entry.Remark : "无备注";
        PreviewRemarkText.Foreground = entry.HasRemark
            ? (Brush)FindResource("TextPrimaryBrush")
            : (Brush)FindResource("TextSecondaryBrush");
    }

    private void PopulateDraft(WorkHourEntry entry)
    {
        WorkDatePicker.SelectedDate = entry.WorkDate;
        ProjectNumberTextBox.Text = entry.ProjectNumber;
        DisciplineComboBox.Text = entry.Discipline;
        ActivityComboBox.Text = entry.WorkActivity;
        HoursTextBox.Text = WorkHourValueConverter.FormatHours(entry.HourUnits);
        RemarkTextBox.Text = entry.Remark;
    }

    private void SetEditing(bool editing)
    {
        _isEditing = editing;
        PreviewPanel.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        PreviewActions.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        EditPanel.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        EditActions.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        DialogTitleText.Text = editing ? (_isNew ? "添加人工时" : "编辑人工时") : "人工时详情";
        ValidationText.Visibility = Visibility.Collapsed;
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (_sourceEntry is not null)
        {
            PopulateDraft(_sourceEntry);
        }
        SetEditing(true);
        ProjectNumberTextBox.Focus();
    }

    private void OnCancelEditClick(object sender, RoutedEventArgs e)
    {
        if (_isNew)
        {
            CloseAllowed();
            return;
        }

        PopulateDraft(_sourceEntry!);
        SetEditing(false);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var workDate = WorkDatePicker.SelectedDate?.Date;
        var projectNumber = WorkHourValueConverter.NormalizeProjectNumber(ProjectNumberTextBox.Text);
        var discipline = DisciplineComboBox.Text.Trim();
        var activity = ActivityComboBox.Text.Trim();

        if (workDate is null)
        {
            ShowValidation("请选择日期。");
            return;
        }
        if (projectNumber.Length == 0)
        {
            ShowValidation("请输入项目号。");
            return;
        }
        if (discipline.Length == 0)
        {
            ShowValidation("请选择或输入专业。");
            return;
        }
        if (activity.Length == 0)
        {
            ShowValidation("请选择或输入工作内容。");
            return;
        }
        if (!WorkHourValueConverter.TryParseHours(HoursTextBox.Text, out var hourUnits))
        {
            ShowValidation("工时必须大于 0、不超过 24，并且最多保留两位小数。");
            return;
        }

        Result = new WorkHourEntryDialogResult(
            WorkHourDialogAction.Save,
            _sourceEntry?.Id ?? Guid.NewGuid(),
            workDate.Value,
            projectNumber,
            discipline,
            activity,
            hourUnits,
            RemarkTextBox.Text.Trim(),
            _sourceEntry?.CreatedAt ?? DateTime.Now);
        CloseAllowed();
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_sourceEntry is null || !ConfirmDialog.Show(this, "删除人工时", "确定删除这条人工时记录吗？", "删除"))
        {
            return;
        }

        Result = new WorkHourEntryDialogResult(
            WorkHourDialogAction.Delete,
            _sourceEntry.Id,
            _sourceEntry.WorkDate,
            _sourceEntry.ProjectNumber,
            _sourceEntry.Discipline,
            _sourceEntry.WorkActivity,
            _sourceEntry.HourUnits,
            _sourceEntry.Remark,
            _sourceEntry.CreatedAt);
        CloseAllowed();
    }

    private void OnProjectNumberLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ProjectNumberTextBox.Text = WorkHourValueConverter.NormalizeProjectNumber(ProjectNumberTextBox.Text);
    }

    private void ShowValidation(string message)
    {
        ValidationText.Text = message;
        ValidationText.Visibility = Visibility.Visible;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => RequestClose();

    private void OnDialogKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            RequestClose();
            e.Handled = true;
        }
    }

    private void RequestClose()
    {
        if (_isEditing && HasDraftChanges() &&
            !ConfirmDialog.Show(this, "放弃修改", "当前人工时内容尚未保存，确定关闭吗？", "放弃"))
        {
            return;
        }

        CloseAllowed();
    }

    private bool HasDraftChanges()
    {
        if (_isNew)
        {
            return !string.IsNullOrWhiteSpace(ProjectNumberTextBox.Text)
                   || !string.IsNullOrWhiteSpace(DisciplineComboBox.Text)
                   || !string.IsNullOrWhiteSpace(ActivityComboBox.Text)
                   || !string.IsNullOrWhiteSpace(HoursTextBox.Text)
                   || !string.IsNullOrWhiteSpace(RemarkTextBox.Text);
        }

        return WorkDatePicker.SelectedDate?.Date != _sourceEntry!.WorkDate
               || WorkHourValueConverter.NormalizeProjectNumber(ProjectNumberTextBox.Text) != _sourceEntry.ProjectNumber
               || !string.Equals(DisciplineComboBox.Text.Trim(), _sourceEntry.Discipline, StringComparison.Ordinal)
               || !string.Equals(ActivityComboBox.Text.Trim(), _sourceEntry.WorkActivity, StringComparison.Ordinal)
               || !WorkHourValueConverter.TryParseHours(HoursTextBox.Text, out var units)
               || units != _sourceEntry.HourUnits
               || !string.Equals(RemarkTextBox.Text.Trim(), _sourceEntry.Remark, StringComparison.Ordinal);
    }

    private void OnDialogClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        if (_isEditing && HasDraftChanges() &&
            !ConfirmDialog.Show(this, "放弃修改", "当前人工时内容尚未保存，确定关闭吗？", "放弃"))
        {
            e.Cancel = true;
            return;
        }

        _allowClose = true;
    }

    private void CloseAllowed()
    {
        _allowClose = true;
        Close();
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
            // The mouse state can change during the drag handoff.
        }
    }

    private void PlayOpenAnimation()
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(150);
        DialogChrome.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = easing });
        if (DialogChrome.RenderTransform is ScaleTransform scale)
        {
            var animation = new DoubleAnimation(0.96, 1, duration) { EasingFunction = easing };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
    }
}
