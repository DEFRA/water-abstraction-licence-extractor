using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Strategies;

public class LinkedLicencesVerificationOutputStrategy : IVerificationOutputStrategy
{
    public string SectionName => "Linked Licences";

    public void HandleVerifications(OutputListDataItem listRow, LicenceVerificationLookups verificationLookups,
        Guid fileId, string licenceNumber, Dictionary<Guid, string> fileIdToLicenceNumberMapping)
    {
        var hasOutgoingVerifications =
            verificationLookups.ByFileId.TryGetValue(fileId, out var outgoingVerifications);

        var hasIncomingVerifications =
            verificationLookups.ByItemId.TryGetValue(licenceNumber, out var incomingVerifications);

        var linkedLicences = listRow.linkedLicences?.ToList() ?? [];

        List<LicenceSectionItemSummary> sectionSummaries = hasOutgoingVerifications
            ? LinkedLicenceVerificationMergeHelper.MergeOutgoing(
                linkedLicences, outgoingVerifications!, listRow.linkedLicences, listRow.processRunId)
            : [];

        if (hasIncomingVerifications)
        {
            LinkedLicenceVerificationMergeHelper.MergeIncoming(linkedLicences, incomingVerifications!,
                fileIdToLicenceNumberMapping);
        }

        if (hasOutgoingVerifications || hasIncomingVerifications)
        {
            listRow.linkedLicences = linkedLicences.Where(ll => ll.ContainedIn?.Length > 0).ToArray();
        }

        // Add a dummy, flagged entry for any LL without any verifications
        var existingItemIds = sectionSummaries
            .Select(s => s.LicenceSectionItemId)
            .ToHashSet();

        foreach (var linkedLicence in listRow.linkedLicences ?? [])
        {
            var linkedLicenceNumber = linkedLicence.LicenceNumber;
            if (string.IsNullOrWhiteSpace(linkedLicenceNumber) || !existingItemIds.Add(linkedLicenceNumber))
            {
                continue;
            }

            // Only add the dummy entry if the LL is outgoing
            if (linkedLicence.ContainedIn?.Any(c => c.Direction == InformationDirection.Outgoing) != true)
            {
                continue;
            }

            sectionSummaries.Add(new LicenceSectionItemSummary
            {
                LicenceSectionItemId = linkedLicenceNumber,
                VerificationTypes = [],
                ScrapedDataIsDifferent = true
            });
        }

        if (sectionSummaries.Count == 0)
        {
            return;
        }

        var summaries = listRow.licenceSectionVerifications?.ToList() ?? [];
        summaries.Add(new LicenceSectionVerificationSummary
        {
            LicenceSectionName = SectionName,
            LicenceSectionItems = sectionSummaries.ToArray()
        });
        listRow.licenceSectionVerifications = summaries.ToArray();
    }
}