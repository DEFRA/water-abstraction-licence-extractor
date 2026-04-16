using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveMatchResultRequest
{
    public Guid fileId { get; set; }
    
    public MatchesResult? matches { get; set; }
        
    public int processRunId { get; set; }
}