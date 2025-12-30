namespace WALE.ProcessFile.Core.Models;

public class NaldDataPeriod
{
    public string? PeriodStart { get; set; }
    
    public string? PeriodEnd { get; set; }

    public override string ToString()
    {
        return $"{PeriodStart}{PeriodEnd}";
    }
}