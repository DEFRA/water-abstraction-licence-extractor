namespace WRADI.Core.AbstractionLicence.Models;

public class AggregateVariation : Aggregate
{
    public string? Difference { get; set; }

    public static AggregateVariation FromAggregate(Aggregate aggregate, string difference)
    {
        var returnItem = new AggregateVariation();
        CloneProperties(aggregate, returnItem);
        returnItem.Difference = difference;
        
        return returnItem;
    }
}