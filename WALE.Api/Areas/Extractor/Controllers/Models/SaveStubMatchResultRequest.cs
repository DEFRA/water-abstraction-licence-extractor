namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveStubMatchResultRequest
{
    public string? filename { get; set; }
    
    public Guid fileId { get; set; }
    
    public int processRunId { get; set; }

    /// <summary>
    /// Reclaim an existing row for this file/run (e.g. one left as TransientError) instead of inserting another.
    /// Optional so older extractors that don't send it keep inserting as before.
    /// </summary>
    public bool isUpdate { get; set; }
}