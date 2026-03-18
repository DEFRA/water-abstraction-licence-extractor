using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Configuration;

public class LookupConfiguration(
    List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
    Dictionary<string, DmsFileData> allDmsData,
    Dictionary<Guid, List<DmsFileIdInformation>> dmsFileIds,
    HashSet<string> validLowercaseFirstNames,
    IFileService fileService,
    int regionCode,
    int maxPagesToProcessWhenOcrNeeded = 20,
    object? naldLinkedLicenceHelper = null)
{
    public List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels { get; } = labels;

    public Dictionary<string, DmsFileData> AllDmsData { get; set; } = allDmsData;
    
    public Dictionary<Guid, List<DmsFileIdInformation>> DmsFileIds { get; set; } = dmsFileIds;

    public readonly HashSet<string> ValidLowercaseFirstNames = validLowercaseFirstNames;
    
    public IFileService FileService { get; set; } = fileService;

    public int RegionCode { get; set; } = regionCode;

    public readonly int MaxPagesToProcessWhenOcrNeeded = maxPagesToProcessWhenOcrNeeded;

    public object? NaldLinkedLicenceHelper { get; set; } = naldLinkedLicenceHelper;
    
    public LookupConfiguration Clone()
    {
        return new LookupConfiguration(
            Labels,
            AllDmsData,
            DmsFileIds,
            ValidLowercaseFirstNames,
            FileService,
            RegionCode,
            MaxPagesToProcessWhenOcrNeeded,
            NaldLinkedLicenceHelper);
    }
}