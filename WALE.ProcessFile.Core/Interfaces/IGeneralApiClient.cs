using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IGeneralApiClient
{
    public Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync();

    public Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation);

    public Task SaveLicenceFinderResultsAsync(List<LicenceMatchResult> results);

    public Task ClearLicenceFinderResultsAsync();

    public Task SaveVersionFilesToDownloadAsync(List<DownloadInfoMissing> results);

    public Task SaveVersionFilesAsync(List<DownloadInfoAll> results);

    public Task ClearVersionFilesAsync();

    public Task ClearVersionFilesToDownloadAsync();

    public Task SaveInspectionReportFinderResultsAsync(List<DmsExtract> results);

    public Task ClearInspectionReportFinderResultsAsync();

    public Task<(List<DmsExtract> Data, string ImportDate)> GetDmsExtractAsync(
        int skip,
        int take,
        string extractType);

    public Task<List<LicenceMatchResult>> GetLicenceFinderResultsAsync(int skip, int take);

    public Task<List<DmsFileReaderResult>> GetDmsFileReaderResultsAsync();
}