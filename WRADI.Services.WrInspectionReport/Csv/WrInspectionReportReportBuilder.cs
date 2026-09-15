using System.Globalization;
using System.Reflection;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;

namespace WRADI.DocumentType.WrInspectionReport.Csv;

// Pure byte-builders for the WR51 process-run export - no DB/IO dependency, callers fetch the
// already-saved MatchesResult rows for a run, convert each to a WrInspectionReportCsvLine (see
// WrInspectionReportSchemaConverter.ToForm + WrInspectionReportCsvLine.FromForm, exactly as
// FileDataController.WrInspectionReportStringAsync already does per-file), and hand the list here.
public static class WrInspectionReportReportBuilder
{
    // Internal/derived-only columns - useful for debugging extraction, not for a business report.
    // Raw* columns duplicate an already-parsed column in unprocessed form; Year is a pure
    // derivative of InspectionDate__DateTime; DocumentTemplateVerison is template-versioning
    // metadata; Images is a list of internal artifact filenames. Flagged out via
    // excludeInternalColumns rather than removed outright - still useful when debugging a
    // specific extraction. Chosen 2026-09-15 while trimming a ~17,600-row export.
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

        // UTF-8 (with BOM, so Excel on Windows auto-detects it rather than mis-reading as
        // Windows-1252) - roughly halves the file versus the old UTF-16 convention, still opens
        // correctly anywhere UTF-16 did.
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
