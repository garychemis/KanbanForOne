using System.Windows;
using System.Windows.Controls;

namespace KanbanForOne.Controls;

public partial class SearchBoxControl : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(SearchBoxControl),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public SearchBoxControl()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        Text = string.Empty;
    }
}
