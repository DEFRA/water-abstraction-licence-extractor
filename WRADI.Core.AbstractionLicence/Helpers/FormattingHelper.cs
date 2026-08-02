using System.Collections.Concurrent;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Helpers;

public static class FormattingHelper
{
    private static readonly ConcurrentDictionary<string, NaldData?> NaldDataCache = new();

    public static async Task<NaldData?> GetNaldDataLineAsync(
        IAbstractionLicenceCacheService cacheService,
        string? licenceNumber,
        int regionCode)
    {
        var key = $"{regionCode}|{licenceNumber}";

        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (NaldDataCache.TryGetValue(key, out var cachedData))
        {
            return cachedData;
        }

        var naldData = await cacheService.GetNaldLicenceAsync(licenceNumber!, regionCode);
        NaldDataCache.TryAdd(key, naldData);

        return naldData;
    }
}