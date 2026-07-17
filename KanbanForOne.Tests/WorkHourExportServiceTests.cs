using ClosedXML.Excel;
using KanbanForOne.Models;
using KanbanForOne.Services;

namespace KanbanForOne.Tests;

public sealed class WorkHourExportServiceTests
{
    [Fact]
    public async Task Export_creates_summary_and_detail_sheets_with_numeric_hours()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "KanbanForOne.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            var outputPath = Path.Combine(testRoot, "summary.xlsx");
            var summary = new WorkHourSummaryItem("P1001", "Piping", "Design", 375, 2);
            var detail = new WorkHourEntry
            {
                WorkDate = new DateTime(2026, 7, 17),
                ProjectNumber = "P1001",
                Discipline = "Piping",
                WorkActivity = "Design",
                HourUnits = 375,
                Remark = "Checked",
                CreatedAt = new DateTime(2026, 7, 17, 9, 0, 0),
                UpdatedAt = new DateTime(2026, 7, 17, 9, 0, 0)
            };

            await new WorkHourExportService().ExportAsync(
                outputPath,
                new DateTime(2026, 7, 1),
                new DateTime(2026, 7, 31),
                [summary],
                [detail]);

            using var workbook = new XLWorkbook(outputPath);
            Assert.Equal(2, workbook.Worksheets.Count);
            var summarySheet = workbook.Worksheet("人工时汇总");
            Assert.Equal("2026.07.01~2026.07.31", summarySheet.Cell(2, 1).GetString());
            Assert.Equal(3.75, summarySheet.Cell(2, 5).GetDouble(), precision: 2);

            var detailSheet = workbook.Worksheet("人工时明细");
            Assert.Equal(new DateTime(2026, 7, 17), detailSheet.Cell(2, 1).GetDateTime());
            Assert.Equal(3.75, detailSheet.Cell(2, 5).GetDouble(), precision: 2);
            Assert.Equal("Checked", detailSheet.Cell(2, 6).GetString());
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
