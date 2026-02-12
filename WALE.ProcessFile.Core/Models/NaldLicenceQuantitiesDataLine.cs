namespace WALE.ProcessFile.Core.Models;

public class NaldLicenceQuantitiesDataLine
{
    public string LookupKey => $"{FgacRegionCode}|{AabvAablId}";
    public int? Id { get; set; }
    public int? AabvAablId { get; set; }
    public int? AabvIssueNo { get; set; }
    public int? AabvIncrNo { get; set; }
    public string? MaxAnnualQty { get; set; }
    public string? MaxDailyQty { get; set; }
    public string? AggregatedInd { get; set; }
    public string? PurpPointsInd { get; set; }
    public string? UserValidInd { get; set; }
    public string? FgacRegionCode { get; set; }
}
