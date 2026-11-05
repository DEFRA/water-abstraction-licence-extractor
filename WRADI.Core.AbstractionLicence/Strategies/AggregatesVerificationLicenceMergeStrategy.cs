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

        var merged = AggregateVerificationMergeHelper.MergeAggregates(
            licence.AbstractionLimits.Aggregates, aggregateVerifications);

        return licence.CloneWithAbstractionLimits(
            new AbstractionLimits
            {
                Individual = licence.AbstractionLimits.Individual,
                Aggregates = merged.ToArray()
            });
    }
}