namespace WALE.ProcessFile.Core.Models;

public class NaldDataAggregate
{
    public string? Type { get; init; }

    public double? AnnualQty { get; init; }
    
    public string? AnnualQtyUnits { get; init; }
    
    public double? DailyQty { get; init; }
    
    public string? DailyQtyUnits { get; init; }
    
    public double? HourlyQty { get; init; }
    
    public string? HourlyQtyUnits { get; init; }
    
    public double? InstQty { get; init; }
    
    public string? InstQtyUnits { get; init; }
    
    public string? Condition { get; init; }
    
    public long? ConditionId { get; init; }
    
    public int? PeriodStartDay { get; set; }
    
    public int? PeriodStartMonth { get; set; }
    
    public int? PeriodEndDay { get; set; }
    
    public int? PeriodEndMonth { get; set; }

    public override string ToString()
    {
        return $"{ConditionId}{Condition}{Type}{AnnualQty}{AnnualQtyUnits}{DailyQty}{DailyQtyUnits}{HourlyQty}{HourlyQtyUnits}{InstQty}{InstQtyUnits}|{PeriodStartDay}/{PeriodStartMonth}-{PeriodEndDay}/{PeriodEndMonth}";
    }
}