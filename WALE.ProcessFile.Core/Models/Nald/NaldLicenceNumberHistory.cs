namespace WALE.ProcessFile.Core.Models.Nald;

public class NaldLicenceNumberHistory
{
    public string? LicenceNumber { get; set; }

    public List<string> FollowOnLicenceNumbers { get; set; } = [];
    
    public string? Source { get; set; }
}