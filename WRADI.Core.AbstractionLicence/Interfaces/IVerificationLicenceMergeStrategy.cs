using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Interfaces;

public interface IVerificationLicenceMergeStrategy
{
    string SectionName { get; }

    Licence ApplyVerifications(
        Licence licence,
        LicenceVerificationLookups sectionVerificationLookups,
        Guid fileId,
        Dictionary<Guid, string> fileIdToLicenceNumberMapping);
}
