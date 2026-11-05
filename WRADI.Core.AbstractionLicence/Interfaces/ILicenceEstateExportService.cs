using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Interfaces;

public interface ILicenceEstateExportService
{
    Task<LicenceExport?> ExportLicenceAsync(int licenceId);

    Task<LicenceExport?> ExportLicenceAsync(string licenceNumber, int processRunId);

    /// Writes a zip of one JSON file per licence. Destination stays the caller's to dispose.
    Task<LicenceEstateExportSummary> ExportRunAsync(int processRunId, Stream destination);
}
