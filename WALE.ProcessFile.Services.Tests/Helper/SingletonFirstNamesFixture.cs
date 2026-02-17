using System.Collections.Concurrent;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Tests.Helper;

public class SingletonFirstNamesFixture : IDisposable
{
    private readonly Lock _firstNamesLock = new();
    private Task<HashSet<string>>? _firstNamesCsvTask;
    
    public Task<HashSet<string>> FirstNamesCsvTask()
    {
        lock (_firstNamesLock)
        {
            return _firstNamesCsvTask ??= CompanyName.GetFirstNamesCsvFromFileAsync();
        }
    }

    private readonly ConcurrentDictionary<short, List<NaldLicence>> _licencesAlternateFormatValues = [];
    
    public async Task SetupLicenceNumbersAsync(short regionCode, ICacheService cacheService)
    {
        if (!_licencesAlternateFormatValues.TryGetValue(regionCode, out var licences))
        {
            var allNaldData = await cacheService.GetNaldDataAsync(regionCode);
            licences = allNaldData.LicencesAlternateFormat!;
            
            _licencesAlternateFormatValues.TryAdd(regionCode, licences);
        }
        
        LicenceNumber.Instance = new LicenceNumber(licences);
    }
    
    public void Dispose()
    {
        // Cleanup resources
    }
}