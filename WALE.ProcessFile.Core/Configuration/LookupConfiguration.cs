using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Configuration;

public class LookupConfiguration(
    List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
    HashSet<string> validLowercaseFirstNames,
    IFileService fileService,
    ICacheService cacheService,
    int regionId,
    DateTime requestedAt,
    int currentLockRetryCount = 0,
    int maxPagesToProcessWhenOcrNeeded = 20,
    int skipFileIfMoreThenPages = 30,
    object? naldLinkedLicenceHelper = null,
    bool useLockExclusivity = true)
{
    public List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels { get; } = labels;
    
    public readonly HashSet<string> ValidLowercaseFirstNames = validLowercaseFirstNames;
    
    public IFileService FileService { get; set; } = fileService;
    
    public ICacheService CacheService { get; set; } = cacheService;

    public int RegionId { get; set; } = regionId;

    public readonly int MaxPagesToProcessWhenOcrNeeded = maxPagesToProcessWhenOcrNeeded;
    
    public readonly int SkipFileWhenMoreThenPages = skipFileIfMoreThenPages;

    public object? NaldLinkedLicenceHelper { get; set; } = naldLinkedLicenceHelper;

    public DateTime RequestedAt { get; set; } = requestedAt;

    public int CurrentLockRetryCount { get; set; } = currentLockRetryCount;
    
    public bool UseLockExclusivity { get; set; } = useLockExclusivity;
    
    public LookupConfiguration Clone()
    {
        return new LookupConfiguration(
            Labels,
            ValidLowercaseFirstNames,
            FileService,
            CacheService,
            RegionId,
            RequestedAt,
            CurrentLockRetryCount,
            MaxPagesToProcessWhenOcrNeeded,
            SkipFileWhenMoreThenPages,
            NaldLinkedLicenceHelper,
            UseLockExclusivity);
    }
}