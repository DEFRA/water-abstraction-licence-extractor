using WALE.ProcessFile.Core.Interfaces;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Interfaces;

namespace WRADI.ProcessFile.Cmd.AbstractionLicence;

public class ConfiguredServices
{
    public IOutputService? OutputService { get; set; }
    
    public IAbstractionLicenceOutputService? AbstractionLicenceOutputService { get; set; }
    
    public ICacheService? CacheService { get; init; }
    
    public IAbstractionLicenceCacheService? AbstractionLicenceCacheService { get; init; }
    
    public ILicenceNumberService? LicenceNumberService { get; set; }
    
    public List<IPdfDataExtractorService>? PdfDataExtractorServices { get; init; }
    
    public INaldDataLookupService? NaldDataLookupService { get; init; }
    
    public int MaxConcurrentScrapers { get; init; }
    
    public string? OutputFolder { get; init; }
    
    public bool RegenerateMappingJson { get; init; }
    
    public IFileService? FileService { get; init; }
    
    public string? ReportTemplatePath { get; init; }
    
    public bool LoadAiJs { get; init; }
    
    public string? ListDataPath { get; init; }
    
    public string? ProcessRunsDataPath { get; init; }
    
    public string? InternalDataPath { get; init; }
    
    public string? LicenceDataPath { get; init; }
    
    public string? LicenceSetsDataPath { get; init; }
    
    public string? ThumbnailImageDataPath { get; init; }
    
    public string? FullImageDataPath { get; init; }
    
    public string? DmsReportPath { get; init; }
    
    public bool RefreshCache { get; init; }

    public int DelayPerProcessMs { get; set; }
}