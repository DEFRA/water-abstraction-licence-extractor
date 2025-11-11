namespace WALE.ProcessFile.Models;

public class NaldData
{
    public string? LicenceNumber { get; set; }
    public string? ExpiryDate { get; init; }
    public string? VersionStartDate { get; init; }
    public List<NaldDataAggregate> AggregateConditions { get; init; } = [];
    
    public double? LicenceWideAnnualQty { get; set; }
    
    public double? LicenceWideDailyQty { get; set; }
    
    public double? LicenceWideHourlyQty { get; set; }
    
    public double? LicenceWideInstQty { get; set; }
    public List<NaldDataPoint> Points { get; init; } = [];
}