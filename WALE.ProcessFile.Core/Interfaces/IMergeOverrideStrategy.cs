using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IMergeOverrideStrategy
{
    string SectionName { get; }
    void Merge(LicenceSectionVerification verification, OutputListDataItem listRow);
}
