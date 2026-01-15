using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface ILicenceNumberService
{
    Task InitializeAsync();
    List<string> FindLicenceNumbers(string? text);
    bool AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr,
        out List<DocumentLine> matchedLines);
}
