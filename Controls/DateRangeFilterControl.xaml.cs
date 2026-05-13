using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KanbanForOne.Controls;

public partial class DateRangeFilterControl : UserControl
{
    public static readonly DependencyProperty StartDateProperty = DependencyProperty.Register(
        nameof(StartDate),
        typeof(DateTime?),
        typeof(DateRangeFilterControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty EndDateProperty = DependencyProperty.Register(
        nameof(EndDate),
        typeof(DateTime?),
        typeof(DateRangeFilterControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty HasFilterProperty = DependencyProperty.Register(
        nameof(HasFilter),
        typeof(bool),
        typeof(DateRangeFilterControl),
        new PropertyMetadata(false));

    public static readonly DependencyProperty ClearCommandProperty = DependencyProperty.Register(
        nameof(ClearCommand),
        typeof(ICommand),
        typeof(DateRangeFilterControl),
        new PropertyMetadata(null));

    public DateRangeFilterControl()
    {
        InitializeComponent();
    }

    public DateTime? StartDate
    {
        get => (DateTime?)GetValue(StartDateProperty);
        set => SetValue(StartDateProperty, value);
    }

    public DateTime? EndDate
    {
        get => (DateTime?)GetValue(EndDateProperty);
        set => SetValue(EndDateProperty, value);
    }

    public bool HasFilter
    {
        get => (bool)GetValue(HasFilterProperty);
        set => SetValue(HasFilterProperty, value);
    }

    public ICommand? ClearCommand
    {
        get => (ICommand?)GetValue(ClearCommandProperty);
        set => SetValue(ClearCommandProperty, value);
    }
}
