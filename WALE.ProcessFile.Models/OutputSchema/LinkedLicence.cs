namespace WALE.ProcessFile.Models.OutputSchema;

public class LinkedLicence
{
    public string? LicenceNumber { get; init; }
    
    public string? Filename { get; set; }
    
    public Condition? Condition { get; set; }
    
    public string[]? FromSection { get; set; }
}