using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Modules.DesignConditions.ViewModels;

namespace KanbanForOne.Modules.DesignConditions.Views;

public partial class DesignConditionView : UserControl
{
    public DesignConditionView() => InitializeComponent();

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || sender is not DataGrid grid ||
            e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(grid, source) is not DataGridRow { Item: DesignConditionEntry entry }) return;
        if (DataContext is DesignConditionViewModel viewModel && viewModel.OpenCommand.CanExecute(entry))
        {
            e.Handled = true;
            viewModel.OpenCommand.Execute(entry);
        }
    }

    private void OnListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not DataGrid { SelectedItem: DesignConditionEntry entry } ||
            DataContext is not DesignConditionViewModel viewModel || !viewModel.OpenCommand.CanExecute(entry)) return;
        e.Handled = true;
        viewModel.OpenCommand.Execute(entry);
    }
}
