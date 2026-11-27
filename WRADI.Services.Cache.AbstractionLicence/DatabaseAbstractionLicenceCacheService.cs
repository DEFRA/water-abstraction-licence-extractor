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

    public async Task<NaldImpoundmentDataCollection> GetImpoundmentNaldDataAsync(
        short? regionCode,
        bool allVersions,
        int skip,
        int take)
    {
        var impoundmentLicencesTask = databaseReadService.GetNaldImpoundmentLicencesAsync(skip, take);
        var impoundmentLicencesVersionsTask = databaseReadService.GetNaldImpoundmentLicenceVersionsAsync(
            regionCode,
            allVersions,
            skip,
            take);
        
        return new NaldImpoundmentDataCollection
        {
            ImpoundmentLicences = await impoundmentLicencesTask,
            ImpoundmentLicenceVersions = await impoundmentLicencesVersionsTask,
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

    public  Task<(
            HashSet<(string, int)> Live,
            HashSet<(string, int)> Terminated,
            HashSet<(string, int)> Revoked)>
        GetNaldImpoundmentLicenceNumbersAsync(short? regionCode)
    {
        return databaseReadService.GetNaldImpoundmentLicenceNumbersAsync(regionCode);
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

    public Task SaveLicenceFinderResultsAsync(List<LicenceFinderResult> results, string licenceType)
    {
        return databaseWriteService.SaveLicenceFinderResultsAsync(results, licenceType);
    }
    
    public Task ClearLicenceFinderResultsAsync(string licenceType)
    {
        return databaseWriteService.ClearLicenceFinderResultsAsync(licenceType);
    }

    public Task<List<VersionFileToDownload>> GetVersionFilesToDownloadAsync(string licenceType)
    {
        return databaseReadService.GetVersionFilesToDownloadAsync(licenceType);
    }

    public Task SaveVersionFilesToDownloadAsync(List<VersionFileToDownload> results, string licenceType)
    {
        return databaseWriteService.SaveVersionFilesToDownloadAsync(results, licenceType);
    }

    public Task<List<VersionFile>> GetVersionFilesAsync(string licenceType)
    {
        return databaseReadService.GetVersionFilesAsync(licenceType);
    }

    public Task SaveVersionFilesAsync(List<VersionFile> results, string licenceType)
    {
        return databaseWriteService.SaveVersionFilesAsync(results, licenceType);
    }

    public Task ClearVersionFilesAsync(string licenceType)
    {
        return databaseWriteService.ClearVersionFilesAsync(licenceType);
    }

    public Task ClearVersionFilesToDownloadAsync(string licenceType)
    {
        return databaseWriteService.ClearVersionFilesToDownloadAsync(licenceType);
    }
    
    public Task<List<NaldLicence>> GetNaldImpoundmentAndAbstractionLicencesAsync()
    {
        return databaseReadService.GetNaldImpoundmentAndAbstractionLicencesAsync(0, int.MaxValue);
    }

    public Task<NaldAbstractionData?> GetNaldAbstractionLicenceAsync(
        string licenceNumber,
        int regionCode,
        bool slashesRemoved = false,
        bool includeDetail = true)
    {
        return databaseReadService.GetNaldAbstractionLicenceAsync(licenceNumber, slashesRemoved, includeDetail);
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
            var licenceNumberInDict = dict.TryGetValue(licenceNumber, out var followOnKvp);
            
            if (licenceNumberInDict)
            {
                followOnKvp!.FollowOnLicenceNumbers.Add(item.FollowOnLicenceNumber!);
            }
            else
            {
                dict.Add(licenceNumber, new NaldLicenceNumberHistory
                {
                    LicenceNumber = item.LicenceNumber,
                    FollowOnLicenceNumbers = [item.FollowOnLicenceNumber!],
                    Source = item.Source
                });
            }
            
            var followOnLicenceNumber = item.FollowOnLicenceNumber!.ToLower();
            var followOnLicenceNumberInDict = dict.TryGetValue(followOnLicenceNumber, out var predecessorKvp);
            
            if (followOnLicenceNumberInDict)
            {
                predecessorKvp!.PrecedingLicenceNumbers.Add(licenceNumber);
            }
            else
            {
                dict.Add(followOnLicenceNumber, new NaldLicenceNumberHistory
                {
                    LicenceNumber = item.FollowOnLicenceNumber,
                    PrecedingLicenceNumbers = [item.LicenceNumber],
                    Source = item.Source
                });
            }
        }

        return dict;
    }
}