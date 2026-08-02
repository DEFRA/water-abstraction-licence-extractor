using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class LicenceNumber
{
    public const string Constant = "LicenceNumber";
    public static ILicenceNumberService? LicenceNumberService;

    public static (bool Success, List<DocumentLine> MatchedLines) AnyIsLicenceNumber(
        IEnumerable<DocumentLine?> lines,
        LabelToMatch label,
        bool isOcr,
        Dictionary<string, object?> additionalInformationStore)
    {
        if (LicenceNumberService == null)
        {
            throw new ArgumentNullException(nameof(LicenceNumberService));
        }

        return LicenceNumberService.AnyIsLicenceNumber(
            lines,
            label,
            isOcr,
            additionalInformationStore);
    }
}