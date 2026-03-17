namespace WALE.Tools.Models;

public class UnknownSectionLinkedLicencesCsvLine
{
    public string? Filename { get; set; }
    public string? LicenceNumber { get; set; }
    public string? ScrapedLicenceNumber { get; set; }
    public bool? LicenceIsLive { get; set; }
    
    public bool? LicenceIsDead { get; set; }
    
    public bool? LicenceIsImpoundment { get; set; }
    
    public bool LicenceFoundInList { get; set; }
    public string? LinkedLicenceNumber { get; set; }
    public int PageNumber { get; set; }
}