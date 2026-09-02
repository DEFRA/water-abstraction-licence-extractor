using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Configuration;

public class LookupConfiguration(
    List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
    HashSet<string> validLowercaseFirstNames,
    IFileService fileService,
    ICacheService cacheService,
    IOutputService outputService,
    ILicenceNumberServiceCore licenceNumberService,
    IDmsLookupService dmsLookupService,
    int regionId,
    DateTime requestedAt,
    int currentLockRetryCount = 0,
    int maxPagesToProcessWhenOcrNeeded = 20,
    int skipFileIfMoreThenPages = 30,
    int skipFileIfMoreThenImages = 50,
    int lineHeight = 9,
    int minimumRowsForDigital = 100,
    object? naldLinkedLicenceHelper = null,
    bool useLockExclusivity = true,
    bool lockInProcess = false)
{
    public List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels { get; } = labels;
    
    public readonly HashSet<string> ValidLowercaseFirstNames = validLowercaseFirstNames;
    
    public IFileService FileService { get; set; } = fileService;
    
    public ICacheService CacheService { get; set; } = cacheService;
    
    public IOutputService OutputService { get; set; } = outputService;

    public ILicenceNumberServiceCore LicenceNumberService { get; set; } = licenceNumberService;
    
    public IDmsLookupService DmsLookupService { get; set; } = dmsLookupService;

    public int RegionId { get; set; } = regionId;

    public readonly int MaxPagesToProcessWhenOcrNeeded = maxPagesToProcessWhenOcrNeeded;
    
    public readonly int SkipFileWhenMoreThenPages = skipFileIfMoreThenPages;

    public readonly int SkipFileWhenMoreThenImages = skipFileIfMoreThenImages;
    
    public object? NaldLinkedLicenceHelper { get; set; } = naldLinkedLicenceHelper;

    public DateTime RequestedAt { get; set; } = requestedAt;

    public int CurrentLockRetryCount { get; set; } = currentLockRetryCount;
    
    public bool UseLockExclusivity { get; set; } = useLockExclusivity;
    
    public bool LockInProcess { get; set; } = lockInProcess;
    
    public int LineHeight { get; set; } = lineHeight;
    public int MinimumRowsForDigital { get; set; } = minimumRowsForDigital;

    public LookupConfiguration Clone()
    {
        return new LookupConfiguration(
            Labels,
            ValidLowercaseFirstNames,
            FileService,
            CacheService,
            OutputService,
            LicenceNumberService,
            DmsLookupService,
            RegionId,
            RequestedAt,
            CurrentLockRetryCount,
            MaxPagesToProcessWhenOcrNeeded,
            SkipFileWhenMoreThenPages,
            SkipFileWhenMoreThenImages,
            LineHeight,
            MinimumRowsForDigital,
            NaldLinkedLicenceHelper,
            UseLockExclusivity,
            LockInProcess);
    }
}