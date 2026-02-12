namespace WALE.ProcessFile.Core.Models;

public class NaldLicencePurposeDataLine
{
    public string LicenceIdLookupKey => $"{FgacRegionCode}|{AabvAablId}";
    public string PurposeIdLookupKey => $"{FgacRegionCode}|{Id}";
    public string? Id { get; set; }
    public string? AabvAablId { get; set; }
    public int? AabvIssueNo { get; set; }
    public int? AabvIncrNo { get; set; }
    public string? ApurApprCode { get; set; }
    public string? ApurApseCode { get; set; }
    public int? ApurApusCode { get; set; }
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
    public DateTime? TimeLtdStartDate { get; set; }
    public DateTime? TimeLtdEndDate { get; set; }
    public string? Lands { get; set; }
    public string? ArecCode { get; set; }
    public int? DispOrd { get; set; }
    public string? Notes { get; set; }
    public string? FgacRegionCode { get; set; }
    public string? PurpPrimDescr { get; set; }
    public string? PurpSecDescr { get; set; }
    public string? PurpUseDescr { get; set; }
}
