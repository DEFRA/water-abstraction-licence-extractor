using WALE.ProcessFile.Services.Interfaces;

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
}