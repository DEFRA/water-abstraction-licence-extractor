namespace WALE.ProcessFile.Core.Models;

public class FileProcessSingleRequest
{
    public string? FilePath { get; set; }

    public string? PermitNumber { get; set; }
    
    public string? DmsPath { get; set; }
    
    public string? DestinationFileName { get; set; }
    
    public Guid FileId { get; set; }
    
    public int RegionId { get; set; }
    
    public int? ProcessRunId { get; set; }
    
    public int? DelayInSeconds { get; set; }
    
    public DateTime RequestedAt { get; set; }
    
    public int LockRetryCount { get; set; }

    // Which document type's services should process this file - the file-process queue is now
    // shared across all document types, so this is what the consumer dispatches on. Defaults to
    // AbstractionLicence for backward compatibility with anything already in flight/unaware of
    // this field.
    public string DocumentType { get; set; } = "AbstractionLicence";
}