# Design Condition Folded A1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restrict design-condition drawing sizes to nine fixed choices, calculate folded-A1 totals at runtime, reject unconvertible legacy data during Excel export, and add folded-A1 values to every summary level and the grand-total row.

**Architecture:** Add one runtime catalog as the sole source of drawing-size names, ordering, validation, and decimal conversion factors. The editor consumes that catalog and sanitizes legacy values only in copied UI rows; the export service performs a full preflight and calculates grouped totals without changing persisted entities or the database schema.

**Tech Stack:** .NET 8, C# 12, WPF, ClosedXML, xUnit v3

---

## File map

- Create `Modules/DesignConditions/Models/DesignConditionDrawingSizeCatalog.cs`: fixed definitions and folded-A1 calculation.
- Modify `Modules/DesignConditions/ViewModels/DesignConditionEditorViewModel.cs`: fixed candidates, legacy UI sanitization, and validation.
- Modify `Modules/DesignConditions/Views/DesignConditionEditorDialog.xaml`: non-editable drawing-size ComboBox.
- Modify `Modules/DesignConditions/Views/DesignConditionEditorDialog.xaml.cs`: remove obsolete editable-text behavior.
- Modify `Modules/DesignConditions/ViewModels/DesignConditionViewModel.cs`: stop loading historical size candidates and preflight export.
- Modify `Modules/DesignConditions/Repositories/DesignConditionRepository.cs`: stop persisting dynamic size options.
- Modify `Modules/DesignConditions/Services/DesignConditionExportService.cs`: validation boundary and folded-A1 summary.
- Modify `KanbanForOne.Tests/DesignConditionModuleTests.cs`: focused and regression tests.

### Task 1: Fixed drawing-size catalog

**Files:**
- Create: `Modules/DesignConditions/Models/DesignConditionDrawingSizeCatalog.cs`
- Test: `KanbanForOne.Tests/DesignConditionModuleTests.cs`

- [ ] **Step 1: Write the failing catalog tests**

```csharp
[Fact]
public void Drawing_size_catalog_has_required_order_and_factors()
{
    Assert.Equal(
        ["A4", "A3", "A2", "A1", "A1+0.25", "A1+0.5", "A0", "A0+0.25", "A0+0.5"],
        DesignConditionDrawingSizeCatalog.Names);
    decimal[] expected = [0.125m, 0.25m, 0.5m, 1m, 1.25m, 1.5m, 2m, 2.5m, 3m];
    Assert.Equal(expected, DesignConditionDrawingSizeCatalog.Definitions.Select(item => item.FoldedA1Factor));
}

[Fact]
public void Drawing_size_catalog_calculates_folded_a1_and_rejects_unknown_size()
{
    Assert.True(DesignConditionDrawingSizeCatalog.TryCalculate(
        [new("A4", 5), new("A1+0.25", 2), new("A0", 1)], out var total, out var error));
    Assert.Equal(5.125m, total);
    Assert.Equal(string.Empty, error);
    Assert.False(DesignConditionDrawingSizeCatalog.TryCalculate([new("自定义", 1)], out _, out error));
    Assert.Contains("自定义", error);
}
```

- [ ] **Step 2: Run the tests and verify RED**

Run:

```powershell
dotnet test .\KanbanForOne.Tests\KanbanForOne.Tests.csproj --filter "FullyQualifiedName~Drawing_size_catalog" --no-restore
```

Expected: compilation fails because `DesignConditionDrawingSizeCatalog` does not exist.

- [ ] **Step 3: Implement the minimal catalog**

```csharp
namespace KanbanForOne.Modules.DesignConditions.Models;

public sealed record DesignConditionDrawingSizeDefinition(string Name, decimal FoldedA1Factor);

public static class DesignConditionDrawingSizeCatalog
{
    public static IReadOnlyList<DesignConditionDrawingSizeDefinition> Definitions { get; } =
    [
        new("A4", 0.125m), new("A3", 0.25m), new("A2", 0.5m), new("A1", 1m),
        new("A1+0.25", 1.25m), new("A1+0.5", 1.5m), new("A0", 2m),
        new("A0+0.25", 2.5m), new("A0+0.5", 3m)
    ];

    public static IReadOnlyList<string> Names { get; } = Definitions.Select(item => item.Name).ToArray();

    public static bool TryGetDefinition(string? value, out DesignConditionDrawingSizeDefinition definition)
    {
        definition = Definitions.FirstOrDefault(item =>
            string.Equals(item.Name, value?.Trim(), StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }

    public static bool TryCalculate(IEnumerable<DesignConditionDrawingSpec> specifications, out decimal total, out string error)
    {
        total = 0m;
        error = string.Empty;
        foreach (var specification in specifications)
        {
            if (!TryGetDefinition(specification.DrawingSize, out var definition))
            {
                error = $"图幅“{specification.DrawingSize}”无法计算折 A1。";
                return false;
            }
            total += specification.DrawingCount * definition.FoldedA1Factor;
        }
        return true;
    }
}
```

