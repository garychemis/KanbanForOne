# Design Condition Flat Excel Summary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the hierarchical design-condition Excel summary with one flat row per project and issuing-discipline combination plus a grand-total row.

**Architecture:** Keep export validation, folded-A1 calculation, and the detail worksheet unchanged. Simplify only `AddSummary` and its row writer so the summary worksheet has four columns and groups directly by `(ProjectNumber, IssuingDiscipline)`.

**Tech Stack:** .NET 8, C# 12, ClosedXML, xUnit v3

---

## File map

- Modify `Modules/DesignConditions/Services/DesignConditionExportService.cs`: flat grouping, four-column header, and total row.
- Modify `KanbanForOne.Tests/DesignConditionModuleTests.cs`: grouping, sorting, totals, four-column layout, empty summary, and detail regressions.

### Task 1: Flat project and issuing-discipline summary

**Files:**
- Modify: `Modules/DesignConditions/Services/DesignConditionExportService.cs:60-99,169-198`
- Test: `KanbanForOne.Tests/DesignConditionModuleTests.cs:482-548`

- [ ] **Step 1: Rewrite the workbook test for the required flat layout**

Keep the original attached `P100 / 工艺` entry. Add three entries before export:

```csharp
var sameGroup = CreateEntry(DateTime.Today.AddDays(1));
sameGroup.Id = Guid.NewGuid();
sameGroup.ConditionName = "加长图幅条件";
sameGroup.DrawingSize = "A1+0.25";
sameGroup.DrawingCounts = "1";
sameGroup.DrawingCount = 1;

var otherDiscipline = CreateEntry(DateTime.Today.AddDays(2));
otherDiscipline.Id = Guid.NewGuid();
otherDiscipline.IssuingDiscipline = "电气";
otherDiscipline.DrawingSize = "A2";
otherDiscipline.DrawingCounts = "2";
otherDiscipline.DrawingCount = 2;

var otherProject = CreateEntry(DateTime.Today.AddDays(3));
otherProject.Id = Guid.NewGuid();
otherProject.ProjectNumber = "P200";
otherProject.DrawingSize = "A0";
otherProject.DrawingCounts = "1";
otherProject.DrawingCount = 1;

await new DesignConditionExportService().ExportAsync(
    output,
    [entry, sameGroup, otherDiscipline, otherProject]);
```

Replace hierarchical summary assertions with:

```csharp
Assert.Equal(["项目", "提出专业", "张数", "折A1张数"],
    summary.Row(1).Cells(1, 4).Select(cell => cell.GetString()));
Assert.Equal(4, summary.LastColumnUsed()!.ColumnNumber());

Assert.Equal("P100", summary.Cell(2, 1).GetString());
Assert.Equal("工艺", summary.Cell(2, 2).GetString());
Assert.Equal(7, summary.Cell(2, 3).GetValue<int>());
Assert.Equal(2.875m, summary.Cell(2, 4).GetValue<decimal>());

Assert.Equal("P100", summary.Cell(3, 1).GetString());
Assert.Equal("电气", summary.Cell(3, 2).GetString());
Assert.Equal(2, summary.Cell(3, 3).GetValue<int>());
Assert.Equal(1m, summary.Cell(3, 4).GetValue<decimal>());

Assert.Equal("P200", summary.Cell(4, 1).GetString());
Assert.Equal("工艺", summary.Cell(4, 2).GetString());
Assert.Equal(1, summary.Cell(4, 3).GetValue<int>());
Assert.Equal(2m, summary.Cell(4, 4).GetValue<decimal>());

Assert.Equal("总计", summary.Cell(5, 1).GetString());
Assert.Equal(string.Empty, summary.Cell(5, 2).GetString());
Assert.Equal(10, summary.Cell(5, 3).GetValue<int>());
Assert.Equal(5.875m, summary.Cell(5, 4).GetValue<decimal>());
Assert.True(summary.Cell(5, 1).Style.Font.Bold);
Assert.Equal("0.###", summary.Column(4).Style.NumberFormat.Format);
```

Retain the existing detail worksheet assertions.

- [ ] **Step 2: Run the focused test and verify RED**

```powershell
dotnet test .\KanbanForOne.Tests\KanbanForOne.Tests.csproj --filter "FullyQualifiedName~Excel_contains" --no-restore
```

Expected: FAIL because the current summary begins with `层级` and has nine columns.

- [ ] **Step 3: Implement the flat summary**

