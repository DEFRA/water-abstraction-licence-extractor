using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Services.Configuration;

public class LookupConfiguration(
    List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
    Dictionary<string, string> licenceNumberMapping)
{
    public Dictionary<string, string> LicenceNumberMapping { get; } = licenceNumberMapping;

    public List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels { get; } = labels;
}