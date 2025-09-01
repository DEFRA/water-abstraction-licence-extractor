using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Configuration;

public class LookupConfiguration(
    List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
    Dictionary<string, string> licenceMapping,
    string outputFolder,
    string cacheFolder)
{
    public Dictionary<string, string> LicenceMapping { get; } = licenceMapping;

    public List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels { get; } = labels;

    public string OutputFolder { get; } = outputFolder;

    public string CacheFolder { get; set; } = cacheFolder;
}