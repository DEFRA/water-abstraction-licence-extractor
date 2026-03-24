using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface ILicenceNumberService
{
    (bool Success, List<DocumentLine> MatchedLines) AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr,
        Dictionary<string, object?> additionalInformationStore);

    List<NaldLicence> GetNaldLicences(string licenceNumber, short regionCode);
    
    List<NaldLicence> ExtractNaldLicences(string? sourceText);
}