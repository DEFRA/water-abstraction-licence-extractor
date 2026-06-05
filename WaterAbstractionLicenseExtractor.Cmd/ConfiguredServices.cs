using WALE.ProcessFile.Core.Interfaces;

namespace WaterAbstractionLicenseExtractor.Cmd;

public class ConfiguredServices
{
    public IOutputService? OutputService { get; set; }
    
    public ICacheService? CacheService { get; init; }
    
    public List<IPdfDataExtractorService>? PdfDataExtractorServices { get; init; }
    
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