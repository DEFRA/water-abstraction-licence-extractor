namespace WALE.ProcessFile.Core.Models;

public class NaldData
{
    public int Id { get; set; }
    public string? LicenceNumber { get; set; }
    public string? LicenceIdCharsAndDigitsOnly { get; set; }
    public string? ExpiryDate { get; init; }
    public string? RevocationDate { get; init; }
    public string? OrigEffDate { get; init; }
    public string? OrigSigDate { get; init; }
    public List<NaldDataAggregate> AggregateConditions { get; init; } = [];
    public List<NaldDataPoint> Points { get; init; } = [];
    public List<NaldDataPeriod> Periods { get; init; } = [];
    public List<NaldDataPurpose> Purposes { get; init; } = [];
    public short FgacRegionCode { get; set; }
    public string? EffStDate { get; set; }
    public string? EffEndDate { get; set; }
    public string? LicSigDate { get; set; }
    public int? IssueNo { get; set; }
    public int? IncrNo { get; set; }
    public string? AabvType { get; set; }
    public string? Status { get; set; }
    public double? MaxAnnualQty { get; set; }
    public double? MaxDailyQty { get; set; }
    public string? AppNo { get; set; }
    public string? WaAltyCode { get; set; }
    public string? AsrcCode { get; set; }
    public char? QuantityAggregated { get; set; }
    public char? QuantityUserValid { get; set; }
    public string? QuantityPurpPoints { get; set; }
}