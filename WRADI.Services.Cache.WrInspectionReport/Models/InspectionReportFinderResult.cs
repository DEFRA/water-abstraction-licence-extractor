namespace WRADI.Services.Cache.WrInspectionReport.Models;

// The inspection-report (WR51) equivalent of LicenceFinderResult. Lives in its own
// document-type-specific project rather than WALE.ProcessFile.Core.Models, mirroring
// LicenceFinderResult's home in WRADI.Core.AbstractionLicence.Models.
public class InspectionReportFinderResult
{
    public string? PermitNumber { get; set; }

    public string? FileUrl { get; set; }

    public string? FileName { get; set; }

    public string? LibraryName { get; set; }

    public string? Regime { get; set; }

    public string? FileSize { get; set; }

    public string? FileId { get; set; }

    public string? DocumentDate { get; set; }

    public string? OtherReference { get; set; }

    public string? DisclosureStatus { get; set; }

    public int ProcessRunId { get; set; }
}
