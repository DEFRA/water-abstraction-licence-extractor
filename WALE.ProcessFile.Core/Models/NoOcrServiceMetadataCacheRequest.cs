namespace WALE.ProcessFile.Core.Models;

public class NoOcrServiceMetadataCacheRequest
{
    public Guid FileId { get; set; }
    public string? NoOcrServiceName  { get; set; }
    public int ProcessRunId { get; set; }
}