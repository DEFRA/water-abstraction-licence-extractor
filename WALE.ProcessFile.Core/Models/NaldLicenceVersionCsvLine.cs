using CsvHelper.Configuration.Attributes;

namespace WALE.ProcessFile.Core.Models;

public class NaldLicenceVersionCsvLine
{
    [Index(0)] // AABL_ID
    public int? AablId { get; set; }
    
    [Index(1)] // ISSUE_NO
    public int? IssueNo { get; set; }

    [Index(2)] // INCR_NO
    public int? IncrNo { get; set; }
    
    [Index(3)] // AABV_TYPE
    public string? AabvType { get; set; }
    
    [Index(4)] // EFF_ST_DATE
    public string? EffStDate { get; set; }
    
    [Index(5)] // STATUS
    public string? Status { get; set; }
    
    [Index(14)] // LIC_SIG_DATE
    public string? LicSigDate { get; set; }
    
    [Index(17)] // EFF_END_DATE
    public string? EffEndDate { get; set; }
    
    [Index(23)] // FGAC_REGION_CODE
    public string? FgacRegionCode { get; set; }
}