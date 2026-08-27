using WRADI.Core.AbstractionLicence.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Strategies;

public class AggregatesVerificationLicenceMergeStrategy : IVerificationLicenceMergeStrategy
{
    public string SectionName => "Aggregates";

    public Licence ApplyVerifications(Licence licence, LicenceVerificationLookups sectionVerificationLookups,
        Guid fileId, Dictionary<Guid, string> fileIdToLicenceNumberMapping)
    {
        if (!sectionVerificationLookups.ByFileId.TryGetValue(fileId, out var aggregateVerifications))
        {
            return licence;
        }

        var merged = AggregateVerificationMergeHelper.MergeAggregates(
            licence.AbstractionLimits.Aggregates, aggregateVerifications);

        return licence.CloneWithAbstractionLimits(new AbstractionLimits
        {
            Individual = licence.AbstractionLimits.Individual,
            Aggregates = merged.ToArray()
        });
    }
}