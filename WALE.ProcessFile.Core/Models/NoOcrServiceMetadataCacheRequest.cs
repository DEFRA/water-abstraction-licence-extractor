namespace WALE.ProcessFile.Models;

public class NoOcrServiceMetadataCacheRequest
{
    public string? Filepath { get; set; }
    public string? NoOcrServiceName  { get; set; }
    public int ProcessRunId { get; set; }
}