namespace WRADI.DocumentType.WrInspectionReport.Models.Csv;

/// <summary>
/// One row per action, with the document's identity repeated on each. Actions are unbounded, like
/// meters, so they cannot live in numbered columns on the per-document line.
/// </summary>
public class WrInspectionReportActionCsvLine
{
    public string? Metadata__Filename { get; set; }
    public string? Metadata__Template { get; set; }
    public string? LicenceNumber { get; set; }
    public string? InspectionDate__DateTime { get; set; }

    public int ActionNumber { get; set; }
    public int ActionCount { get; set; }

    public string? Reference { get; set; }
    public string? Type { get; set; }
    public string? Action { get; set; }
    public string? CompleteBy { get; set; }

    public static IEnumerable<WrInspectionReportActionCsvLine> FromForm(WrInspectionReport form)
    {
        var actions = form.Actions ?? [];

        return actions.Select((action, index) => new WrInspectionReportActionCsvLine
        {
            Metadata__Filename = form.Metadata.Filename,
            Metadata__Template = form.Metadata.Template.ToString(),
            LicenceNumber = form.LicenceNumber,
            InspectionDate__DateTime = form.InspectionDate.DateTime?.ToString("dd/MM/yyyy HH:mm:ss"),
            ActionNumber = index + 1,
            ActionCount = actions.Count,
            Reference = action.Reference,
            Type = action.Type,
            Action = action.Action,
            CompleteBy = action.CompleteBy
        });
    }
}
