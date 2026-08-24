namespace WRADI.Core.AbstractionLicence.Models;

public class AggregateWithContext : Aggregate
{
    public static AggregateWithContext FromAggregate(Aggregate aggregate)
    {
        return new AggregateWithContext
        {
            Points = aggregate.Points,
            Purposes = aggregate.Purposes,

            DocumentIdentifier = aggregate.DocumentIdentifier,
            TimePeriod = aggregate.TimePeriod,
            TimeCutoff = aggregate.TimeCutoff,
            Limits = aggregate.Limits,

            AggregateSetId = aggregate.AggregateSetId,
            SourceLicenceNumber = aggregate.SourceLicenceNumber,
            SourceLicenceVersionId = aggregate.SourceLicenceVersionId,
            PrimaryType = aggregate.PrimaryType,
            SubType = aggregate.SubType,
            NaldType = aggregate.NaldType,
            LinkedLicences = aggregate.LinkedLicences
        };
    }
}