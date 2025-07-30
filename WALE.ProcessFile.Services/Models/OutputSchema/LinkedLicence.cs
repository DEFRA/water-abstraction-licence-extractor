namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class LinkedLicence
{
    public string? LicenceNumber { get; init; }
    
    public Condition? Condition { get; set; }
}