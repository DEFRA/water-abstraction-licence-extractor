using WALE.ProcessFile.Core.Helpers;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

public static class LicenceNumberHelper
{
    public static bool ContainsOther(
        string? licenceNumber1,
        string? licenceNumber2,
        int regionId)
    {
        var licenceNumberStripped1 = FormattingHelper.StripForComparison(licenceNumber1, regionId);

        if (string.IsNullOrWhiteSpace(licenceNumberStripped1))
        {
            return false;
        }

        var licenceNumberStripped2 = FormattingHelper.StripForComparison(licenceNumber2, regionId);

        if (string.IsNullOrWhiteSpace(licenceNumberStripped2))
        {
            return false;
        }

        // A licence must not absorb its own revision, so containment is suppressed when exactly
        // one side carries a revision marker.
        const string r1 = "R1";
        const string r01 = "R01";

        var firstContainsR1 = licenceNumberStripped1.Contains(r1) || licenceNumberStripped1.Contains(r01);
        var secondContainsR1 = licenceNumberStripped2.Contains(r1) || licenceNumberStripped2.Contains(r01);

        var only1HasR1 = (firstContainsR1 && !secondContainsR1)
                         || (!firstContainsR1 && secondContainsR1);

        return !only1HasR1
               && (licenceNumberStripped1.Contains(licenceNumberStripped2)
                   || licenceNumberStripped2.Contains(licenceNumberStripped1));
    }
}
