using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WRADI.DocumentType.WrInspectionReport.Services;

public class NullLicenceNumberService : ILicenceNumberServiceCore
{
    public (bool Success, List<DocumentLine> MatchedLines) AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr,
        Dictionary<string, object?> additionalInformationStore) => (false, []);

    public (bool HasSuccessor, List<NaldLicenceNumberHistory> History) AnyNewerLicenceNumber(
        string? licenceNumber) => (false, []);
}