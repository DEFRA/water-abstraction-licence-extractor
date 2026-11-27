using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IGeneralApiClient
{
    public Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync();

    public Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation);

    public Task SaveLicenceFinderResultsAsync(List<LicenceMatchResult> results);

    public Task ClearLicenceFinderResultsAsync();

    public Task SaveVersionFilesToDownloadAsync(List<DownloadInfoMissing> results, string licenceType);

    public Task SaveVersionFilesAsync(List<DownloadInfoAll> results, string licenceType);

    public Task ClearVersionFilesAsync(string licenceType);

    public Task ClearVersionFilesToDownloadAsync(string licenceType);

    public Task SaveInspectionReportFinderResultsAsync(List<DmsExtract> results);

    public Task ClearInspectionReportFinderResultsAsync();

    public Task<(List<DmsExtract> Data, string ImportDate)> GetDmsExtractAsync(
        int skip,
        int take,
        string extractType);

    public Task<List<LicenceMatchResult>> GetLicenceFinderResultsAsync(int skip, int take);

    public Task<List<DmsFileReaderResult>> GetDmsFileReaderResultsAsync();

    public Task<(List<Override> Data, string ImportDate)> GetOverridesAsync();
}