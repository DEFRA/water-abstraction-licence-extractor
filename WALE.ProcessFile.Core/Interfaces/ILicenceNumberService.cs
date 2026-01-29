using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface ILicenceNumberService
{
    Task<(bool Success, List<DocumentLine> MatchedLines)> AnyIsLicenceNumberAsync(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr);
    Task<List<NaldLicence>> GetNaldLicencesAsync(string licenceNumber, string regionCode);
    Task<List<NaldLicence>> ExtractNaldLicencesAsync(string? sourceText);
}
