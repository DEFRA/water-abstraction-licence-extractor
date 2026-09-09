using System.Collections.Concurrent;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;

namespace WALE.ProcessFile.Services.Services;

public class DmsLookupService : IDmsLookupService
{
    private readonly ConcurrentDictionary<string, DmsFileData?> _dmsFileDataCache = new();
    
    public async Task<DmsFileData?> GetDmsFileDataAsync(
        string? licenceNumber,
        ICacheService cacheService)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return null;
        }
        
        if (_dmsFileDataCache.TryGetValue(licenceNumber, out var cachedData))
        {
            return cachedData;
        }
        
        var dmsFileData = await cacheService.GetDmsFileDataAsync(licenceNumber);
        _dmsFileDataCache.TryAdd(licenceNumber, dmsFileData);
        
        return dmsFileData;
    }
}