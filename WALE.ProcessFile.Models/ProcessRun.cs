namespace WALE.ProcessFile.Models;

public class ProcessRun
{
    public string? Description { get; set; }
    public DateTime? StartDateTimeUtc { get; set; }
    public DateTime? EndDateTimeUtc { get; set; }
    public int NumberOfFiles { get; set; }
}