- [ ] **Step 4: Run focused tests and verify GREEN**

```powershell
dotnet test .\KanbanForOne.Tests\KanbanForOne.Tests.csproj --filter "FullyQualifiedName~Drawing_size_catalog|FullyQualifiedName~Drawing_codec" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Modules/DesignConditions/Models/DesignConditionDrawingSizeCatalog.cs KanbanForOne.Tests/DesignConditionModuleTests.cs
git commit -m "feat: add fixed drawing size conversion catalog"
```

### Task 2: Fixed editor choices and safe legacy handling

**Files:**
- Modify: `Modules/DesignConditions/ViewModels/DesignConditionEditorViewModel.cs`
- Modify: `Modules/DesignConditions/ViewModels/DesignConditionViewModel.cs`
- Modify: `Modules/DesignConditions/Views/DesignConditionEditorDialog.xaml`
- Modify: `Modules/DesignConditions/Views/DesignConditionEditorDialog.xaml.cs`
- Modify: `Modules/DesignConditions/Repositories/DesignConditionRepository.cs`
- Test: `KanbanForOne.Tests/DesignConditionModuleTests.cs`

- [ ] **Step 1: Write failing editor tests**

```csharp
[Fact]
public void Editor_clears_unknown_legacy_size_without_mutating_source()
{
    var source = new DesignConditionEntry
    {
        ProjectNumber = "P100", IssuingDiscipline = "工艺", ReceivingDiscipline = "设备",
        Receiver = "张三", IssuedDate = new(2026, 8, 20), ConditionName = "旧图幅",
        DrawingSize = "A1|自定义", DrawingCounts = "1|4", DrawingCount = 5
    };
    var editor = new DesignConditionEditorViewModel(source, DateTime.Today, ["工艺", "设备"], ["张三"]);
    Assert.Equal(DesignConditionDrawingSizeCatalog.Names, editor.DrawingSizes);
    Assert.Equal("A1", editor.DrawingRows[0].DrawingSize);
    Assert.Equal(string.Empty, editor.DrawingRows[1].DrawingSize);
    Assert.Equal("4", editor.DrawingRows[1].DrawingCountText);
    Assert.Contains("自定义", editor.ValidationMessage);
    Assert.Equal("A1|自定义", source.DrawingSize);
}

[Fact]
public void Editor_rejects_programmatically_assigned_unknown_size()
{
    var editor = CreateValidEditor();
    editor.DrawingRows[0].DrawingSize = "自定义";
    Assert.False(editor.TryBuild(out _));
    Assert.Contains("固定图幅", editor.ValidationMessage);
}
```

Update existing constructor calls and add this helper:

```csharp
private static DesignConditionEditorViewModel CreateValidEditor()
{
    var editor = new DesignConditionEditorViewModel(null, DateTime.Today, ["工艺", "设备"], ["张三"])
    {
        ProjectNumber = "P100",
        IssuingDiscipline = "工艺",
        ReceivingDiscipline = "设备",
        Receiver = "张三",
        ConditionName = "固定图幅条件"
    };
    editor.DrawingRows[0].DrawingSize = "A1";
    editor.DrawingRows[0].DrawingCountText = "1";
    return editor;
}
```

- [ ] **Step 2: Run editor tests and verify RED**

```powershell
dotnet test .\KanbanForOne.Tests\KanbanForOne.Tests.csproj --filter "FullyQualifiedName~Editor_" --no-restore
```

Expected: compilation fails because the fixed four-argument editor API is missing.

- [ ] **Step 3: Implement editor model behavior**

Remove the `drawingSizes` constructor argument and set:

```csharp
DrawingSizes = DesignConditionDrawingSizeCatalog.Names;
```

