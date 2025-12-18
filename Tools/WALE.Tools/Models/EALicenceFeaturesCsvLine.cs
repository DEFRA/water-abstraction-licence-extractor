namespace WALE.Tools.Models;

public class EALicenceFeaturesCsvLine
{
    public string? Filename { get; set; }
    public string? LicenceNumber { get; set; }
    public bool HasPointTable { get; set; }
    public bool HasMultipleSchedules { get; set; }    
}