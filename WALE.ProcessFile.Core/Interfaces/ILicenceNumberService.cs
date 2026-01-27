using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface ILicenceNumberService
{
    Task<List<string>> FindLicenceNumbersAsync(string? text);
    Task<(bool Success, List<DocumentLine> MatchedLines)> AnyIsLicenceNumberAsync(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr);
}
