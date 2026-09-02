namespace WRADI.DocumentType.WrInspectionReport.Models;

public class WrInspectionReport
{
    public WrInspectionReportMetadata Metadata { get; set; } = new();
    
    public string? LicenceNumber { get; set; }
    
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