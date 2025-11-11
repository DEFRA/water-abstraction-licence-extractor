namespace WALE.ProcessFile.Models;

public class NaldDataAggregate
{
    public double? AnnualQty { get; set; }
    public double? DailyQty { get; set; }
    public double? HourlyQty { get; set; }
    public double? InstQty { get; set; }
    public string? Condition { get; init; }

    public override string ToString()
    {
        return $"{Condition}{AnnualQty}{DailyQty}{HourlyQty}{InstQty}";
    }
}