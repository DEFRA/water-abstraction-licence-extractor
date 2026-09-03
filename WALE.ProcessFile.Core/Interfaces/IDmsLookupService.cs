using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IDmsLookupService
{
    public Task<DmsFileData?> GetDmsFileDataAsync(
        string? licenceNumber,
        ICacheService cacheService);
}