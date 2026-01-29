using CsvHelper.Configuration.Attributes;

namespace WALE.ProcessFile.Core.Models;

public class NaldLicenceQuantitiesCsvLine
{
    [Index(0)] // ID
    public int? Id { get; set; }
    
    [Index(1)] // AABV_AABL_ID
    public int? AabvAablId { get; set; }
    
    [Index(2)] // AABV_ISSUE_NO
    public int? AabvIssueNo { get; set; }
    
    [Index(3)] // AABV_INCR_NO
    public int? AabvIncrNo { get; set; }

    [Index(4)] // MAX_ANNUAL_QTY
    public string? MaxAnnualQty { get; set; }
    
    [Index(5)] // MAX_DAILY_QTY
    public string? MaxDailyQty { get; set; }
    
    [Index(9)] // FGAC_REGION_CODE
    public string? FgacRegionCode { get; set; }
}