namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class AggregateWithContext : Aggregate
{
    private new string? LicenceNumber
    {
        get => base.LicenceNumber;
        set => base.LicenceNumber = value;
    }

    private new string? LicenceVersionId
    {
        get => base.LicenceVersionId;
        set => base.LicenceVersionId = value;
    }

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
            ContainedIn = aggregate.ContainedIn,

            AggregateSetId = aggregate.AggregateSetId,
            LicenceNumber = aggregate.LicenceNumber,
            LicenceVersionId = aggregate.LicenceVersionId,
            PrimaryType = aggregate.PrimaryType,
            SubType = aggregate.SubType,
            NaldType = aggregate.NaldType,
            LinkedLicences = aggregate.LinkedLicences
        };
    }
}