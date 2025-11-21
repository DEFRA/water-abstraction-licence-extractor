using WALE.ProcessFile.Models.Interfaces;

namespace WaterAbstractionLicenseExtractor.Cmd;

public class ConfiguredServices
{
    public IOutputService? OutputService { get; set; }
    
    public ICacheService? CacheService { get; set; }
    
    public List<IPdfDataExtractorService>? PdfDataExtractorServices { get; set; }
    
    public string? FileMappingPath { get; set; }
    
    public int MaxConcurrentScrapers { get; set; }
    
    public string? OutputFolder { get; set; }
    
    public bool RegenerateMappingJson { get; set; }
    
    public string? PdfFolderPath { get; set; }
    
    public string? ReportTemplatePath { get; set; }
    
    public bool LoadAiJs { get; set; }
    
    public string? ListDataPath { get; set; }
    
    public string? ProcessRunsDataPath { get; set; }
    
    public string? InternalDataPath { get; set; }
    
    public string? LicenceDataPath { get; set; }
    
    public string? LicenceSetsDataPath { get; set; }
    
    public string? ThumbnailImageDataPath { get; set; }
    
    public string? FullImageDataPath { get; set; }
    
    public bool RefreshCache{ get; set; }
}