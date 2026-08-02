using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Interfaces;

public interface IAbstractionLicenceCacheService
{
    public string? CacheFolderOrUrl { get; set; }
    
    Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync();

    Task<NaldDataCollection> GetNaldDataAsync(
        short? regionCode,
        bool allVersions,
        int skip,
        int take);
    
    Task<NaldLicenceStatusData> GetNaldLicenceStatusDataAsync(short? regionCode = null);
    Task<(
            HashSet<(string, int)> Live,
            HashSet<(string, int)> Lapsed,
            HashSet<(string, int)> Expired,
            HashSet<(string, int)> Revoked,
            HashSet<(string, int)> Impoundment)>
        GetNaldLicenceNumbersAsync(short? regionCode);
    
    Task<int> GetNaldLicenceIncrementNumberAsync(string permitNumber, int issueNumber);
    
    Task<List<NaldLicence>> GetNaldImpoundmentAndAbstractionLicencesAsync();
    
    Task<NaldData?> GetNaldLicenceAsync(string licenceNumber, int regionCode);
    
    Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync(int skip, int take);
    
    Task SaveLicenceFinderResultsAsync(List<LicenceFinderResult> results);

    Task ClearLicenceFinderResultsAsync();
    
    Task<List<VersionFileToDownload>> GetVersionFilesToDownloadAsync();

    Task SaveVersionFilesToDownloadAsync(List<VersionFileToDownload> results);
    
    Task<List<VersionFile>> GetVersionFilesAsync();
    
    Task SaveVersionFilesAsync(List<VersionFile> results);
    
    Task ClearVersionFilesAsync();

    Task ClearVersionFilesToDownloadAsync();
    
    Task<LicenceFinderResult> GetLicenceFinderResultAsync(Guid fileId);
}