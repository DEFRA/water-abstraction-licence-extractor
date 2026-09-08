namespace WRADI.DocumentType.WrInspectionReport.Models;

public class WrInspectionReportMeasurementDetails
{
    public string? Verification { get; set; }

    public string? SpotCheckResult { get; set; }

    public string? Other { get; set; }
    
    public string? CertificatesOrRecordsAvailableFor { get; set; }

    public WrInspectionReportInspectionDate DateOfCertificateOrRecord { get; set; } = new();
    
    public string? Calibration { get; set; }
    
    public string? Conformance { get; set; }
    
    public string? FlowVerification { get; set; }
    
    public string? MeterVerification { get; set; }

    public WrInspectionReportMaintenance Maintenance { get; set; } = new();
    
    public WrInspectionReportReadingsTaken ReadingsTaken { get; set; } = new();

    public string? WhereKept { get; set; }

    // Always populated with at least one entry - single-meter documents get a one-item list.
    // Detecting and populating a genuine second+ meter doesn't exist yet; today this always has
    // exactly one entry, built from the same extraction every document already went through.
    public List<WrInspectionReportMeter>? Meters { get; set; }
}