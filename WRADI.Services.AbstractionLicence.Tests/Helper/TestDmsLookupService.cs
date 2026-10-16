using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.Dms;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public class TestDmsLookupService : IDmsLookupService
{
    public Task<DmsFileData?> GetDmsFileDataAsync(
        string? licenceNumber,
        ICacheService cacheService)
    { 
        return Task.FromResult(new DmsFileData
        {
            DestinationFileName = licenceNumber,
            DmsPath = licenceNumber,
            FileId = Guid.NewGuid(),
            PermitNumber = licenceNumber!.Replace("/", string.Empty)
        })!;
    }
}