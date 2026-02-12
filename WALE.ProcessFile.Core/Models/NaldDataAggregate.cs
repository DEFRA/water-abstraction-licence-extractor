namespace WALE.ProcessFile.Core.Models;

public class NaldDataAggregate
{
    public string? Type { get; init; }

    public double? AnnualQty { get; init; }
    
    public char? AnnualQtyUsability { get; init; }
    
    public double? DailyQty { get; init; }
    
    public char? DailyQtyUsability { get; init; }
    
    public double? HourlyQty { get; init; }
    
    public char? HourlyQtyUsability { get; init; }
    
    public double? InstQty { get; init; }
    
    public char? InstQtyUsability { get; init; }
    
    public string? Condition { get; init; }
    
    public long? ConditionId { get; init; }
    
    public int? PeriodStartDay { get; set; }
    
    public int? PeriodStartMonth { get; set; }
    
    public int? PeriodEndDay { get; set; }
    
    public int? PeriodEndMonth { get; set; }

    public override string ToString()
    {
        return $"{ConditionId}{Condition}{Type}{AnnualQty}{AnnualQtyUsability}{DailyQty}{DailyQtyUsability}{HourlyQty}{HourlyQtyUsability}{InstQty}{InstQtyUsability}|{PeriodStartDay}/{PeriodStartMonth}-{PeriodEndDay}/{PeriodEndMonth}";
    }
}