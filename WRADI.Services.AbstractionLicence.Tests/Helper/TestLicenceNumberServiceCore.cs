using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Nald;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public class TestLicenceNumberServiceCore : ILicenceNumberServiceCore
{
    public (bool Success, List<DocumentLine> MatchedLines) AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr,
        Dictionary<string, object?> additionalInformationStore)
    {
        throw new NotImplementedException();
    }

    public (bool HasSuccessor, List<NaldLicenceNumberHistory> History) AnyNewerLicenceNumber(
        string? licenceNumber)
    {
        return (false, []);
    }
}