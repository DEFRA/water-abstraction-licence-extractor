using WRADI.Core.AbstractionLicence.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Strategies;

public class AggregatesVerificationLicenceMergeStrategy : IVerificationLicenceMergeStrategy
{
    public string SectionName => "Aggregates";

    public Licence ApplyVerifications(Licence licence, LicenceVerificationLookups sectionVerificationLookups,
        Guid fileId)
    {
        var licenceNumber = licence.LicenceNumber?.Value ?? string.Empty;
        if (!sectionVerificationLookups.BySourceLicence.TryGetValue((fileId, licenceNumber), out var aggregateVerifications))
        {
            return licence;
        }

        var scrapedAggregates = licence.AbstractionLimits.Aggregates;
        var (_, merged, _) = AggregateVerificationMergeHelper.Merge(
            scrapedAggregates?.Select(a => a.Id),
            scrapedAggregates,
            aggregateVerifications,
            licence.ProcessRunId);

        return licence.CloneWithAbstractionLimits(
            new AbstractionLimits
            {
                Individual = licence.AbstractionLimits.Individual,
                Aggregates = merged.ToArray()
            });
    }
}