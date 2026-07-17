using System.IO;
using ClosedXML.Excel;
using KanbanForOne.Models;

namespace KanbanForOne.Services;

public sealed class WorkHourExportService
{
    public Task ExportAsync(
        string filePath,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<WorkHourSummaryItem> summaries,
        IReadOnlyList<WorkHourEntry> details)
    {
        return Task.Run(() => Export(filePath, startDate.Date, endDate.Date, summaries, details));
    }

    private static void Export(
        string filePath,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<WorkHourSummaryItem> summaries,
        IReadOnlyList<WorkHourEntry> details)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var workbook = new XLWorkbook();
        AddSummarySheet(workbook, startDate, endDate, summaries);
        AddDetailSheet(workbook, details);
        workbook.SaveAs(filePath);
    }

    private static void AddSummarySheet(
        XLWorkbook workbook,
        DateTime startDate,
        DateTime endDate,
        IReadOnlyList<WorkHourSummaryItem> summaries)
    {
        var worksheet = workbook.Worksheets.Add("人工时汇总");
        string[] headers = ["日期", "项目号", "专业", "工作内容", "汇总工时"];
        WriteHeader(worksheet, headers);
        var dateRange = $"{startDate:yyyy.MM.dd}~{endDate:yyyy.MM.dd}";

        var ordered = summaries
            .OrderBy(item => item.ProjectNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Discipline, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.WorkActivity, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            var row = index + 2;
            var item = ordered[index];
            worksheet.Cell(row, 1).Value = dateRange;
            worksheet.Cell(row, 2).Value = item.ProjectNumber;
            worksheet.Cell(row, 3).Value = item.Discipline;
            worksheet.Cell(row, 4).Value = item.WorkActivity;
            worksheet.Cell(row, 5).Value = item.TotalHours;
        }

        FormatSheet(worksheet, headers.Length, ordered.Length + 1);
        worksheet.Column(1).Width = 25;
        worksheet.Column(2).Width = Math.Max(14, worksheet.Column(2).Width);
        worksheet.Column(3).Width = Math.Max(12, worksheet.Column(3).Width);
        worksheet.Column(4).Width = Math.Max(16, worksheet.Column(4).Width);
        worksheet.Column(5).Width = 13;
        worksheet.Column(5).Style.NumberFormat.Format = "0.##";
    }

    private static void AddDetailSheet(XLWorkbook workbook, IReadOnlyList<WorkHourEntry> details)
    {
        var worksheet = workbook.Worksheets.Add("人工时明细");
        string[] headers = ["日期", "项目号", "专业", "工作内容", "工时", "备注"];
        WriteHeader(worksheet, headers);

        var ordered = details
            .OrderBy(item => item.WorkDate)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            var row = index + 2;
            var item = ordered[index];
            worksheet.Cell(row, 1).Value = item.WorkDate;
            worksheet.Cell(row, 2).Value = item.ProjectNumber;
            worksheet.Cell(row, 3).Value = item.Discipline;
            worksheet.Cell(row, 4).Value = item.WorkActivity;
            worksheet.Cell(row, 5).Value = item.Hours;
            worksheet.Cell(row, 6).Value = item.Remark;
        }

        FormatSheet(worksheet, headers.Length, ordered.Length + 1);
        worksheet.Column(1).Width = 13;
        worksheet.Column(1).Style.DateFormat.Format = "yyyy.MM.dd";
        worksheet.Column(2).Width = Math.Max(14, worksheet.Column(2).Width);
        worksheet.Column(3).Width = Math.Max(12, worksheet.Column(3).Width);
        worksheet.Column(4).Width = Math.Max(16, worksheet.Column(4).Width);
        worksheet.Column(5).Width = 11;
        worksheet.Column(5).Style.NumberFormat.Format = "0.##";
        worksheet.Column(6).Width = Math.Clamp(worksheet.Column(6).Width, 18, 48);
        worksheet.Column(6).Style.Alignment.WrapText = true;
    }

    private static void WriteHeader(IXLWorksheet worksheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            worksheet.Cell(1, index + 1).Value = headers[index];
        }

        var header = worksheet.Range(1, 1, 1, headers.Count);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.FromHtml("#2F3A36");
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E7ECE9");
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(1).Height = 24;
    }

    private static void FormatSheet(IXLWorksheet worksheet, int columnCount, int lastRow)
    {
        worksheet.SheetView.FreezeRows(1);
        worksheet.Range(1, 1, Math.Max(lastRow, 1), columnCount).SetAutoFilter();
        worksheet.Columns(1, columnCount).AdjustToContents();
        worksheet.Range(1, 1, Math.Max(lastRow, 1), columnCount).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
        worksheet.Range(1, 1, Math.Max(lastRow, 1), columnCount).Style.Border.BottomBorderColor = XLColor.FromHtml("#D5DBD8");
        worksheet.Range(2, 1, Math.Max(lastRow, 2), columnCount).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }
}
