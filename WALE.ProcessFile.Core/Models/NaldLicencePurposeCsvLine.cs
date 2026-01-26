using CsvHelper.Configuration.Attributes;

namespace WALE.ProcessFile.Core.Models;

public class NaldLicencePurposeCsvLine
{
    [Index(0)] // ID
    public string? Id { get; set; }
    
    [Index(1)] // AABV_AABL_ID
    public string? InternalLicenceId { get; set; }

    [Index(5)] // APUR_APSE_CODE
    public string? PurposeCode { get; set; }
    
    [Index(6)] // APUR_APUS_CODE
    public int? PurposeCodeId { get; set; }
    
    [Index(7)] // PERIOD_ST_DAY
    public int? PeriodStartDay { get; set; }
    
    [Index(8)] // PERIOD_ST_MONTH
    public int? PeriodStartMonth { get; set; }
    
    [Index(9)] // PERIOD_END_DAY
    public int? PeriodEndDay { get; set; }
    
    [Index(10)] // PERIOD_END_MONTH
    public int? PeriodEndMonth { get; set; }

    [Index(12)] // ANNUAL_QTY
    public string? AnnualQty { get; set; }
    
    [Index(13)] // ANNUAL_QTY_USABILITY
    public string? AnnualQtyUnits { get; set; }
    
    [Index(14)] // DAILY_QTY
    public string? DailyQty { get; set; }

    [Index(15)] // DAILY_QTY_USABILITY
    public string? DailyQtyUnits { get; set; }
    
    [Index(16)] // HOURLY_QTY
    public string? HourlyQty { get; set; }

    [Index(15)] // HOURLY_QTY_USABILITY
    public string? HourlyQtyUnits { get; set; }
    
    [Index(18)] // INST_QTY
    public string? InstQty { get; set; }
    
    [Index(19)] // INST_QTY_USABILITY
    public string? InstQtyUnits { get; set; }
    
    [Index(26)] // FGAC_REGION_CODE
    public string? FgacRegionCode { get; set; }
}