When loading rows, canonicalize supported values and replace unsupported values with `string.Empty`, retain their count, collect original names, then set:

```csharp
ValidationMessage = $"记录“{ConditionName}”包含无法计算折 A1 的历史图幅：{string.Join("、", unknownSizes.Distinct(StringComparer.OrdinalIgnoreCase))}。请重新选择固定图幅后保存。";
```

Before serializing each row, validate and canonicalize:

```csharp
if (!DesignConditionDrawingSizeCatalog.TryGetDefinition(size, out var definition))
{
    errorMessage = $"第 {index + 1} 行：请选择固定图幅。";
    return false;
}
specifications.Add(new DesignConditionDrawingSpec(definition.Name, count));
```

- [ ] **Step 4: Implement WPF and repository wiring**

In `DesignConditionViewModel.ShowEditorAsync`, remove historical size-option assembly and use the four-argument constructor. Replace the editing control with:

```xml
<ComboBox IsEditable="False"
          ItemsSource="{Binding DataContext.DrawingSizes, RelativeSource={RelativeSource AncestorType=Window}}"
          SelectedItem="{Binding DrawingSize, UpdateSourceTrigger=PropertyChanged}"
          HorizontalContentAlignment="Center" AutomationProperties.Name="条件图幅"
          Style="{StaticResource ConditionComboBoxStyle}" Height="34" />
```

Remove `OnDrawingComboBoxLoaded`. In `DesignConditionRepository.SaveAggregateAsync`, retain discipline and receiver option insertion but remove `DrawingSize` option insertion.

- [ ] **Step 5: Run editor and repository tests and verify GREEN**

