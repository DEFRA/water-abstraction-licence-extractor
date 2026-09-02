using WRADI.Core.AbstractionLicence.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Strategies;

public class LinkedLicencesVerificationLicenceMergeStrategy : IVerificationLicenceMergeStrategy
{
    public string SectionName => "Linked Licences";

    public Licence ApplyVerifications(Licence licence, LicenceVerificationLookups sectionVerificationLookups,
        Guid fileId, Dictionary<Guid, string> fileIdToLicenceNumberMapping)
    {
        var hasOutgoingVerifications =
            sectionVerificationLookups.ByFileId.TryGetValue(fileId, out var outgoingVerifications);

        var licenceNumber = licence.LicenceNumber?.Value ?? string.Empty;
        var hasIncomingVerifications =
            sectionVerificationLookups.ByItemId.TryGetValue(licenceNumber, out var incomingVerifications);

        if (!hasOutgoingVerifications && !hasIncomingVerifications)
        {
            return licence;
        }

        var linkedLicences = licence.LinkedLicences?.ToList() ?? [];

        if (hasOutgoingVerifications)
        {
            LinkedLicenceVerificationMergeHelper.MergeOutgoing(
                linkedLicences, outgoingVerifications!, licence.LinkedLicences, licence.ProcessRunId);
        }

        if (hasIncomingVerifications)
        {
            LinkedLicenceVerificationMergeHelper.MergeIncoming(linkedLicences, incomingVerifications!,
                fileIdToLicenceNumberMapping);
        }

        licence.LinkedLicences = linkedLicences.Where(ll => ll.ContainedIn?.Length > 0).ToArray();

        return licence;
    }
}