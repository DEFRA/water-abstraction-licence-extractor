using WRADI.Core.AbstractionLicence.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Strategies;

public class AggregatesVerificationOutputStrategy : IVerificationOutputStrategy
{
    public string SectionName => "Aggregates";

    public void HandleVerifications(OutputListDataItem listRow, LicenceVerificationLookups verificationLookups,
        Guid fileId, string licenceNumber, Dictionary<Guid, string> fileIdToLicenceNumberMapping)
    {
        if (!verificationLookups.ByFileId.TryGetValue(fileId, out var outgoingVerifications))
        {
            return; // no incoming-link concept for Aggregates — ByItemId is never consulted
        }

        var (aggregateIds, summaries) = AggregateVerificationMergeHelper.MergeAggregateIds(
            listRow.aggregateIds, outgoingVerifications);

        listRow.aggregateIds = aggregateIds.ToArray(); // aggregatesCount recomputes automatically from this

        var sectionSummaries = listRow.licenceSectionVerifications?.ToList() ?? [];
        sectionSummaries.Add(new LicenceSectionVerificationSummary
        {
            LicenceSectionName = SectionName,
            LicenceSectionItems = summaries.ToArray()
        });
        listRow.licenceSectionVerifications = sectionSummaries.ToArray();
    }
}