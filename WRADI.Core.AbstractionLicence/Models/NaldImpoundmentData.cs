namespace WRADI.Core.AbstractionLicence.Models;

public class NaldImpoundmentData
{
    public int Id { get; set; }
    
    public string? LicenceNumber { get; set; }
    
    public short FgacRegionCode { get; set; }
    
    public DateTime? RevocationDate { get; init; }
}