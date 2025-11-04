using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Services.Configuration;

public class LookupConfiguration(
    List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
    Dictionary<string, string> licenceMapping)
{
    public Dictionary<string, string> LicenceMapping { get; } = licenceMapping;

    public List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels { get; } = labels;
}