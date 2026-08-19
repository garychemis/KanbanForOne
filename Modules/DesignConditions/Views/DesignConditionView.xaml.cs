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
        if (sender is DataGrid { SelectedItem: DesignConditionEntry entry } && DataContext is DesignConditionViewModel viewModel && viewModel.OpenCommand.CanExecute(entry))
        {
            viewModel.OpenCommand.Execute(entry);
        }
    }
}
