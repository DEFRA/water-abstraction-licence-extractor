namespace WRADI.Core.AbstractionLicence.Models;

public class AggregateWithContext : Aggregate
{
    public static AggregateWithContext FromAggregate(Aggregate aggregate)
    {
        var returnItem = new AggregateWithContext();
        CloneProperties(aggregate, returnItem);
        
        return returnItem;
    }
}