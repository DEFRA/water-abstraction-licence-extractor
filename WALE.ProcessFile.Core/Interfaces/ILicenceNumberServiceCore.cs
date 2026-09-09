using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Nald;

namespace WALE.ProcessFile.Core.Interfaces;

public interface ILicenceNumberServiceCore
{
    (bool Success, List<DocumentLine> MatchedLines) AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr,
        Dictionary<string, object?> additionalInformationStore);

    (bool HasSuccessor, List<NaldLicenceNumberHistory> History) AnyNewerLicenceNumber(
        string? licenceNumber);
}