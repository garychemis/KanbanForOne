namespace KanbanForOne.Modules.DesignConditions.Models;

public sealed record DesignConditionDrawingSizeDefinition(string Name, decimal FoldedA1Factor);

public static class DesignConditionDrawingSizeCatalog
{
    public static IReadOnlyList<DesignConditionDrawingSizeDefinition> Definitions { get; } =
    [
        new("A4", 0.125m),
        new("A3", 0.25m),
        new("A2", 0.5m),
        new("A1", 1m),
        new("A1+0.25", 1.25m),
        new("A1+0.5", 1.5m),
        new("A0", 2m),
        new("A0+0.25", 2.5m),
        new("A0+0.5", 3m)
    ];

    public static IReadOnlyList<string> Names { get; } = Definitions.Select(item => item.Name).ToArray();

    public static bool TryGetDefinition(string? drawingSize, out DesignConditionDrawingSizeDefinition definition)
    {
        definition = Definitions.FirstOrDefault(item =>
            string.Equals(item.Name, drawingSize?.Trim(), StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }

    public static bool TryCalculate(
        IEnumerable<DesignConditionDrawingSpec> specifications,
        out decimal foldedA1,
        out string errorMessage)
    {
        foldedA1 = 0m;
        errorMessage = string.Empty;
        foreach (var specification in specifications)
        {
            if (!TryGetDefinition(specification.DrawingSize, out var definition))
            {
                errorMessage = $"图幅“{specification.DrawingSize}”无法计算折 A1。";
                return false;
            }

            foldedA1 += specification.DrawingCount * definition.FoldedA1Factor;
        }

        return true;
    }
}
