using System.Collections.Concurrent;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Formats;

namespace WALE.ProcessFile.Services.Tests.Helper;

public class BaseFixture : IDisposable
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

            _licenceNumberService = new AbstractionLicenceNumber(licences, []);
            return _licenceNumberService;
            
        }
        finally
        {
            SetupLicenceNumbersLock.Release();
        }
    }
    
    private static readonly SemaphoreSlim FirstNamesLock = new(1, 1);
    
    public async Task<HashSet<string>> FirstNamesCsvTask()
    {
        await FirstNamesLock.WaitAsync();
        
        try
        {
            return await CompanyNameHelper.GetFirstNamesCsvFromFileAsync();
        }
        finally
        {
            FirstNamesLock.Release();
        }
    }
    
    public void Dispose()
    {
        // Cleanup resources
    }
}