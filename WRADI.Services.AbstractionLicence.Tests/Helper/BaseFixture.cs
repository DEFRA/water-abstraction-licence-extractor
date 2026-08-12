using System.Collections.Concurrent;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Formats;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public class BaseFixture
{
    private readonly ConcurrentDictionary<short, List<NaldLicence>> _licencesAlternateFormatValues = [];
    private static readonly SemaphoreSlim SetupLicenceNumbersLock = new(1, 1);
    private static ILicenceNumberService? _licenceNumberService;
    
    public async Task<ILicenceNumberService> GetLicenceNumbersServiceAsync(
        short regionCode,
        IAbstractionLicenceCacheService cacheService)
    {
        if (_licenceNumberService != null)
        {
            return _licenceNumberService;
        }

        await SetupLicenceNumbersLock.WaitAsync();
        
        try
        {
            if (_licenceNumberService != null)
            {
                return _licenceNumberService;
            }
            
            if (!_licencesAlternateFormatValues.TryGetValue(regionCode, out var licences))
            {
                var allNaldData = await cacheService.GetNaldDataAsync(regionCode, false, 0, int.MaxValue);
                licences = allNaldData.AbstractionAndImpoundmentLicences!;

                _licencesAlternateFormatValues.TryAdd(regionCode, licences);
            }

            _licenceNumberService = new AbstractionLicenceNumber(licences);
            return _licenceNumberService;
            
        }
        finally
        {
            SetupLicenceNumbersLock.Release();
        }
    }
}