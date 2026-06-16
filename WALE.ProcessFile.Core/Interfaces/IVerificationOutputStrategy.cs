using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IVerificationOutputStrategy
{
    string SectionName { get; }
    void HandleVerifications(IEnumerable<LicenceSectionVerification> verifications, OutputListDataItem listRow, IEnumerable<InvertedLicenceSectionVerification> invertedVerifications);
}
