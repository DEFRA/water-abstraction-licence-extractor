using CsvHelper.Configuration.Attributes;

namespace WALE.ProcessFile.Core.Models;

public class NaldAbstractionLicencesDataLine
{
    [Index(0)]
    public string? Id { get; set; }

    [Index(1)]
    public string? LicenceNo { get; set; }

    [Index(6)]
    public string? ExpiryDate { get; set; }
    
    [Index(7)]
    public string? OrigEffectiveDate { get; set; }

    [Index(20)]
    public string? FgacRegionCode { get; set; }
}