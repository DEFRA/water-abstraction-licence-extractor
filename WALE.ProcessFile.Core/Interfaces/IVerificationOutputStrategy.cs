using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IVerificationOutputStrategy
{
    string SectionName { get; }
    void HandleVerifications(OutputListDataItem listRow, LicenceVerificationLookups sectionVerificationLookups, Guid fileId, string licenceNumber);
}