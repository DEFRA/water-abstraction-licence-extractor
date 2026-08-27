using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.Cache.AbstractionLicence;

public class FileSystemAbstractionLicenceCacheService(string cacheFolder) : IAbstractionLicenceCacheService
{
    public string? CacheFolderOrUrl { get; set; } = cacheFolder.StartsWith('/') ? cacheFolder : Path.GetFullPath(cacheFolder);
    
    public Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync()
    {
        throw new NotImplementedException();
    }

    public Task<NaldDataCollection> GetNaldDataAsync(
        short? regionCode,
        bool allVersions,
        int skip,
        int take)
    {
        throw new NotImplementedException();
    }

    public Task<NaldLicenceStatusData> GetNaldLicenceStatusDataAsync(short? regionCode = null)
    {
        throw new NotImplementedException();
    }

    public Task<(
        HashSet<(string, int)> Live,
        HashSet<(string, int)> Lapsed,
        HashSet<(string, int)> Expired,
        HashSet<(string, int)> Revoked,
        HashSet<(string, int)> Impoundment)> GetNaldLicenceNumbersAsync(short? regionCode)
    {
        throw new NotImplementedException();
    }
    
    public Task<int> GetNaldLicenceIncrementNumberAsync(string permitNumber, int issueNumber)
    {
        throw new NotImplementedException();
    }

    public Task<NaldImpoundmentData?> GetNaldImpoundmentLicenceAsync(string licenceNumber, int regionCode)
    {
        throw new NotImplementedException();
    }

    public Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync(int skip, int take)
    {
        throw new NotImplementedException();
    }

    public Task SaveLicenceFinderResultsAsync(List<LicenceFinderResult> results)
    {
        throw new NotImplementedException();
    }

    public Task ClearLicenceFinderResultsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<VersionFileToDownload>> GetVersionFilesToDownloadAsync()
    {
        throw new NotImplementedException();
    }

    public Task SaveVersionFilesToDownloadAsync(List<VersionFileToDownload> results)
    {
        throw new NotImplementedException();
    }

    public Task<List<VersionFile>> GetVersionFilesAsync()
    {
        throw new NotImplementedException();
    }

    public Task SaveVersionFilesAsync(List<VersionFile> results)
    {
        throw new NotImplementedException();
    }

    public Task ClearVersionFilesAsync()
    {
        throw new NotImplementedException();
    }

    public Task ClearVersionFilesToDownloadAsync()
    {
        throw new NotImplementedException();
    }
    
    public Task<List<NaldLicence>> GetNaldImpoundmentAndAbstractionLicencesAsync()
    {
        throw new NotImplementedException();
    }

    public Task<NaldAbstractionData?> GetNaldAbstractionLicenceAsync(string licenceNumber, int regionCode)
    {
        throw new NotImplementedException();
    }

    public Task<LicenceFinderResult> GetLicenceFinderResultAsync(Guid fileId)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<string, NaldLicenceNumberHistory>> GetNaldLicenceNumberHistoryAsync()
    {
        throw new NotImplementedException();
    }
}