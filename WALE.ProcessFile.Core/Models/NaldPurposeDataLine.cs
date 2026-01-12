using CsvHelper.Configuration.Attributes;

namespace WALE.ProcessFile.Core.Models;

public class NaldPurposeDataLine
{
    [Index(0)]
    public string? Id { get; set; }
    
    [Index(1)]
    public string? LicenceId { get; set; }

    [Index(12)]
    public double? AnnualQty { get; set; }

    // TODO 13 is units
    
    [Index(14)]
    public double? DailyQty { get; set; }

    // TODO 15 is units
    
    [Index(16)]
    public double? HourlyQty { get; set; }

    // TODO 17 is units
    
    [Index(18)]
    public double? InstQty { get; set; }
    
    // TODO 19 is units
    
    [Index(25)]
    public string? Notes { get; set; }
    
    [Index(26)]
    public string? FgacRegionCode { get; set; }
}