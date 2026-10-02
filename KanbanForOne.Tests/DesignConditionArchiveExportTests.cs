using System.IO;
using ClosedXML.Excel;
using KanbanForOne.Modules.DesignConditions.Models;
using KanbanForOne.Modules.DesignConditions.Services;

namespace KanbanForOne.Tests;

public sealed class DesignConditionArchiveExportTests
{
    [Fact]
    public async Task Received_export_groups_by_receiving_discipline_and_preserves_both_dates()
    {
        var file = Path.Combine(Path.GetTempPath(), $"received-export-{Guid.NewGuid():N}.xlsx");
        try
        {
            var first = Entry("工艺", "设备", 2);
            var second = Entry("管道", "设备", 3);
            first.DrawingSize = "A1|A4";
            first.DrawingCounts = "1|1";
            await new DesignConditionExportService().ExportAsync(file, [first, second], DesignConditionArchiveKind.Received);

            using var workbook = new XLWorkbook(file);
            Assert.Equal(2, workbook.Worksheets.Count);
            var summary = workbook.Worksheet("接受条件汇总");
            Assert.Equal("接收专业", summary.Cell(1, 2).GetString());
            Assert.Equal("设备", summary.Cell(2, 2).GetString());
            Assert.Equal(5, summary.Cell(2, 3).GetValue<int>());
            Assert.Equal(4.125m, summary.Cell(2, 4).GetValue<decimal>());
            Assert.Equal("总计", summary.Cell(3, 1).GetString());

            var details = workbook.Worksheet("接受条件明细");
            Assert.Equal("接受日期", details.Cell(1, 1).GetString());
            Assert.Equal("提出日期", details.Cell(1, 15).GetString());
            Assert.Equal(4, details.LastRowUsed()!.RowNumber());
            Assert.Equal(first.ReceivedDate!.Value, details.Cell(2, 1).GetDateTime());
            Assert.Equal(first.IssuedDate, details.Cell(2, 15).GetDateTime());
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [Fact]
    public async Task Export_rejects_mixed_categories_before_creating_a_file()
    {
        var file = Path.Combine(Path.GetTempPath(), $"mixed-export-{Guid.NewGuid():N}.xlsx");
        var received = Entry("工艺", "设备", 1);
        var issued = Entry("工艺", "设备", 1);
        issued.ArchiveKind = DesignConditionArchiveKind.Issued;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new DesignConditionExportService().ExportAsync(file, [issued, received]));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task Received_export_requires_a_received_date()
    {
        var file = Path.Combine(Path.GetTempPath(), $"invalid-export-{Guid.NewGuid():N}.xlsx");
        var received = Entry("工艺", "设备", 1);
        received.ReceivedDate = null;
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new DesignConditionExportService().ExportAsync(file, [received], DesignConditionArchiveKind.Received));
        Assert.False(File.Exists(file));
    }

    private static DesignConditionEntry Entry(string issuing, string receiving, int count) => new()
    {
        ArchiveKind = DesignConditionArchiveKind.Received,
        ProjectNumber = "P100",
        IssuingDiscipline = issuing,
        ReceivingDiscipline = receiving,
        Receiver = "测试",
        ConditionName = issuing + "条件",
        IssuedDate = new DateTime(2026, 9, 28),
        ReceivedDate = new DateTime(2026, 10, 1),
        DrawingSize = "A1",
        DrawingCounts = count.ToString(),
        DrawingCount = count
    };
}
