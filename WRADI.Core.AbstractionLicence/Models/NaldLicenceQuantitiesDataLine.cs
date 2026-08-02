namespace WRADI.Core.AbstractionLicence.Models;

public class NaldLicenceQuantitiesDataLine
{
    public string LookupKey => $"{FgacRegionCode}|{AabvAablId}";
    public int? AabvAablId { get; set; }
    public short AabvIssueNo { get; set; }
    public short AabvIncrNo { get; set; }
    public double MaxAnnualQty { get; set; }
    public double MaxDailyQty { get; set; }
    public char? AggregatedInd { get; set; }
    public char? PurpPointsInd { get; set; }
    public char? UserValidInd { get; set; }
    public short FgacRegionCode { get; set; }
}