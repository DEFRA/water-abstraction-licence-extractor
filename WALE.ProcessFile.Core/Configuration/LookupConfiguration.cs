using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Configuration;

public class LookupConfiguration(
    List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
    Dictionary<string, DmsFileData> licenceNumberMapping,
    HashSet<string> validLowercaseFirstNames,
    string pdfFolder,
    int regionCode,
    int maxPagesToProcessWhenOcrNeeded = 20)
{
    public Dictionary<string, DmsFileData> LicenceNumberMapping { get; set; } = licenceNumberMapping;

    public List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels { get; } = labels;

    public string PdfFolder { get; set; } = pdfFolder;

    public int RegionCode { get; set; } = regionCode;

    public readonly HashSet<string> ValidLowercaseFirstNames = validLowercaseFirstNames;

    public readonly int MaxPagesToProcessWhenOcrNeeded = maxPagesToProcessWhenOcrNeeded;

    public LookupConfiguration Clone()
    {
        return new LookupConfiguration(
            Labels,
            LicenceNumberMapping,
            ValidLowercaseFirstNames,
            PdfFolder,
            RegionCode,
            MaxPagesToProcessWhenOcrNeeded);
    }
}