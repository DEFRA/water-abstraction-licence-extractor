namespace WRADI.Core.AbstractionLicence.Models;

public class NaldLicencePurposeDataLine
{
    public string LicenceIdLookupKey => $"{FgacRegionCode}|{AabvAablId}";
    public string PurposeIdLookupKey => $"{FgacRegionCode}|{Id}";
    public string? Id { get; set; }
    public int AabvAablId { get; set; }
    public short AabvIssueNo { get; set; }
    public short AabvIncrNo { get; set; }
    public string? ApurApprCode { get; set; }
    public string? ApurApseCode { get; set; }
    public short ApurApusCode { get; set; }
    public int? PeriodStartDay { get; set; }
    public int? PeriodStartMonth { get; set; }
    public int? PeriodEndDay { get; set; }
    public int? PeriodEndMonth { get; set; }
    public string? AmomCode { get; set; }
    public string? AnnualQty { get; set; }
    public char? AnnualQtyUsability { get; set; }
    public string? DailyQty { get; set; }
    public char? DailyQtyUsability { get; set; }
    public string? HourlyQty { get; set; }
    public char? HourlyQtyUsability { get; set; }
    public string? InstQty { get; set; }
    public char? InstQtyUsability { get; set; }
    public string? Notes { get; set; }
    public short FgacRegionCode { get; set; }
    public string? PurpPrimDescr { get; set; }
    public string? PurpSecDescr { get; set; }
    public string? PurpUseDescr { get; set; }
}