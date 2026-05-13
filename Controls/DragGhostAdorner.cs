using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Controls;

namespace KanbanForOne.Controls;

internal sealed class DragGhostAdorner : Adorner
{
    private VisualCollection? _visuals;
    private FrameworkElement? _ghost;
    private readonly Point _cursorOffset;
    private readonly Size _size;
    private double _left;
    private double _top;

    public DragGhostAdorner(UIElement adornedElement, FrameworkElement sourceElement, Point cursorOffset)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
        _cursorOffset = cursorOffset;
        _size = new Size(sourceElement.ActualWidth, sourceElement.ActualHeight);
        _ghost = CreateGhost(sourceElement, _size);
        _visuals = new VisualCollection(this) { _ghost };
    }

    protected override int VisualChildrenCount => _visuals?.Count ?? 0;

    public void UpdatePositionFromScreen(Point screenPosition)
    {
        var position = AdornedElement.PointFromScreen(screenPosition);
        _left = position.X - _cursorOffset.X;
        _top = position.Y - _cursorOffset.Y;
        InvalidateArrange();
    }

    public static bool TryGetCursorScreenPosition(out Point screenPosition)
    {
        if (GetCursorPos(out var point))
        {
            screenPosition = new Point(point.X, point.Y);
            return true;
        }

        screenPosition = default;
        return false;
    }

    protected override Visual GetVisualChild(int index)
    {
        if (_visuals is null || index < 0 || index >= _visuals.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _visuals[index];
    }

    protected override Size MeasureOverride(Size constraint)
    {
        _ghost?.Measure(_size);
        return constraint;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _ghost?.Arrange(new Rect(new Point(_left, _top), _size));
        return finalSize;
    }

    private static FrameworkElement CreateGhost(FrameworkElement sourceElement, Size size)
    {
        var bitmap = CaptureElement(sourceElement, size);

        var shadow = new Border
        {
            Width = size.Width,
            Height = size.Height,
            CornerRadius = new CornerRadius(8),
            Background = Brushes.White,
            Opacity = 0.34,
            Effect = new DropShadowEffect
            {
                BlurRadius = 24,
                ShadowDepth = 10,
                Direction = 270,
                Opacity = 0.22
            }
        };

        var image = new Image
        {
            Source = bitmap,
            Width = size.Width,
            Height = size.Height,
            Stretch = Stretch.Fill,
            Opacity = 0.72,
            Effect = new BlurEffect
            {
                Radius = 1.6,
                KernelType = KernelType.Gaussian,
                RenderingBias = RenderingBias.Performance
            }
        };

        return new Grid
        {
            Width = size.Width,
            Height = size.Height,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(0.4),
            Children =
            {
                shadow,
                image
            }
        };
    }

    private static ImageSource CaptureElement(FrameworkElement sourceElement, Size size)
    {
        var dpi = VisualTreeHelper.GetDpi(sourceElement);
        var width = Math.Max(1, (int)Math.Ceiling(size.Width * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Ceiling(size.Height * dpi.DpiScaleY));
        var bitmap = new RenderTargetBitmap(
            width,
            height,
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);

        bitmap.Render(sourceElement);
        bitmap.Freeze();
        return bitmap;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out CursorPoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint
    {
        public int X;
        public int Y;
    }
}
