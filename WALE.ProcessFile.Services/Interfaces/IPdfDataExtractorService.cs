using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Interfaces;

public interface IPdfDataExtractorService
{
    public bool InUse { get; set; }
    
    public Task<IReadOnlyList<LabelGroupResult>> GetMatchesAsync(
        string pdfFilePath,
        IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        Dictionary<string, string> licenceMapping,
        List<string> previouslyParsedPaths,
        string outputFolder,
        bool useCache);
}