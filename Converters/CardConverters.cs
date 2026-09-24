using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using KanbanForOne.Models;
using KanbanForOne.Services;
using TaskStatus = KanbanForOne.Models.TaskStatus;

namespace KanbanForOne.Converters;

public sealed class TaskStatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var role = parameter as string;
        if (role == "Border")
        {
            return value switch
            {
                TaskStatus.Doing => ThemeService.GetBrush("TaskDoingBorderBrush", "#9EB9C2"),
                TaskStatus.Blocked => ThemeService.GetBrush("TaskBlockedBorderBrush", "#D1A7A1"),
                TaskStatus.Done => ThemeService.GetBrush("TaskDoneBorderBrush", "#A7BFAF"),
                _ => ThemeService.GetBrush("TaskTodoBorderBrush", "#B9BDB7")
            };
        }

        if (role == "Secondary")
        {
            return value is TaskStatus.Blocked
                ? ThemeService.GetBrush("BlockedCardSecondaryBrush", "#626D67")
                : ThemeService.GetBrush("TextSecondaryBrush", "#65716C");
        }

        return value switch
        {
            TaskStatus.Todo => ThemeService.GetBrush("TaskTodoAccentBrush", "#8D9289"),
            TaskStatus.Doing => ThemeService.GetBrush("TaskDoingAccentBrush", "#548698"),
            TaskStatus.Blocked => role == "Foreground"
                ? ThemeService.GetBrush("BlockedCardTextBrush", "#8E514B")
                : ThemeService.GetBrush("BlockedCardAccentBrush", "#AE6D66"),
            TaskStatus.Done => ThemeService.GetBrush("TaskDoneAccentBrush", "#58876D"),
            _ => ThemeService.GetBrush("TaskTodoAccentBrush", "#8D9289")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class TaskStatusBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            TaskStatus.Todo => ThemeService.GetBrush("TaskTodoBackgroundBrush", "#FDFCF7"),
            TaskStatus.Doing => ThemeService.GetBrush("TaskDoingBackgroundBrush", "#EEF6F8"),
            TaskStatus.Blocked => ThemeService.GetBrush("BlockedCardBackgroundBrush", "#FAEEEC"),
            TaskStatus.Done => ThemeService.GetBrush("TaskDoneBackgroundBrush", "#F1F6EF"),
            _ => ThemeService.GetBrush("CardSurfaceBrush", "#F3F4F2")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class TaskStatusDrawerBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ThemeService.GetBrush("CardSurfaceBrush", "#F3F4F2");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
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
            TaskPriority.High => ThemeService.GetBrush("DangerBrush", "#A34F59"),
            TaskPriority.Medium => ThemeService.GetBrush("PriorityMediumBrush", "#927039"),
            TaskPriority.Low => ThemeService.GetBrush("TextSecondaryBrush", "#65716C"),
            _ => ThemeService.GetBrush("TextSecondaryBrush", "#65716C")
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
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

public sealed class TaskPreviewSegmentBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var role = parameter as string ?? "Background";
        var (key, hex) = value switch
        {
            TaskStatus.Todo => role switch
            {
                "Border" => ("SegmentTodoBorderBrush", "#CBD5E1"),
                "Foreground" => ("SegmentTodoForegroundBrush", "#475569"),
                _ => ("SegmentTodoBackgroundBrush", "#F1F5F9")
            },
            TaskStatus.Doing => role switch
            {
                "Border" => ("SegmentDoingBorderBrush", "#D7E4E8"),
                "Foreground" => ("SegmentDoingForegroundBrush", "#4D7483"),
                _ => ("SegmentDoingBackgroundBrush", "#EFF4F6")
            },
            TaskStatus.Blocked => role switch
            {
                "Border" => ("BlockedCardBorderBrush", "#E4D3CF"),
                "Foreground" => ("BlockedCardTextBrush", "#8E514B"),
                _ => ("BlockedCardBackgroundBrush", "#FAEEEC")
            },
            TaskStatus.Done => role switch
            {
                "Border" => ("SegmentDoneBorderBrush", "#D9E6D7"),
                "Foreground" => ("SegmentDoneForegroundBrush", "#4D785E"),
                _ => ("SegmentDoneBackgroundBrush", "#F0F5ED")
            },
            TaskPriority.High => role switch
            {
                "Border" => ("DangerBorderBrush", "#EBD4D3"),
                "Foreground" => ("DangerBrush", "#A34F59"),
                _ => ("DangerTintBrush", "#F8ECEB")
            },
            TaskPriority.Medium => role switch
            {
                "Border" => ("SegmentMediumBorderBrush", "#E8DACA"),
                "Foreground" => ("SegmentMediumForegroundBrush", "#936C42"),
                _ => ("SegmentMediumBackgroundBrush", "#F8F2E9")
            },
            TaskPriority.Low => role switch
            {
                "Border" => ("SegmentLowBorderBrush", "#E2E8F0"),
                "Foreground" => ("SegmentTodoForegroundBrush", "#475569"),
                _ => ("SegmentLowBackgroundBrush", "#F8FAFC")
            },
            _ => role switch
            {
                "Border" => ("SegmentDefaultBorderBrush", "#E5E7EB"),
                "Foreground" => ("TextSecondaryBrush", "#65716C"),
                _ => ("SegmentLowBackgroundBrush", "#F8FAFC")
            }
        };

        return ThemeService.GetBrush(key, hex);
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
        return value is TaskStatus.Done
            ? ThemeService.GetBrush("TextSecondaryBrush", "#65716C")
            : ThemeService.GetBrush("TextPrimaryBrush", "#283D38");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
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
        return hex switch
        {
            "#DDE3EB" => ThemeService.GetBrush("FilterSelectedBrush", hex),
            "#E3EDE9" or "SelectedBackgroundBrush" => ThemeService.GetBrush("SelectedBackgroundBrush", "#E3EDE9"),
            "#00FFFFFF" or "Transparent" => Brushes.Transparent,
            _ when !hex.StartsWith('#') => ThemeService.GetBrush(hex, "#00FFFFFF"),
            _ => (Brush)new BrushConverter().ConvertFromString(hex)!
        };
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

public sealed class SizeMultiplierConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double size || size < 0)
        {
            return 0d;
        }

        var factor = parameter is not null
            && double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 1d;

        return size * factor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
