namespace WALE.ProcessFile.Core.Models;

public class SingleFileProcessRequest
{
    public string? FilePath { get; set; }

    public int? ProcessRunId { get; set; }
}