using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using KanbanForOne.Models;
using TaskStatus = KanbanForOne.Models.TaskStatus;

namespace KanbanForOne.Converters;

public sealed class TaskStatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TaskStatus.Todo => BrushFrom("#A8A29E"),
            TaskStatus.Doing => BrushFrom("#2F80ED"),
            TaskStatus.Blocked => BrushFrom("#F97316"),
            TaskStatus.Done => BrushFrom("#4AA568"),
            _ => BrushFrom("#A8A29E")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}

public sealed class TaskStatusBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TaskStatus.Todo => BrushFrom("#FFFDF5"),
            TaskStatus.Doing => BrushFrom("#EEF6FF"),
            TaskStatus.Blocked => BrushFrom("#FFF3EC"),
            TaskStatus.Done => BrushFrom("#F1FAF5"),
            _ => BrushFrom("#FFFFFF")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}

public sealed class TaskStatusDrawerBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TaskStatus.Todo => BrushFrom("#FFFBF0"),
            TaskStatus.Doing => BrushFrom("#E7F5FF"),
            TaskStatus.Blocked => BrushFrom("#FFF4E6"),
            TaskStatus.Done => BrushFrom("#EBFBEE"),
            _ => BrushFrom("#FFFFFF")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}

public sealed class TaskStatusNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TaskStatus.Todo => "待办",
            TaskStatus.Doing => "进行中",
            TaskStatus.Blocked => "卡住",
            TaskStatus.Done => "完成",
            _ => string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class TaskPriorityBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TaskPriority.High => BrushFrom("#EF4444"),
            TaskPriority.Medium => BrushFrom("#F59E0B"),
            TaskPriority.Low => BrushFrom("#64748B"),
            _ => BrushFrom("#64748B")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}

public sealed class TaskPriorityNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TaskPriority.High => "高",
            TaskPriority.Medium => "中",
            TaskPriority.Low => "低",
            _ => string.Empty
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null)
        {
            return Binding.DoNothing;
        }

        var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return Enum.Parse(enumType, parameter.ToString()!);
    }
}

public sealed class DoneTextDecorationConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is TaskStatus.Done ? TextDecorations.Strikethrough : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class DoneForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is TaskStatus.Done ? BrushFrom("#6B7280") : BrushFrom("#212529");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}

public sealed class DateDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            DateTime date => date.ToString("MM-dd", culture),
            DateTimeOffset date => date.ToString("MM-dd", culture),
            _ => "无日期"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class AttachmentSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB"];
        var size = (double)bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{size:0} {units[unitIndex]}" : $"{size:0.0} {units[unitIndex]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        var isVisible = value is not null;
        return isVisible ^ invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class BooleanToVisibilityConverterEx : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        var isVisible = value is true;
        return isVisible ^ invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class FilterSelectedBackgroundConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var currentFilter = values.ElementAtOrDefault(0) as string;
        var buttonFilter = values.ElementAtOrDefault(1) as string;

        var selectedBrush = "#DDE3EB";
        var normalBrush = "#00FFFFFF";

        if (parameter is string colors)
        {
            var parts = colors.Split('|', StringSplitOptions.TrimEntries);
            selectedBrush = parts.ElementAtOrDefault(0) ?? selectedBrush;
            normalBrush = parts.ElementAtOrDefault(1) ?? normalBrush;
        }

        return string.Equals(currentFilter, buttonFilter, StringComparison.Ordinal)
            ? BrushFrom(selectedBrush)
            : BrushFrom(normalBrush);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return targetTypes.Select(_ => Binding.DoNothing).ToArray();
    }

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }
}

public sealed class CollectionEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        var isEmpty = value switch
        {
            ICollection collection => collection.Count == 0,
            IEnumerable enumerable => !enumerable.Cast<object>().Any(),
            _ => true
        };

        return isEmpty ^ invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class TagDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return FormatTag(value as string);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    internal static string FormatTag(string? tag)
    {
        tag = tag?.Trim();

        if (string.IsNullOrWhiteSpace(tag))
        {
            return string.Empty;
        }

        return tag;
    }
}

public sealed class TagListDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var tags = value switch
        {
            string text => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            IEnumerable<string> strings => strings,
            IEnumerable enumerable => enumerable.Cast<object>().Select(item => item?.ToString() ?? string.Empty),
            _ => []
        };

        return string.Join(", ", tags
            .Select(TagDisplayConverter.FormatTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag)));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
