namespace WALE.ProcessFile.Core.Models;

public class DmsFileData
{
    public string? PermitNumber { get; set; }
    
    public string? NaldLicenceRef { get; set; }
    
    public string? DmsPath { get; set; }
    
    public string? DestinationFileName { get; set; }
    
    public string? StrippedLicenceNumber { get; set; }
    
    public Guid FileId { get; set; }
}