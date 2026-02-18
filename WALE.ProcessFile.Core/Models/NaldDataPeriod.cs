namespace WALE.ProcessFile.Core.Models;

public class NaldDataPeriod
{
    public List<int> PurposeIds { get; init; } = [];
    
    public int? PeriodStartDay { get; init; }
    
    public int? PeriodStartMonth { get; init; }
    
    public int? PeriodEndDay { get; init; }
    
    public int? PeriodEndMonth { get; init; }

    public override string ToString()
    {
        return $"{PeriodStartDay}/{PeriodStartMonth}-{PeriodEndDay}/{PeriodEndMonth}";
    }
}