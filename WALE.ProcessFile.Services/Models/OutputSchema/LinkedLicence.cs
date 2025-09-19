namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class LinkedLicence
{
    public string? LicenceNumber { get; init; }
    
    public string? Filename { get; set; }
    
    public Condition? Condition { get; set; }
    
    public string[]? FromSection { get; set; }
}