using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Interfaces;

public interface ILicenceRunComparisonService
{
    LicenceRunComparison Compare(
        string licenceNumber,
        int newerRunId,
        string? newerData,
        int olderRunId,
        string? olderData);
}
