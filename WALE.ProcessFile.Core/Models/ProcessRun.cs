namespace WALE.ProcessFile.Core.Models;

public class ProcessRun
{
    public int ProcessRunId { get; set; }
    
    public string? Description { get; set; }
    
    public DateTime? StartDateTimeUtc { get; set; }
    
    public DateTime? EndDateTimeUtc { get; set; }
    
    public int NumberOfFiles { get; set; }
    
    public int SuccessCount { get; set; }

    public string? Status { get; set; }

    public int FilesProcessed { get; set; }

    public int FilesErrored { get; set; }

    public int LicenceCount { get; set; }

    public int LiveLicenceCount { get; set; }
    
    public string DocumentType { get; set; } = "AbstractionLicence";
}