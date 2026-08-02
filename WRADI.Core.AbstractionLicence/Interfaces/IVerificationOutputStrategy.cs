using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Interfaces;

public interface IVerificationOutputStrategy
{
    string SectionName { get; }
    
    void HandleVerifications(
        OutputListDataItem listRow,
        LicenceVerificationLookups sectionVerificationLookups,
        Guid fileId,
        string licenceNumber,
        Dictionary<Guid, string> fileIdToLicenceNumberMapping);
}