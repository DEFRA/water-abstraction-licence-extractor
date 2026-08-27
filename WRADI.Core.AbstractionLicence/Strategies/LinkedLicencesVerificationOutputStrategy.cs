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

        if (!hasOutgoingVerifications && !hasIncomingVerifications)
        {
            return;
        }

        var linkedLicences = listRow.linkedLicences?.ToList() ?? [];

        if (hasOutgoingVerifications)
        {
            var sectionSummaries = LinkedLicenceVerificationMergeHelper.MergeOutgoing(
                linkedLicences, outgoingVerifications!, listRow.linkedLicences, listRow.processRunId);

            var summaries = listRow.licenceSectionVerifications?.ToList() ?? [];
            summaries.Add(new LicenceSectionVerificationSummary
            {
                LicenceSectionName = SectionName,
                LicenceSectionItems = sectionSummaries.ToArray()
            });
            listRow.licenceSectionVerifications = summaries.ToArray();
        }

        if (hasIncomingVerifications)
        {
            LinkedLicenceVerificationMergeHelper.MergeIncoming(linkedLicences, incomingVerifications!,
                fileIdToLicenceNumberMapping);
        }

        listRow.linkedLicences = linkedLicences.Where(ll => ll.ContainedIn?.Length > 0).ToArray();
    }
}