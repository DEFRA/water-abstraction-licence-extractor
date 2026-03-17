using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Configuration;

public class LookupConfiguration(
    List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
    Dictionary<string, DmsFileData> licenceNumberMapping,
    HashSet<string> validLowercaseFirstNames,
    IFileService fileService,
    int regionCode,
    int maxPagesToProcessWhenOcrNeeded = 20,
    object? naldLinkedLicenceHelper = null)
{
    public Dictionary<string, DmsFileData> LicenceNumberMapping { get; set; } = licenceNumberMapping;

    public List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels { get; } = labels;

    public IFileService FileService { get; set; } = fileService;

    public int RegionCode { get; set; } = regionCode;

    public readonly HashSet<string> ValidLowercaseFirstNames = validLowercaseFirstNames;

    public readonly int MaxPagesToProcessWhenOcrNeeded = maxPagesToProcessWhenOcrNeeded;

    public object? NaldLinkedLicenceHelper { get; set; } = naldLinkedLicenceHelper;
    
    public LookupConfiguration Clone()
    {
        return new LookupConfiguration(
            Labels,
            LicenceNumberMapping,
            ValidLowercaseFirstNames,
            FileService,
            RegionCode,
            MaxPagesToProcessWhenOcrNeeded,
            NaldLinkedLicenceHelper);
    }
}