namespace WALE.ProcessFile.Core.Models;

public class NaldData
{
    public string? Id { get; set; }
    public string? LicenceNumber { get; set; }
    
    public string? LicenceIdCharsAndDigitsOnly { get; set; }
    
    public string? ExpiryDate { get; init; }
    
    public string? VersionStartDate { get; init; }
    
    public List<NaldDataAggregate> AggregateConditions { get; init; } = [];

    public double? LicenceWideAnnualQty { get; set; }

    public double? LicenceWideDailyQty { get; set; }

    public double? LicenceWideHourlyQty { get; set; }

    public double? LicenceWideInstQty { get; set; }
    
    public List<NaldDataPoint> Points { get; init; } = [];
    
    public List<NaldDataPeriod> Periods { get; init; } = [];
    
    public List<NaldDataPurpose> Purposes { get; init; } = [];
    
    public string? FgacRegionCode { get; set; }
}