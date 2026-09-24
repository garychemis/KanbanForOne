using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KanbanForOne.Controls;

/// <summary>Keeps six equal weeks at rest and grows a week only for an expanded completed-task preview.</summary>
public sealed class CalendarMonthPanel : Panel
{
    public static readonly DependencyProperty ViewportHeightProperty = DependencyProperty.Register(
        nameof(ViewportHeight), typeof(double), typeof(CalendarMonthPanel),
        new FrameworkPropertyMetadata(480d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private double[] _rowHeights = [];

    public double ViewportHeight
    {
        get => (double)GetValue(ViewportHeightProperty);
        set => SetValue(ViewportHeightProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        const int columns = 7;
        var rows = Math.Max(6, (InternalChildren.Count + columns - 1) / columns);
        var baseHeight = Math.Max(48, ViewportHeight / rows);
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 700;
        _rowHeights = Enumerable.Repeat(baseHeight, rows).ToArray();

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];
            var expanded = child.IsMouseOver && HasExpandedPreview(child);
            child.Measure(new Size(width / columns, expanded ? double.PositiveInfinity : baseHeight));
            if (expanded)
                _rowHeights[index / columns] = Math.Max(_rowHeights[index / columns], child.DesiredSize.Height);
        }

        return new Size(width, _rowHeights.Sum());
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0d;
        for (var row = 0; row < _rowHeights.Length; row++)
        {
            for (var column = 0; column < 7; column++)
            {
                var index = row * 7 + column;
                if (index >= InternalChildren.Count) break;
                InternalChildren[index].Arrange(new Rect(column * finalSize.Width / 7, y, finalSize.Width / 7, _rowHeights[row]));
            }
            y += _rowHeights[row];
        }
        return finalSize;
    }

    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        if (visualAdded is UIElement added)
        {
            added.MouseEnter += OnHoverChanged;
            added.MouseLeave += OnHoverChanged;
        }
        if (visualRemoved is UIElement removed)
        {
            removed.MouseEnter -= OnHoverChanged;
            removed.MouseLeave -= OnHoverChanged;
        }
    }

    private void OnHoverChanged(object sender, MouseEventArgs e) => InvalidateMeasure();

    private static bool HasExpandedPreview(DependencyObject element)
    {
        if (element is UIElement { Visibility: not Visibility.Visible }) return false;
        if (element is MarkdownPreviewControl { IsPreviewExpanded: true, HasPreviewContent: true }) return true;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
            if (HasExpandedPreview(VisualTreeHelper.GetChild(element, index))) return true;
        return false;
    }
}
