namespace WRADI.DocumentType.WrInspectionReport.Models;

public class WrInspectionReportMeasurementDetails
{
    public string? MeterMake { get; set; }

    public string? SerialNumber { get; set; }

    public string? MeterAssetNumber { get; set; }

    public string? Reading { get; set; }

    public string? FlowRate { get; set; }

    public string? Verification { get; set; }

    public string? SpotCheckResult { get; set; }

    public string? Units { get; set; }
    
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
}