namespace WRADI.Core.AbstractionLicence.Models;

public class AggregateVersion : Aggregate
{
    public string? Difference { get; set; }

    public static AggregateVersion FromAggregate(Aggregate aggregate, string difference)
    {
        var returnItem = new AggregateVersion();
        CloneProperties(aggregate, returnItem);
        returnItem.Difference = difference;
        
        return returnItem;
    }
}