Replace `AddSummary` with:

```csharp
private static void AddSummary(XLWorkbook workbook, IReadOnlyList<DesignConditionEntry> entries)
{
    var sheet = workbook.Worksheets.Add("设计条件汇总");
    string[] headers = ["项目", "提出专业", "张数", "折A1张数"];
    WriteHeader(sheet, headers);
    var row = 2;
    var groups = entries
        .GroupBy(item => (item.ProjectNumber, item.IssuingDiscipline))
        .OrderBy(group => group.Key.ProjectNumber, StringComparer.OrdinalIgnoreCase)
        .ThenBy(group => group.Key.IssuingDiscipline, StringComparer.OrdinalIgnoreCase);
    foreach (var group in groups)
    {
        WriteSummaryRow(sheet, row++, group.Key.ProjectNumber, group.Key.IssuingDiscipline,
            group.Sum(item => item.DrawingCount), GetFoldedA1Total(group));
    }
    WriteSummaryRow(sheet, row, "总计", string.Empty,
        entries.Sum(item => item.DrawingCount), GetFoldedA1Total(entries), isTotal: true);
    sheet.Range(1, 1, row, headers.Length).SetAutoFilter();
    sheet.SheetView.FreezeRows(1);
    sheet.Columns().AdjustToContents();
    sheet.Column(4).Style.NumberFormat.Format = "0.###";
}
```

Replace the hierarchical writer with:

```csharp
private static void WriteSummaryRow(
    IXLWorksheet sheet,
    int row,
    string project,
    string issuingDiscipline,
    int drawingCount,
    decimal foldedA1,
    bool isTotal = false)
{
    sheet.Cell(row, 1).Value = project;
    sheet.Cell(row, 2).Value = issuingDiscipline;
    sheet.Cell(row, 3).Value = drawingCount;
    sheet.Cell(row, 4).Value = foldedA1;
    if (isTotal)
    {
        sheet.Range(row, 1, row, 4).Style.Font.Bold = true;
    }
}
```

- [ ] **Step 4: Run all Excel tests and verify GREEN**

```powershell
dotnet test .\KanbanForOne.Tests\KanbanForOne.Tests.csproj --filter "FullyQualifiedName~Excel_" --no-restore
```

Expected: all selected tests pass, including invalid-export and unchanged detail-sheet tests.

- [ ] **Step 5: Commit**

```powershell
git add Modules/DesignConditions/Services/DesignConditionExportService.cs KanbanForOne.Tests/DesignConditionModuleTests.cs
git commit -m "feat: flatten design condition Excel summary"
```

### Task 2: Empty summary and full regression verification

**Files:**
- Modify: `KanbanForOne.Tests/DesignConditionModuleTests.cs`

- [ ] **Step 1: Write the empty-summary regression test**

```csharp
[Fact]
public async Task Excel_summary_with_no_entries_contains_zero_total()
{
    var root = CreateRoot();
    try
    {
        var output = Path.Combine(root, "empty.xlsx");
        await new DesignConditionExportService().ExportAsync(output, []);
        using var workbook = new XLWorkbook(output);
        var summary = workbook.Worksheet("设计条件汇总");
        Assert.Equal("总计", summary.Cell(2, 1).GetString());
        Assert.Equal(0, summary.Cell(2, 3).GetValue<int>());
        Assert.Equal(0m, summary.Cell(2, 4).GetValue<decimal>());
    }
    finally
    {
        Directory.Delete(root, true);
    }
}
```

- [ ] **Step 2: Run the empty-summary test**

```powershell
dotnet test .\KanbanForOne.Tests\KanbanForOne.Tests.csproj --filter "FullyQualifiedName~Excel_summary_with_no_entries" --no-restore
```

Expected: PASS after Task 1 because the total row is always written.

- [ ] **Step 3: Run full verification**

```powershell
dotnet format .\KanbanForOne.sln --verify-no-changes --no-restore
dotnet test .\KanbanForOne.sln --no-restore
dotnet build .\KanbanForOne.sln --no-restore
```

Expected: formatting exits 0, all tests pass, and build reports zero errors.

- [ ] **Step 4: Commit the empty-summary test**

```powershell
git add KanbanForOne.Tests/DesignConditionModuleTests.cs
git commit -m "test: cover empty design condition Excel summary"
```

- [ ] **Step 5: Inspect final repository state**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors and no uncommitted files.
