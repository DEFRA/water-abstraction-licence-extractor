namespace WRADI.Core.AbstractionLicence.Models;

public class LicenceFileMapEntry
{
    public string? LicenceNumber { get; set; }
        
    public int LicenceId { get; set; }
        
    public int MatchesResultId { get; set; }
        
    public Guid FileId { get; set; }
}