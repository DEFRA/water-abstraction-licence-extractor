namespace WRADI.Core.AbstractionLicence.Constants;

public static class LicenceNumberFlagReasons
{
    // The licence number has never appeared in any previous process run
    public const string NewLicence = "New Licence";

    // The licence number appeared in the most recent previous process run, but with a different file ID
    public const string NewLicenceDocument = "New Licence Document";
}
