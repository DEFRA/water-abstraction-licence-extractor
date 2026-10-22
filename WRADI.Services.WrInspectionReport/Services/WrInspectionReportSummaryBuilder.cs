using System.Globalization;
using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Models;

namespace WRADI.DocumentType.WrInspectionReport.Services;

/// Projects an extracted report onto the handful of fields a list view needs.
public static class WrInspectionReportSummaryBuilder
{
    /// A matched label that found nothing still serialises as this non-empty string. Counting it
    /// made the provisions section always score, giving every document a floor of 10%. A blank
    /// value does still count: a rule matched and the value was genuinely empty.
    private const string NoFindingEnumValue = "DidntMatch";

    /// Abstraction licences run from the 1960s, so anything earlier is a misread.
    private const int EarliestPlausibleYear = 1960;

    /// The sections completeness is measured over.
    private static readonly string[] SectionNames =
    [
        "licenceNumber", "licenceNumberCleaned", "inspectionClass", "address", "metWith",
        "inspectingOfficer", "inspectionDate", "licenceProvisions", "measurementDetails",
        "generalComments"
    ];

    /// <param name="dmsDates">
    /// The dates DMS holds for the file. These are filing dates, not inspection dates, and stand
    /// in only when the document yielded neither.
    /// </param>
    public static WrInspectionReportSummary Build(
        MatchesResult matchesResult,
        Guid fileId,
        int processRunId,
        string? permitNumber,
        (DateOnly? DocumentDate, DateOnly? UploadDate) dmsDates)
    {
        var form = WrInspectionReportSchemaConverter.ToForm(
            matchesResult, null, GetKnownTemplate(matchesResult));

        // Read back off the serialized JSON rather than the typed model, so this sees exactly what
        // the portal sees - notably Template, which JsonHelper writes as its name rather than the
        // enum's ordinal.
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(form, JsonHelper.GetSerializerOptions()));

        var root = document.RootElement;
        var metadata = root.GetProperty("metadata");

        var inspectionDate = ReadDate(root, "inspectionDate", "dateTime");
        var formDate = ReadDate(metadata, "date", "date");

        var (sortDate, dateSource) = PickSortDate(inspectionDate, formDate, dmsDates);

        return new WrInspectionReportSummary
        {
            ProcessRunId = processRunId,
            FileId = fileId,
            MatchesResultId = matchesResult.MatchesResultId,
            InspectionDate = inspectionDate,
            FormDate = formDate,
            DmsDocumentDate = dmsDates.DocumentDate,
            DmsUploadDate = dmsDates.UploadDate,
            SortDate = sortDate,
            DateSource = dateSource,
            DateWarning = CheckDate(inspectionDate, dmsDates.DocumentDate),
            PermitNumber = permitNumber,
            Template = GetStringOrNull(metadata, "template"),
            Completeness = ComputeCompleteness(root),
            IsScan = metadata.TryGetProperty("isScan", out var isScan)
                     && isScan.ValueKind == JsonValueKind.True
        };
    }

    public static WrTemplateType? GetKnownTemplate(MatchesResult matchesResult)
    {
        if (matchesResult.AdditionalInformation == null
            || !matchesResult.AdditionalInformation.TryGetValue(
                WrInspectionReportSchemaConverter.AdditionalInformationTemplateKey, out var value))
        {
            return null;
        }

        var rawTemplate = value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => null
        };

        return Enum.TryParse<WrTemplateType>(rawTemplate, out var template) ? template : null;
    }

    /// The discovery table holds the DMS date as free text, so anything unparseable is treated as
    /// no date rather than guessed at.
    public static DateOnly? ParseDmsDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? DateOnly.FromDateTime(parsed)
            : null;
    }

    internal static (DateOnly? Date, WrInspectionReportDateSource Source) PickSortDate(
        DateOnly? inspectionDate,
        DateOnly? formDate,
        (DateOnly? DocumentDate, DateOnly? UploadDate) dmsDates)
    {
        if (inspectionDate.HasValue)
        {
            return (inspectionDate, WrInspectionReportDateSource.Extracted);
        }

        if (formDate.HasValue)
        {
            return (formDate, WrInspectionReportDateSource.Metadata);
        }

        if (dmsDates.DocumentDate.HasValue)
        {
            return (dmsDates.DocumentDate, WrInspectionReportDateSource.DmsDocument);
        }

        return dmsDates.UploadDate.HasValue
            ? (dmsDates.UploadDate, WrInspectionReportDateSource.DmsUpload)
            : (null, WrInspectionReportDateSource.None);
    }

    /// Checked against today and against the filing date, because a misread year still presents as
    /// a successfully extracted field.
    internal static WrInspectionReportDateWarning CheckDate(
        DateOnly? inspectionDate,
        DateOnly? dmsDocumentDate)
    {
        if (!inspectionDate.HasValue)
        {
            return WrInspectionReportDateWarning.None;
        }

        if (inspectionDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return WrInspectionReportDateWarning.FutureDate;
        }

        if (inspectionDate.Value.Year < EarliestPlausibleYear)
        {
            return WrInspectionReportDateWarning.BeforeCorpusEra;
        }

        // A day's grace, because DMS records the document a day early over British Summer Time.
        return dmsDocumentDate.HasValue && inspectionDate.Value > dmsDocumentDate.Value.AddDays(1)
            ? WrInspectionReportDateWarning.AfterFiling
            : WrInspectionReportDateWarning.None;
    }

    private static DateOnly? ReadDate(JsonElement parent, string sectionName, string propertyName)
    {
        if (!parent.TryGetProperty(sectionName, out var section))
        {
            return null;
        }

        var value = GetStringOrNull(section, propertyName);

        return !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value.Split('T')[0], out var date)
            ? date
            : null;
    }

    /// A rough proxy: the percentage of the report's top-level sections carrying any content.
    internal static int ComputeCompleteness(JsonElement report)
    {
        var withContent = SectionNames.Count(sectionName =>
            report.TryGetProperty(sectionName, out var section) && HasContent(section));

        return (int)Math.Round(withContent / (double)SectionNames.Length * 100);
    }

    private static bool HasContent(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => HasContent(element.GetString()),
            JsonValueKind.Array => element.EnumerateArray().Any(HasContent),
            JsonValueKind.Object => element.EnumerateObject().Any(property => HasContent(property.Value)),
            _ => true
        };

    private static bool HasContent(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !value.Equals(NoFindingEnumValue, StringComparison.Ordinal);

    private static string? GetStringOrNull(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
