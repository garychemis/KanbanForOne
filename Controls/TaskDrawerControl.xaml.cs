using System.Windows;
using System.Windows.Controls;
using KanbanForOne.Models;

namespace KanbanForOne.Controls;

public partial class TaskDrawerControl : UserControl
{
    public static readonly DependencyProperty SelectedTaskProperty = DependencyProperty.Register(
        nameof(SelectedTask),
        typeof(TaskItem),
        typeof(TaskDrawerControl),
        new PropertyMetadata(null));

    public TaskDrawerControl()
    {
        InitializeComponent();
    }

    public TaskItem? SelectedTask
    {
        get => (TaskItem?)GetValue(SelectedTaskProperty);
        set => SetValue(SelectedTaskProperty, value);
    }

    private void OnAddTagClick(object sender, RoutedEventArgs e)
    {
        TaskTagEditor.FocusInput();
    }
}
