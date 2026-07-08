namespace WALE.Api.Areas.BFF.Models;

public class SendFileProcessSingleMessageRequest
{
    public string? FilePath { get; set; }
    
    public string? PermitNumber { get; set; }
    
    public string? NaldLicenceRef { get; set; }
    
    public string? DmsPath { get; set; }
    
    public string? DestinationFileName { get; set; }
    
    public string? StrippedLicenceNumber { get; set; }
    
    public Guid FileId { get; set; }
    
    public int RegionId { get; set; }
    
    public int? ProcessRunId { get; set; }
}