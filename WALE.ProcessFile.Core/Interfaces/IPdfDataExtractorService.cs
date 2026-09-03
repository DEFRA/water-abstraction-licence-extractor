using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IPdfDataExtractorService
{
    public int Id { get; set; }
    public bool InUse { get; set; }
    
    public Task<(bool StopExecution, bool? AlreadySaved, MatchesResult? Item)> GetMatchesAsync(
        string pdfFileName,
        DmsFileData dmsDataForFile,
        LookupConfiguration configuration,
        List<string> previouslyParsedFiles,
        int processRunId);

    public Task SaveMatchResultAsync(MatchesResult matchesResult, Guid fileId, int processRunId, bool isUpdate);

    public Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> text,
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        List<string> previouslyParsedPaths,
        int regionCode,
        int processRunId,
        LookupConfiguration configuration,
        IDocumentLineService documentLineService,
        Dictionary<string, object?> additionalInformationStore);

    public void Dispose();
}