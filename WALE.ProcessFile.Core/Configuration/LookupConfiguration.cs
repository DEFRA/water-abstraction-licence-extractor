using System.Collections.Concurrent;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Configuration;

public class LookupConfiguration(
    List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
    Dictionary<string, DmsFileData> allDmsData,
    ConcurrentDictionary<Guid, List<DmsFileIdInformation>> dmsFileIds,
    HashSet<string> validLowercaseFirstNames,
    IFileService fileService,
    ICacheService cacheService,
    int regionCode,
    int maxPagesToProcessWhenOcrNeeded = 20,
    object? naldLinkedLicenceHelper = null)
{
    public List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels { get; } = labels;

    public Dictionary<string, DmsFileData> AllDmsData { get; set; } = allDmsData;
    
    public ConcurrentDictionary<Guid, List<DmsFileIdInformation>> DmsFileIds { get; set; } = dmsFileIds;

    public readonly HashSet<string> ValidLowercaseFirstNames = validLowercaseFirstNames;
    
    public IFileService FileService { get; set; } = fileService;
    
    public ICacheService CacheService { get; set; } = cacheService;

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
            CacheService,
            RegionCode,
            MaxPagesToProcessWhenOcrNeeded,
            NaldLinkedLicenceHelper);
    }
}