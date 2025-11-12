namespace WALE.Tools.Models;

public class LinkedLicencesCsvLine
{
    public string? Filename { get; set; }
    public string? LicenceNumber { get; set; }
    public string? ScrapedLicenceNumber { get; set; }
    public string? NaldLicenceNumber { get; set; }
    public bool? LicenceIsLive { get; set; }
    
    public bool? LicenceIsDead { get; set; }
    
    public bool? LicenceIsImpoundment { get; set; }
    
    public bool LicenceFoundInList { get; set; }
    public string? LinkedLicenceNumber { get; set; }
    public string? NaldLinkedLicenceNumber { get; set; }
    public string? LinkedLicenceFromSection { get; set; }
    public bool? LinkedLicenceIsLive { get; set; }
    
    public bool? LinkedLicenceIsDead { get; set; }
    
    public bool? LinkedLicenceIsImpoundment { get; set; }
    
    public bool LinkedLicenceFoundInList { get; set; }
}