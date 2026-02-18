using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Configuration;

public class LookupConfiguration(
    List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
    Dictionary<string, DmsFileData> licenceNumberMapping,
    HashSet<string> validLowercaseFirstNames,
    int regionCode)
{
    public Dictionary<string, DmsFileData> LicenceNumberMapping { get; } = licenceNumberMapping;

    public List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels { get; } = labels;

    public readonly int RegionCode = regionCode;

    public HashSet<string> ValidLowercaseFirstNames = validLowercaseFirstNames;
}