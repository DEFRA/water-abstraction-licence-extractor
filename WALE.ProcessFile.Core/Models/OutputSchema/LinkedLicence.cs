namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class LinkedLicence
{
    public string? LicenceNumber { get; init; }
    
    public string? NaldLicenceNumber { get; init; }
    
    public string? Filename { get; set; }
    
    public Condition? Condition { get; set; }
    
    public string[]? FromSection { get; set; }
    
    public bool? IsLiveLicence { get; set; }
    
    public bool? IsDeadLicence { get; set; }
    
    public bool? IsImpoundmentLicence { get; set; }
    
    public bool LicenceFoundInList { get; set; }
    
    public string? DmsPath { get; set; }
}