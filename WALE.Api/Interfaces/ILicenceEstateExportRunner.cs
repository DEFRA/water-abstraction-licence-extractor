namespace WALE.Api.Interfaces;

public interface ILicenceEstateExportRunner
{
    void StartRunExport(int processRunId);

    Task<string?> GetRunExportUrlAsync(int processRunId);
}
