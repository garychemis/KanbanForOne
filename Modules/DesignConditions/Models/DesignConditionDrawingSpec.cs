using System.Globalization;

namespace KanbanForOne.Modules.DesignConditions.Models;

public sealed record DesignConditionDrawingSpec(string DrawingSize, int DrawingCount);

public sealed record DesignConditionDrawingStorage(string DrawingSizes, string DrawingCounts, int TotalDrawingCount);

public static class DesignConditionDrawingCodec
{
    public const char Separator = '|';

    public static bool TryParse(
        string? drawingSizes,
        string? drawingCounts,
        out IReadOnlyList<DesignConditionDrawingSpec> specifications,
        out string errorMessage)
    {
        specifications = [];
        errorMessage = string.Empty;
        var sizeParts = (drawingSizes ?? string.Empty).Split(Separator, StringSplitOptions.None);
        var countParts = (drawingCounts ?? string.Empty).Split(Separator, StringSplitOptions.None);
        if (sizeParts.Length != countParts.Length)
        {
            errorMessage = "图幅与数量的项目数不一致。";
            return false;
        }

        var result = new List<DesignConditionDrawingSpec>(sizeParts.Length);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        for (var index = 0; index < sizeParts.Length; index++)
        {
            var size = sizeParts[index].Trim();
            var countText = countParts[index].Trim();
            if (size.Length == 0)
            {
                errorMessage = $"第 {index + 1} 项图幅不能为空。";
                return false;
            }
            if (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count <= 0)
            {
                errorMessage = $"第 {index + 1} 项数量必须是大于 0 的整数。";
                return false;
            }
            if (!seen.Add(size))
            {
                errorMessage = $"图幅“{size}”重复，请删除或修改重复项。";
                return false;
            }
            total += count;
            if (total > int.MaxValue)
            {
                errorMessage = "图纸总张数超过允许范围。";
                return false;
            }
            result.Add(new DesignConditionDrawingSpec(size, count));
        }

        specifications = result;
        return true;
    }

    public static bool TrySerialize(
        IEnumerable<DesignConditionDrawingSpec> specifications,
        out DesignConditionDrawingStorage storage,
        out string errorMessage)
    {
        var items = specifications.ToArray();
        storage = new DesignConditionDrawingStorage(string.Empty, string.Empty, 0);
        if (items.Length == 0)
        {
            errorMessage = "请至少添加一种图幅。";
            return false;
        }
        if (items.Any(item => item.DrawingSize.Contains(Separator)))
        {
            errorMessage = "自定义图幅不能包含半角竖线 |。";
            return false;
        }

        var sizes = string.Join(Separator, items.Select(item => item.DrawingSize.Trim()));
        var counts = string.Join(Separator, items.Select(item => item.DrawingCount.ToString(CultureInfo.InvariantCulture)));
        if (!TryParse(sizes, counts, out var normalized, out errorMessage))
        {
            return false;
        }
        storage = new DesignConditionDrawingStorage(
            string.Join(Separator, normalized.Select(item => item.DrawingSize)),
            string.Join(Separator, normalized.Select(item => item.DrawingCount.ToString(CultureInfo.InvariantCulture))),
            normalized.Sum(item => item.DrawingCount));
        return true;
    }

    public static string GetSummary(IEnumerable<DesignConditionDrawingSpec> specifications) =>
        string.Join(" · ", specifications.Select(item => $"{item.DrawingSize} × {item.DrawingCount}"));
}
