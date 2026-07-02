namespace WALE.ProcessFile.Core.Models;

public class ProcessRunFile
{
    public int ProcessRunId { get; set; }
    
    public int ProcessRunFileId { get; set; }
    
    public string? FileName { get; set; }
    
    public DateTime? EndDateTimeUtc { get; set; }

    public string? ErrorMessage { get; set; }
}