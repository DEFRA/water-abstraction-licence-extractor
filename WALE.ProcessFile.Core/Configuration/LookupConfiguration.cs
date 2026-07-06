using System.Collections.Concurrent;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Configuration;

public class LookupConfiguration(
    List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
    Dictionary<string, DmsFileData> allDmsData,
    HashSet<string> validLowercaseFirstNames,
    IFileService fileService,
    ICacheService cacheService,
    int regionId,
    int maxPagesToProcessWhenOcrNeeded = 20,
    int skipFileIfMoreThenPages = 30,
    object? naldLinkedLicenceHelper = null)
{
    public List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels { get; } = labels;

    public Dictionary<string, DmsFileData> AllDmsData { get; set; } = allDmsData;
    
    public readonly HashSet<string> ValidLowercaseFirstNames = validLowercaseFirstNames;
    
    public IFileService FileService { get; set; } = fileService;
    
    public ICacheService CacheService { get; set; } = cacheService;

    public int RegionId { get; set; } = regionId;

    public readonly int MaxPagesToProcessWhenOcrNeeded = maxPagesToProcessWhenOcrNeeded;
    
    public readonly int SkipFileWhenMoreThenPages = skipFileIfMoreThenPages;

    public object? NaldLinkedLicenceHelper { get; set; } = naldLinkedLicenceHelper;
    
    public LookupConfiguration Clone()
    {
        return new LookupConfiguration(
            Labels,
            AllDmsData,
            ValidLowercaseFirstNames,
            FileService,
            CacheService,
            RegionId,
            MaxPagesToProcessWhenOcrNeeded,
            SkipFileWhenMoreThenPages,
            NaldLinkedLicenceHelper);
    }
}