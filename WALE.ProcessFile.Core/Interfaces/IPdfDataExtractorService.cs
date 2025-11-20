using WALE.ProcessFile.Models.Configuration;

namespace WALE.ProcessFile.Models.Interfaces;

public interface IPdfDataExtractorService
{
    public bool InUse { get; set; }
    
    public Task<MatchesResult> GetMatchesAsync(
        string pdfFilePath,
        LookupConfiguration configuration,
        List<string> previouslyParsedPaths,
        int processRunId);

    public Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> text,
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        Dictionary<string, string> licenceMapping,
        List<string> previouslyParsedPaths,
        int processRunId);

    public void Dispose();
}