using System.Globalization;
using System.Reflection;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;

namespace WRADI.DocumentType.WrInspectionReport.Csv;

public static class WrInspectionReportReportBuilder
{
    private static readonly HashSet<string> InternalColumnNames = new(StringComparer.Ordinal)
    {
        nameof(WrInspectionReportCsvLine.Images),
        nameof(WrInspectionReportCsvLine.Metadata__Date__RawDate),
        nameof(WrInspectionReportCsvLine.InspectionDate__RawDate),
        nameof(WrInspectionReportCsvLine.InspectionDate__RawTime),
        nameof(WrInspectionReportCsvLine.MeasurementDetails__DateOfCertificateOrRecord__RawDate),
        nameof(WrInspectionReportCsvLine.InspectionDate__Year),
        nameof(WrInspectionReportCsvLine.Metadata__DocumentTemplateVerison)
    };

    public static byte[] BuildCsv(IEnumerable<WrInspectionReportCsvLine> lines, bool excludeInternalColumns = false)
    {
        var properties = GetColumns(excludeInternalColumns);

        using var memoryStream = new MemoryStream();

        using (var writer = new StreamWriter(memoryStream, Encoding.UTF8, leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CultureInfo("en-GB")))
        {
            foreach (var property in properties)
            {
                csv.WriteField(property.Name);
            }

            csv.NextRecord();

            foreach (var line in lines)
            {
                foreach (var property in properties)
                {
                    csv.WriteField(property.GetValue(line)?.ToString() ?? string.Empty);
                }

                csv.NextRecord();
            }
        }

        return memoryStream.ToArray();
    }

    public static byte[] BuildXlsx(IEnumerable<WrInspectionReportCsvLine> lines, bool excludeInternalColumns = false)
    {
        var properties = GetColumns(excludeInternalColumns);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("WR51");

        for (var columnIndex = 0; columnIndex < properties.Length; columnIndex++)
        {
            worksheet.Cell(1, columnIndex + 1).Value = properties[columnIndex].Name;
        }

        var rowIndex = 2;

        foreach (var line in lines)
        {
            for (var columnIndex = 0; columnIndex < properties.Length; columnIndex++)
            {
                var value = properties[columnIndex].GetValue(line);
                worksheet.Cell(rowIndex, columnIndex + 1).Value = value?.ToString() ?? string.Empty;
            }

            rowIndex++;
        }

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.SheetView.FreezeRows(1);

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static PropertyInfo[] GetColumns(bool excludeInternalColumns)
    {
        var properties = typeof(WrInspectionReportCsvLine).GetProperties();

        return excludeInternalColumns
            ? properties.Where(property => !InternalColumnNames.Contains(property.Name)).ToArray()
            : properties;
    }
}
