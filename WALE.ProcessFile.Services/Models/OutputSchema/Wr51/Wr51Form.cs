namespace WALE.ProcessFile.Services.Models.OutputSchema.Wr51;

public class Wr51Form
{
    public Wr51FormMetadata Metadata { get; set; } = new();
    
    public string? LicenceNumber { get; set; }
    
    public string? InspectionClass { get; set; }
    
    public Wr51FormAddress Address { get; set; } = new();
    
    public Wr51FormMetWith MetWith { get; set; } = new();
    
    public string? InspectingOfficer { get; set; }
    
    public Wr51FormInspectionDateTime InspectionDate { get; set; } = new();
    
    public Wr51FormLicenceProvisions LicenceProvisions { get; set; } = new();
    
    public Wr51FormMeasurementDetails MeasurementDetails { get; set; } = new();
    
    public string? GeneralComments { get; set; }
}