using System.Collections.Concurrent;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Tests.Helper;

public class BaseFixture : IDisposable
{
    private readonly ConcurrentDictionary<short, List<NaldLicence>> _licencesAlternateFormatValues = [];
    private static readonly SemaphoreSlim SetupLicenceNumbersLock = new(1, 1);
    
    public async Task SetupLicenceNumbersAsync(short regionCode, ICacheService cacheService)
    {
        await SetupLicenceNumbersLock.WaitAsync();

        try
        {
            if (!_licencesAlternateFormatValues.TryGetValue(regionCode, out var licences))
            {
                var allNaldData = await cacheService.GetNaldDataAsync(regionCode, false, 0, int.MaxValue);
                licences = allNaldData.AbstractionAndImpoundmentLicences!;
            
                _licencesAlternateFormatValues.TryAdd(regionCode, licences);
            }
        
            LicenceNumber.Instance = new LicenceNumber(licences);
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