using System.Collections.Concurrent;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Interfaces;

namespace WRADI.DocumentType.AbstractionLicence.Services;

public class NaldDataLookupService(IAbstractionLicenceCacheService cacheService) : INaldDataLookupService
{
    private readonly ConcurrentDictionary<string, NaldAbstractionData?> _naldAbstractionDataCache = new();
    private readonly ConcurrentDictionary<string, NaldImpoundmentData?> _naldImpoundmentDataCache = new();
    
    public async Task<NaldAbstractionData?> GetNaldAbstractionDataLineAsync(
        string? licenceNumber,
        int regionCode)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return null;
        }
        
        var key = $"{regionCode}|{licenceNumber}";

        if (_naldAbstractionDataCache.TryGetValue(key, out var cachedData))
        {
            return cachedData;
        }

        var naldData = await cacheService.GetNaldAbstractionLicenceAsync(licenceNumber, regionCode);
        _naldAbstractionDataCache.TryAdd(key, naldData);
        
        return naldData;
    }

    public async Task<NaldImpoundmentData?> GetNaldImpoundmentDataLineAsync(string? licenceNumber, int regionCode)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return null;
        }
        
        var key = $"{regionCode}|{licenceNumber}";

        if (_naldImpoundmentDataCache.TryGetValue(key, out var cachedData))
        {
            return cachedData;
        }

        var naldData = await cacheService.GetNaldImpoundmentLicenceAsync(licenceNumber, regionCode);
        _naldImpoundmentDataCache.TryAdd(key, naldData);
        
        return naldData;
    }
}