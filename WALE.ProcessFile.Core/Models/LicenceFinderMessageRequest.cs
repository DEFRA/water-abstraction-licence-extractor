namespace WALE.ProcessFile.Core.Models;

public class LicenceFinderMessageRequest
{
    public string FlowToRun { get; set; } = "AbstractionLicence";

    public string RegionName { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string RestrictToRegionName { get; set; } = string.Empty;
}
