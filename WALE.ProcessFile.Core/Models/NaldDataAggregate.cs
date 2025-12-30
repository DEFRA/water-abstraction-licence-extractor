namespace WALE.ProcessFile.Core.Models;

public class NaldDataAggregate
{
    public string? Type { get; init; }
    public double? AnnualQty { get; init; }
    public double? DailyQty { get; init; }
    public double? HourlyQty { get; init; }
    public double? InstQty { get; init; }
    public string? Condition { get; init; }
    public long? ConditionId { get; init; }

    public override string ToString()
    {
        return $"{ConditionId}{Condition}{Type}{AnnualQty}{DailyQty}{HourlyQty}{InstQty}";
    }
}