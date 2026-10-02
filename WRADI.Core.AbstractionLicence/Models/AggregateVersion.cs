namespace WRADI.Core.AbstractionLicence.Models;

public class AggregateVersion : Aggregate
{
    public string? Difference { get; set; }

    public static AggregateVersion FromAggregate(Aggregate aggregate, string difference)
    {
        return new AggregateVersion
        {
            AggregateSetId = aggregate.AggregateSetId,
            ContainedIn = aggregate.ContainedIn,
            Difference = difference,
            DocumentIdentifier = aggregate.DocumentIdentifier,
            IsExplicitlyAggregate = aggregate.IsExplicitlyAggregate,
            Limits = aggregate.Limits,
            LinkedLicences = aggregate.LinkedLicences,
            NaldType = aggregate.NaldType,
            PrimaryType = aggregate.PrimaryType,
            Purposes = aggregate.Purposes,
            Points = aggregate.Points,
            SourceLicenceNumber = aggregate.SourceLicenceNumber,
            SourceLicenceVersionId = aggregate.SourceLicenceVersionId,
            SubType = aggregate.SubType,
            TimePeriod = aggregate.TimePeriod,
            TimeCutoff = aggregate.TimeCutoff,
            OtherVersions = aggregate.OtherVersions
        };
    }
}