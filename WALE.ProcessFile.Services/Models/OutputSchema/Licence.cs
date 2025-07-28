namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class Licence
{
    public string? LicenceNumber { get; set; }
    
    public string? Filename { get; set; }
    
    public LicenceVersion? LicenceVersion { get; set; }
    
    public AbstractionLimits? AbstractionLimits { get; set; }    
}