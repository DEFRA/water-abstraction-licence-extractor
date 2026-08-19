using System.Collections.Concurrent;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Interfaces;

namespace WRADI.DocumentType.AbstractionLicence.Services;

public class NaldDataLookupService(IAbstractionLicenceCacheService cacheService) : INaldDataLookupService
{
    private readonly ConcurrentDictionary<string, NaldData?> _naldDataCache = new();
    
    public async Task<NaldData?> GetNaldDataLineAsync(
        string? licenceNumber,
        int regionCode)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return null;
        }
        
        var key = $"{regionCode}|{licenceNumber}";

        if (_naldDataCache.TryGetValue(key, out var cachedData))
        {
            return cachedData;
        }

        var naldData = await cacheService.GetNaldLicenceAsync(licenceNumber, regionCode);
        _naldDataCache.TryAdd(key, naldData);
        
        return naldData;
    }
}