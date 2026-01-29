namespace WALE.ProcessFile.Core.Models;

public class NaldDataPeriod
{
    public int? PeriodStartDay { get; set; }
    
    public int? PeriodStartMonth { get; set; }
    
    public int? PeriodEndDay { get; set; }
    
    public int? PeriodEndMonth { get; set; }

    public override string ToString()
    {
        return $"{PeriodStartDay}/{PeriodStartMonth}-{PeriodEndDay}/{PeriodEndMonth}";
    }
}