using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IScrapedDataComparisonStrategy
{
    string SectionName { get; }
    bool ScrapedDataIsDifferent(LicenceSectionVerification verification, OutputListDataItem listRow);
}
