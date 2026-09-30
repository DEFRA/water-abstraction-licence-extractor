namespace WRADI.DocumentType.WrInspectionReport.Models;

public class WrInspectionReport
{
    public WrInspectionReportMetadata Metadata { get; set; } = new();
    
    public string? LicenceNumber { get; set; }

    // Canonical form for cross-system matching (e.g. against NALD/DMS licence records) -
    // LicenceNumber itself stays a verbatim transcription of the printed cell (deliberately,
    // see the golden set's own labelling notes), which can read "28/39/23/0090 and
    // 28/39/23/0143" for a multi-licence inspection. This splits that apart and normalises
    // each licence number to alphanumeric-only, uppercase - the same canonical form the
    // DEFRA water-abstraction-licence-finder tool's own permit-number matching
    // (RuleHelpers.ContainsPermitNumberPattern) already strips filenames down to for
    // comparison, and the same shape the DMS filenames themselves are built from (e.g.
    // "7/34/06/*G/0027" -> "73406G0027", matching the real filename wr51__73406g0027__...).
    public List<string> LicenceNumberCleaned { get; set; } = [];

    public string? InspectionClass { get; set; }
    
    public WrInspectionReportAddress Address { get; set; } = new();
    
    public WrInspectionReportMetWith MetWith { get; set; } = new();
    
    public string? InspectingOfficer { get; set; }
    
    public WrInspectionReportInspectionDateTime InspectionDate { get; set; } = new();
    
    public WrInspectionReportLicenceProvisions LicenceProvisions { get; set; } = new();
    
    public WrInspectionReportMeasurementDetails MeasurementDetails { get; set; } = new();
    
    public List<string> Images { get; set; } = [];
    
    public string? GeneralComments { get; set; }
}