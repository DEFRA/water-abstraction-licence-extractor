using WALE.ProcessFile.Models;
using WALE.ProcessFile.Services.Configuration;

namespace WALE.ProcessFile.Services.Interfaces;

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

    Task<MatchesResult> GetPagesAsync(
        string pdfFilePath,
        LookupConfiguration configuration);
    
    public void Dispose();
}