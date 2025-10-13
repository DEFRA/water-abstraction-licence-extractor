using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Database.Interfaces;

public interface IDatabaseReadService
{
    public List<ProcessRun> GetProcessRuns();
    
    Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request);
}