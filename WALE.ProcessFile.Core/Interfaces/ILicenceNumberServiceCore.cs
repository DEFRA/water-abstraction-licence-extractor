using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface ILicenceNumberServiceCore
{
    (bool Success, List<DocumentLine> MatchedLines) AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr,
        Dictionary<string, object?> additionalInformationStore);
}