```powershell
dotnet test .\KanbanForOne.Tests\KanbanForOne.Tests.csproj --filter "FullyQualifiedName~Editor_|FullyQualifiedName~Repository" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 6: Commit**

```powershell
git add Modules/DesignConditions/ViewModels/DesignConditionEditorViewModel.cs Modules/DesignConditions/ViewModels/DesignConditionViewModel.cs Modules/DesignConditions/Views/DesignConditionEditorDialog.xaml Modules/DesignConditions/Views/DesignConditionEditorDialog.xaml.cs Modules/DesignConditions/Repositories/DesignConditionRepository.cs KanbanForOne.Tests/DesignConditionModuleTests.cs
git commit -m "feat: restrict design condition drawing sizes"
```

### Task 3: Excel preflight and folded-A1 summary

**Files:**
- Modify: `Modules/DesignConditions/Services/DesignConditionExportService.cs`
- Modify: `Modules/DesignConditions/ViewModels/DesignConditionViewModel.cs`
- Test: `KanbanForOne.Tests/DesignConditionModuleTests.cs`

- [ ] **Step 1: Write failing invalid-export test**

```csharp
[Fact]
public async Task Excel_export_rejects_unknown_size_without_creating_file()
{
    var root = CreateRoot();
    try
    {
        var entry = CreateEntry(DateTime.Today);
        entry.DrawingSize = "旧图幅";
        entry.DrawingCounts = "2";
        entry.DrawingCount = 2;
        var output = Path.Combine(root, "invalid.xlsx");
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new DesignConditionExportService().ExportAsync(output, [entry]));
        Assert.Contains(entry.ConditionName, error.Message);
        Assert.Contains("旧图幅", error.Message);
        Assert.False(File.Exists(output));
    }
    finally { Directory.Delete(root, true); }
}
```

- [ ] **Step 2: Run invalid-export test and verify RED**

```powershell
dotnet test .\KanbanForOne.Tests\KanbanForOne.Tests.csproj --filter "FullyQualifiedName~Excel_export_rejects_unknown_size" --no-restore
```

Expected: test fails because a workbook is created.

- [ ] **Step 3: Implement reusable preflight**

```csharp
public static bool TryValidate(IReadOnlyList<DesignConditionEntry> entries, out string errorMessage)
{
    var errors = new List<string>();
    foreach (var item in entries)
    {
        if (!DesignConditionDrawingCodec.TryParse(item.DrawingSize, item.EffectiveDrawingCounts, out var specs, out var parseError))
        {
            errors.Add($"{item.ProjectNumber} / {item.ConditionName}：{parseError}");
            continue;
        }
        if (!DesignConditionDrawingSizeCatalog.TryCalculate(specs, out _, out var conversionError))
            errors.Add($"{item.ProjectNumber} / {item.ConditionName}：{conversionError}");
    }
    errorMessage = errors.Count == 0 ? string.Empty
        : "无法导出设计条件，以下记录不能计算折 A1：" + Environment.NewLine + string.Join(Environment.NewLine, errors);
    return errors.Count == 0;
}
```

Call this before creating `XLWorkbook` and throw `InvalidDataException` on failure. Call the same method in `DesignConditionViewModel.ExportAsync` before showing `SaveFileDialog`; notify and return on failure.

- [ ] **Step 4: Run invalid-export test and verify GREEN**

Run the Step 2 command. Expected: the test passes and no file exists.

- [ ] **Step 5: Write failing summary-column assertions**

Extend the existing workbook test with a second condition. The original `A1 × 1 + A4 × 5` contributes six sheets and `1.625` folded A1; the second condition contributes `A1+0.25 × 1`, producing seven sheets and `2.875` folded A1 overall:

```csharp
var second = CreateEntry(DateTime.Today);
second.Id = Guid.NewGuid();
second.ConditionName = "加长图幅条件";
second.DrawingSize = "A1+0.25";
second.DrawingCounts = "1";
second.DrawingCount = 1;
await new DesignConditionExportService().ExportAsync(output, [entry, second]);
```

Then assert:

```csharp
Assert.Equal("折A1", summary.Cell(1, 8).GetString());
Assert.Equal(2.875m, summary.Cell(2, 8).GetValue<decimal>());
var totalRow = summary.Column(1).CellsUsed().Single(cell => cell.GetString() == "总计").Address.RowNumber;
Assert.Equal(7, summary.Cell(totalRow, 7).GetValue<int>());
Assert.Equal(2.875m, summary.Cell(totalRow, 8).GetValue<decimal>());
Assert.Equal("0.###", summary.Column(8).Style.NumberFormat.Format);
```

- [ ] **Step 6: Run summary test and verify RED**

```powershell
dotnet test .\KanbanForOne.Tests\KanbanForOne.Tests.csproj --filter "FullyQualifiedName~Excel_contains_hierarchical_summary" --no-restore
```

Expected: column 8 is still “附件数”, so the test fails.

- [ ] **Step 7: Implement folded-A1 summary values**

Use these headers:

```csharp
string[] headers = ["层级", "项目", "提出专业", "接收专业", "条件名称", "记录数", "图纸数量", "折A1", "附件数"];
```

Calculate each group after preflight:

```csharp
private static decimal GetFoldedA1Total(IEnumerable<DesignConditionEntry> entries)
{
    decimal total = 0m;
    foreach (var entry in entries)
    {
        DesignConditionDrawingSizeCatalog.TryCalculate(entry.DrawingSpecifications, out var value, out _);
        total += value;
    }
    return total;
}
```

Pass the decimal into every `WriteSummaryRow`, write it to column 8, shift attachments to column 9, bold through column 9, and set:

```csharp
sheet.Column(8).Style.NumberFormat.Format = "0.###";
```

- [ ] **Step 8: Run all Excel tests and verify GREEN**

```powershell
dotnet test .\KanbanForOne.Tests\KanbanForOne.Tests.csproj --filter "FullyQualifiedName~Excel_" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 9: Commit**

```powershell
git add Modules/DesignConditions/Services/DesignConditionExportService.cs Modules/DesignConditions/ViewModels/DesignConditionViewModel.cs KanbanForOne.Tests/DesignConditionModuleTests.cs
git commit -m "feat: export folded A1 design condition totals"
```

### Task 4: Full regression verification

**Files:**
- Modify only files already listed if a verified regression is caused by this feature.

- [ ] **Step 1: Verify formatting**

```powershell
dotnet format .\KanbanForOne.sln --verify-no-changes --no-restore
```

Expected: exit code 0.

- [ ] **Step 2: Run all tests**

```powershell
dotnet test .\KanbanForOne.sln --no-restore
```

Expected: zero failed tests.

- [ ] **Step 3: Build the solution**

```powershell
dotnet build .\KanbanForOne.sln --no-restore
```

Expected: zero build errors.

- [ ] **Step 4: Inspect repository state**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors and no uncommitted implementation files.

- [ ] **Step 5: Report evidence**

Report the exact test count, build result, commits, fixed candidate order, legacy warning behavior, and the Excel total-row values asserted by tests.
