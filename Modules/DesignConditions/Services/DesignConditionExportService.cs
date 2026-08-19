using System.IO;
using ClosedXML.Excel;
using KanbanForOne.Modules.DesignConditions.Models;

namespace KanbanForOne.Modules.DesignConditions.Services;

public sealed class DesignConditionExportService
{
    public Task ExportAsync(string filePath, IReadOnlyList<DesignConditionEntry> entries)
    {
        return Task.Run(() => Export(filePath, entries));
    }

    private static void Export(string filePath, IReadOnlyList<DesignConditionEntry> entries)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        using var workbook = new XLWorkbook();
        AddSummary(workbook, entries);
        AddDetails(workbook, entries);
        workbook.SaveAs(filePath);
    }

    private static void AddSummary(XLWorkbook workbook, IReadOnlyList<DesignConditionEntry> entries)
    {
        var sheet = workbook.Worksheets.Add("设计条件汇总");
        string[] headers = ["层级", "项目", "提出专业", "接收专业", "条件名称", "记录数", "图纸数量", "附件数"];
        WriteHeader(sheet, headers);
        var row = 2;
        foreach (var projectGroup in entries.GroupBy(item => item.ProjectNumber).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            WriteSummaryRow(sheet, row++, "项目", projectGroup.Key, "", "", "", projectGroup.Count(), projectGroup.Sum(item => item.DrawingCount), projectGroup.Sum(item => item.AttachmentCount), 0);
            foreach (var issuingGroup in projectGroup.GroupBy(item => item.IssuingDiscipline).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                WriteSummaryRow(sheet, row++, "提出专业", projectGroup.Key, issuingGroup.Key, "", "", issuingGroup.Count(), issuingGroup.Sum(item => item.DrawingCount), issuingGroup.Sum(item => item.AttachmentCount), 1);
                foreach (var receivingGroup in issuingGroup.GroupBy(item => item.ReceivingDiscipline).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
                {
                    WriteSummaryRow(sheet, row++, "接收专业", projectGroup.Key, issuingGroup.Key, receivingGroup.Key, "", receivingGroup.Count(), receivingGroup.Sum(item => item.DrawingCount), receivingGroup.Sum(item => item.AttachmentCount), 2);
                    foreach (var conditionGroup in receivingGroup.GroupBy(item => item.ConditionName).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        WriteSummaryRow(sheet, row++, "条件明细", projectGroup.Key, issuingGroup.Key, receivingGroup.Key, conditionGroup.Key, conditionGroup.Count(), conditionGroup.Sum(item => item.DrawingCount), conditionGroup.Sum(item => item.AttachmentCount), 3);
                    }
                }
            }
        }
        WriteSummaryRow(sheet, row, "总计", "", "", "", "", entries.Count, entries.Sum(item => item.DrawingCount), entries.Sum(item => item.AttachmentCount), 0);
        sheet.Range(1, 1, row, headers.Length).SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        sheet.Column(5).Width = Math.Clamp(sheet.Column(5).Width, 16, 40);
    }

    private static void AddDetails(XLWorkbook workbook, IReadOnlyList<DesignConditionEntry> entries)
    {
        var sheet = workbook.Worksheets.Add("设计条件明细");
        string[] headers = ["提出日期", "项目", "提出专业", "接收专业", "接收人", "条件名称", "版次", "图幅", "图纸数量", "附件数", "附件文件名", "创建时间", "更新时间"];
        WriteHeader(sheet, headers);
        var ordered = entries.OrderBy(item => item.IssuedDate).ThenBy(item => item.ProjectNumber).ThenBy(item => item.ConditionName).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            var item = ordered[index];
            var row = index + 2;
            sheet.Cell(row, 1).Value = item.IssuedDate;
            sheet.Cell(row, 2).Value = item.ProjectNumber;
            sheet.Cell(row, 3).Value = item.IssuingDiscipline;
            sheet.Cell(row, 4).Value = item.ReceivingDiscipline;
            sheet.Cell(row, 5).Value = item.Receiver;
            sheet.Cell(row, 6).Value = item.ConditionName;
            sheet.Cell(row, 7).Value = item.Revision;
            sheet.Cell(row, 8).Value = item.DrawingSize;
            sheet.Cell(row, 9).Value = item.DrawingCount;
            sheet.Cell(row, 10).Value = item.AttachmentCount;
            sheet.Cell(row, 11).Value = string.Join(Environment.NewLine, item.Attachments.Select(file => file.OriginalFileName));
            sheet.Cell(row, 12).Value = item.CreatedAt;
            sheet.Cell(row, 13).Value = item.UpdatedAt;
        }
        sheet.Column(1).Style.DateFormat.Format = "yyyy.MM.dd";
        sheet.Columns(12, 13).Style.DateFormat.Format = "yyyy.MM.dd HH:mm";
        sheet.Column(11).Style.Alignment.WrapText = true;
        sheet.Range(1, 1, Math.Max(ordered.Length + 1, 1), headers.Length).SetAutoFilter();
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        sheet.Column(6).Width = Math.Clamp(sheet.Column(6).Width, 18, 42);
        sheet.Column(11).Width = Math.Clamp(sheet.Column(11).Width, 18, 48);
    }

    private static void WriteHeader(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }
        var header = sheet.Range(1, 1, 1, headers.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E7ECE9");
        header.Style.Font.FontColor = XLColor.FromHtml("#2F3A36");
        sheet.Row(1).Height = 24;
    }

    private static void WriteSummaryRow(IXLWorksheet sheet, int row, string level, string project, string issuing, string receiving, string condition, int records, int drawings, int attachments, int indent)
    {
        sheet.Cell(row, 1).Value = level;
        sheet.Cell(row, 2).Value = project;
        sheet.Cell(row, 3).Value = issuing;
        sheet.Cell(row, 4).Value = receiving;
        sheet.Cell(row, 5).Value = condition;
        sheet.Cell(row, 5).Style.Alignment.Indent = indent;
        sheet.Cell(row, 6).Value = records;
        sheet.Cell(row, 7).Value = drawings;
        sheet.Cell(row, 8).Value = attachments;
        if (level is not "条件明细")
        {
            sheet.Range(row, 1, row, 8).Style.Font.Bold = true;
        }
    }
}
