using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Nald;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.Cache.AbstractionLicence;

public class DatabaseAbstractionLicenceCacheService(
    IAbstractionLicenceDatabaseReadService databaseReadService,
    IAbstractionLicenceDatabaseWriteService databaseWriteService) : IAbstractionLicenceCacheService
{
    public string? CacheFolderOrUrl { get; set; } = null;
    
    public Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync()
    {
        return databaseReadService.GetNaldLinkedLicenceRawDataAsync();
    }

    public async Task<NaldDataCollection> GetNaldDataAsync(
        short? regionCode,
        bool allVersions,
        int skip,
        int take)
    {
        var licencesTask = databaseReadService.GetNaldAbsLicencesAsync(regionCode, skip, take);
        var versionsTask = databaseReadService.GetNaldLicenceVersionsAsync(regionCode, allVersions, skip, take);
        var purposesTask = databaseReadService.GetNaldLicencePurposesAsync(regionCode, skip, take);
        var pointsTask = databaseReadService.GetNaldLicencePointsAsync(regionCode, skip, take);
        var quantitiesTask = databaseReadService.GetNaldLicenceQuantitiesAsync(regionCode, skip, take);
        var allLicencesTask = databaseReadService.GetNaldImpoundmentAndAbstractionLicencesAsync(skip, take);
        
        return new NaldDataCollection
        {
            AbstractionLicences = await licencesTask,
            AbstractionAndImpoundmentLicences = await allLicencesTask,
            AbstractionLicenceVersions = await versionsTask,
            AbstractionLicencePurposes = await purposesTask,
            AbstractionLicencePoints = await pointsTask,
            AbstractionLicenceQuantities = await quantitiesTask
        };
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
        HashSet<(string, int)> Impoundment)>
        GetNaldLicenceNumbersAsync(short? regionCode)
    {
        return databaseReadService.GetNaldLicenceNumbersAsync(regionCode);
    }
    
    public Task<int> GetNaldLicenceIncrementNumberAsync(string permitNumber, int issueNumber)
    {
        return databaseReadService.GetNaldLicenceIncrementNumberAsync(permitNumber, issueNumber);
    }

    public Task<NaldImpoundmentData?> GetNaldImpoundmentLicenceAsync(string licenceNumber, int regionCode)
    {
        return databaseReadService.GetNaldImpoundmentLicenceAsync(licenceNumber);
    }

    public Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync(int skip, int take)
    {
        return databaseReadService.GetLicenceFinderResultsAsync(skip, take);
    }

    public Task SaveLicenceFinderResultsAsync(List<LicenceFinderResult> results)
    {
        return databaseWriteService.SaveLicenceFinderResultsAsync(results);
    }

    public Task ClearLicenceFinderResultsAsync()
    {
        return databaseWriteService.ClearLicenceFinderResultsAsync();
    }

    public Task<List<VersionFileToDownload>> GetVersionFilesToDownloadAsync()
    {
        return databaseReadService.GetVersionFilesToDownloadAsync();
    }

    public Task SaveVersionFilesToDownloadAsync(List<VersionFileToDownload> results)
    {
        return databaseWriteService.SaveVersionFilesToDownloadAsync(results);
    }

    public Task<List<VersionFile>> GetVersionFilesAsync()
    {
        return databaseReadService.GetVersionFilesAsync();
    }

    public Task SaveVersionFilesAsync(List<VersionFile> results)
    {
        return databaseWriteService.SaveVersionFilesAsync(results);
    }

    public Task ClearVersionFilesAsync()
    {
        return databaseWriteService.ClearVersionFilesAsync();
    }

    public Task ClearVersionFilesToDownloadAsync()
    {
        return databaseWriteService.ClearVersionFilesToDownloadAsync();
    }
    
    public Task<List<NaldLicence>> GetNaldImpoundmentAndAbstractionLicencesAsync()
    {
        return databaseReadService.GetNaldImpoundmentAndAbstractionLicencesAsync(0, int.MaxValue);
    }

    public Task<NaldAbstractionData?> GetNaldAbstractionLicenceAsync(string licenceNumber, int regionCode)
    {
        return databaseReadService.GetNaldAbstractionLicenceAsync(licenceNumber);
    }

    public Task<LicenceFinderResult> GetLicenceFinderResultAsync(Guid fileId)
    {
        return databaseReadService.GetLicenceFinderResultAsync(fileId);
    }

    public async Task<Dictionary<string, NaldLicenceNumberHistory>> GetNaldLicenceNumberHistoryAsync()
    {
        var list = await databaseReadService.GetNaldLicenceNumberHistoryAsync();
        var dict = new Dictionary<string, NaldLicenceNumberHistory>();

        foreach (var item in list)
        {
            var licenceNumber = item.LicenceNumber!.ToLower();
            
            if (dict.ContainsKey(licenceNumber))
            {
                dict[licenceNumber].FollowOnLicenceNumbers.Add(item.FollowOnLicenceNumber!);
                continue;
            }
            
            dict.Add(licenceNumber, new NaldLicenceNumberHistory
            {
                LicenceNumber = item.LicenceNumber,
                FollowOnLicenceNumbers = [item.FollowOnLicenceNumber!],
                Source = item.Source
            });
        }

        return dict;
    }
}