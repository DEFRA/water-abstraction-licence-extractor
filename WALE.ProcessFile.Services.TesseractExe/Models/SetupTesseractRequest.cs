using WALE.ProcessFile.Core.Enums;

namespace WALE.ProcessFile.Services.TesseractExe.Models;

public class SetupTesseractRequest
{
    public PageSegMode PageSegMode { get; set; }
    
    public string? BytesMode { get; set; }
    
    public string? CacheFolderOrApiUrl { get; set; }
    
    public string? OutputFolder { get; set; }
    
    public string? TessDataPath { get; set; }
}