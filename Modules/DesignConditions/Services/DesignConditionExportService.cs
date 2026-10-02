using System.IO;
using ClosedXML.Excel;
using KanbanForOne.Modules.DesignConditions.Models;

namespace KanbanForOne.Modules.DesignConditions.Services;

public sealed class DesignConditionExportService
{
    public static bool TryValidate(IReadOnlyList<DesignConditionEntry> entries, out string errorMessage)
    {
        var errors = new List<string>();
        foreach (var item in entries)
        {
            if (!DesignConditionDrawingCodec.TryParse(
                    item.DrawingSize,
                    item.EffectiveDrawingCounts,
                    out var specifications,
                    out var parseError))
            {
                errors.Add($"{item.ProjectNumber} / {item.ConditionName}：{parseError}");
                continue;
            }

            if (!DesignConditionDrawingSizeCatalog.TryCalculate(specifications, out _, out var conversionError))
            {
                errors.Add($"{item.ProjectNumber} / {item.ConditionName}：{conversionError}");
            }
        }

        errorMessage = errors.Count == 0
            ? string.Empty
            : "无法导出设计条件，以下记录不能计算折 A1：" + Environment.NewLine +
              string.Join(Environment.NewLine, errors);
        return errors.Count == 0;
    }

    public Task ExportAsync(string filePath, IReadOnlyList<DesignConditionEntry> entries,
        DesignConditionArchiveKind archiveKind = DesignConditionArchiveKind.Issued)
    {
        return Task.Run(() => Export(filePath, entries, archiveKind));
    }

    private static void Export(string filePath, IReadOnlyList<DesignConditionEntry> entries, DesignConditionArchiveKind archiveKind)
    {
        if (!Enum.IsDefined(archiveKind) || entries.Any(item => item.ArchiveKind != archiveKind))
        {
            throw new InvalidDataException("请分别导出提出条件和接受条件，不能混合两类归档记录。");
        }
        if (entries.Any(item => item.IsReceived && !item.ReceivedDate.HasValue))
        {
            throw new InvalidDataException("接受条件缺少接受日期，请补全后再导出。");
        }
        if (!TryValidate(entries, out var validationError))
        {
            throw new InvalidDataException(validationError);
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        using var workbook = new XLWorkbook();
        AddSummary(workbook, entries, archiveKind);
        AddDetails(workbook, entries, archiveKind);
        workbook.SaveAs(filePath);
    }

    private static void AddSummary(XLWorkbook workbook, IReadOnlyList<DesignConditionEntry> entries, DesignConditionArchiveKind archiveKind)
    {
        var received = archiveKind == DesignConditionArchiveKind.Received;
        var sheet = workbook.Worksheets.Add(received ? "接受条件汇总" : "设计条件汇总");
        string[] headers = ["项目", received ? "接收专业" : "提出专业", "张数", "折A1张数"];
        WriteHeader(sheet, headers, received);
        var row = 2;
        var groups = entries
            .GroupBy(item => (item.ProjectNumber, Discipline: received ? item.ReceivingDiscipline : item.IssuingDiscipline))
            .OrderBy(group => group.Key.ProjectNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.Discipline, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            WriteSummaryRow(
                sheet,
                row++,
                group.Key.ProjectNumber,
                group.Key.Discipline,
                group.Sum(item => item.DrawingCount),
                GetFoldedA1Total(group));
        }
        WriteSummaryRow(
            sheet,
            row,
            "总计",
            string.Empty,
            entries.Sum(item => item.DrawingCount),
            GetFoldedA1Total(entries),
            isTotal: true);
        sheet.Range(1, 1, row, headers.Length).SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        sheet.Column(4).Style.NumberFormat.Format = "0.###";
    }

    private static void AddDetails(XLWorkbook workbook, IReadOnlyList<DesignConditionEntry> entries, DesignConditionArchiveKind archiveKind)
    {
        var received = archiveKind == DesignConditionArchiveKind.Received;
        var sheet = workbook.Worksheets.Add(received ? "接受条件明细" : "设计条件明细");
        string[] headers = [received ? "接受日期" : "提出日期", "项目", "提出专业", "接收专业", "接收人", "条件名称", "版次", "图幅", "图纸数量", "条件总张数", "附件数", "附件文件名", "创建时间", "更新时间"];
        if (received) headers = [.. headers, "提出日期"];
        WriteHeader(sheet, headers, received);
        var ordered = entries.OrderBy(item => item.ArchiveDate).ThenBy(item => item.ProjectNumber).ThenBy(item => item.ConditionName).ToArray();
        var detailRows = ordered.SelectMany(item => GetDetailSpecifications(item).Select(specification => (Item: item, Specification: specification))).ToArray();
        for (var index = 0; index < detailRows.Length; index++)
        {
            var (item, specification) = detailRows[index];
            var row = index + 2;
            sheet.Cell(row, 1).Value = item.ArchiveDate;
            sheet.Cell(row, 2).Value = item.ProjectNumber;
            sheet.Cell(row, 3).Value = item.IssuingDiscipline;
            sheet.Cell(row, 4).Value = item.ReceivingDiscipline;
            sheet.Cell(row, 5).Value = item.Receiver;
            sheet.Cell(row, 6).Value = item.ConditionName;
            sheet.Cell(row, 7).Value = item.Revision;
            sheet.Cell(row, 8).Value = specification.DrawingSize;
            sheet.Cell(row, 9).Value = specification.DrawingCount;
            sheet.Cell(row, 10).Value = item.DrawingCount;
            sheet.Cell(row, 11).Value = item.AttachmentCount;
            sheet.Cell(row, 12).Value = string.Join(Environment.NewLine, item.Attachments.Select(file => file.OriginalFileName));
            sheet.Cell(row, 13).Value = item.CreatedAt;
            sheet.Cell(row, 14).Value = item.UpdatedAt;
            if (received) sheet.Cell(row, 15).Value = item.IssuedDate;
        }
        sheet.Column(1).Style.DateFormat.Format = "yyyy.MM.dd";
        if (received) sheet.Column(15).Style.DateFormat.Format = "yyyy.MM.dd";
        sheet.Columns(13, 14).Style.DateFormat.Format = "yyyy.MM.dd HH:mm";
        sheet.Column(12).Style.Alignment.WrapText = true;
        sheet.Range(1, 1, Math.Max(detailRows.Length + 1, 1), headers.Length).SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        sheet.Column(6).Width = Math.Clamp(sheet.Column(6).Width, 18, 42);
        sheet.Column(12).Width = Math.Clamp(sheet.Column(12).Width, 18, 48);
    }

    private static IReadOnlyList<DesignConditionDrawingSpec> GetDetailSpecifications(DesignConditionEntry item)
    {
        if (item.DrawingSpecifications.Count > 0) return item.DrawingSpecifications;
        // 无规格旧记录：图幅为空或数量为 0 时无意义，不导出明细行。
        return string.IsNullOrWhiteSpace(item.DrawingSize) || item.DrawingCount <= 0
            ? []
            : [new DesignConditionDrawingSpec(item.DrawingSize, item.DrawingCount)];
    }

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

    private static void WriteHeader(IXLWorksheet sheet, IReadOnlyList<string> headers, bool received)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }
        var header = sheet.Range(1, 1, 1, headers.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml(received ? "#EDE7F6" : "#E7ECE9");
        header.Style.Font.FontColor = XLColor.FromHtml(received ? "#5B478E" : "#2F3A36");
        sheet.Row(1).Height = 24;
    }

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
}
