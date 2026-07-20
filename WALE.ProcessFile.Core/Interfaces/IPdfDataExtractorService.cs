using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IPdfDataExtractorService
{
    public int Id { get; set; }
    public bool InUse { get; set; }
    
    public Task<(bool AlreadySaved, MatchesResult Item)> GetMatchesAsync(
        string pdfFileName,
        DmsFileData dmsDataForFile,
        LookupConfiguration configuration,
        List<string> previouslyParsedFiles,
        int processRunId);

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
        Dictionary<string, object?> additionalInformationStore);

    public void Dispose();
}