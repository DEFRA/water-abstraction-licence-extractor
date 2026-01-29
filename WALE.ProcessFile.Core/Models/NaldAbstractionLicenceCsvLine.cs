using CsvHelper.Configuration.Attributes;

namespace WALE.ProcessFile.Core.Models;

public class NaldAbstractionLicenceCsvLine
{
    [Index(0)]
    public string? Id { get; set; }

    [Index(1)]
    public string? LicenceNo { get; set; }

    [Index(6)]
    public string? ExpiryDate { get; set; }
    
    [Index(7)]
    public string? OrigEffectiveDate { get; set; }
    
    [Index(8)]
    public string? OrigSignatureDate { get; set; }

    [Index(12)]
    public string? RevDate { get; set; }
    
    [Index(13)]
    public string? LapsedDate { get; set; }
    
    [Index(20)]
    public string? FgacRegionCode { get; set; }
}