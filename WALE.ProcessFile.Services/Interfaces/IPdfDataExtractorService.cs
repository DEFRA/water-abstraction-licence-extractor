using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Interfaces;

public interface IPdfDataExtractorService
{
    public bool InUse { get; set; }
    
    public Task<MatchesResult> GetMatchesAsync(
        string pdfFilePath,
        IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        Dictionary<string, string> licenceMapping,
        List<string> previouslyParsedPaths,
        string outputFolder,
        bool useCache);

    public Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> text,
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        Dictionary<string, string> licenceMapping,
        List<string> previouslyParsedPaths,
        string outputFolder,
        bool useCache);

    public void Dispose();
}