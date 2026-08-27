namespace WRADI.Core.AbstractionLicence.Models;

public class NaldAbstractionData
{
    public int Id { get; set; }
    public string? LicenceNumber { get; set; }
    public string? LicenceIdCharsAndDigitsOnly { get; set; }
    public DateTime? ExpiryDate { get; init; }
    public DateTime? RevocationDate { get; init; }
    public DateTime? LapsedDate { get; init; }
    public DateTime? OrigEffDate { get; init; }
    public DateTime? OrigSigDate { get; init; }
    public List<NaldDataAggregate> AggregateConditions { get; init; } = [];
    public List<NaldDataPoint> Points { get; init; } = [];
    public List<NaldDataPeriod> Periods { get; init; } = [];
    public List<NaldDataPurpose> Purposes { get; init; } = [];
    public short FgacRegionCode { get; set; }
    public DateTime? EffStDate { get; set; }
    public DateTime? EffEndDate { get; set; }
    public DateTime? LicSigDate { get; set; }
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
    public string? ArepEiucCode { get; set; }
    public bool? HasAggCondition { get; set; }
}