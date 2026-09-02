using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IDmsLookupService
{
    public Task<DmsFileData?> GetDmsFileDataAsync(
        string? licenceNumber,
        ICacheService cacheService);
}