namespace WALE.ProcessFile.Services.Models.OutputSchema.Wr51;

public class Wr51FormMeasurementDetails
{
    public string? MeterMake { get; set; }
    
    public string? SerialNumber { get; set; }
    
    public string? Reading { get; set; }
    
    public string? Units { get; set; }
    
    public string? Other { get; set; }
    
    public string? CertificatesOrRecordsAvailableFor { get; set; }

    public Wr51FormInspectionDate DateOfCertificateOrRecord { get; set; } = new();
    
    public string? Calibration { get; set; }
    
    public string? Conformance { get; set; }
    
    public string? FlowVerification { get; set; }
    
    public string? MeterVerification { get; set; }

    public Wr51FormMaintenance Maintenance { get; set; } = new();
    
    public Wr51FormReadingsTaken ReadingsTaken { get; set; } = new();
    
    public string? WhereKept { get; set; }
}