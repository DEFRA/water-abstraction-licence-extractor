namespace WALE.ProcessFile.Core.Models;

public class LicenceFinderMessageRequest
{
    public string FlowToRun { get; init; } = "FindLicenceFilesAsync";

    public string RegionName { get; init; } = string.Empty;
    
    public string RestrictToRegionName { get; init; } = string.Empty;
}
