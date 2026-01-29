using CsvHelper.Configuration.Attributes;

namespace WALE.ProcessFile.Core.Models;

public class NaldLicencePointCsvLine
{
    [Index(0)] // AABP_ID
    public string? AabpId { get; set; }
    
    [Index(1)] // AAIP_ID
    public string? AaipId { get; set; }
    
    [Index(2)] // AMOA_CODE
    public string? AmoaCode { get; set; }
    
    [Index(3)] // NOTES
    public string? Notes { get; set; }
    
    [Index(4)] // FGAC_REGION_CODE
    public string? FgacRegionCode { get; set; }
}