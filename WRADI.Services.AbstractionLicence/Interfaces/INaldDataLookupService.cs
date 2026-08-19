using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Interfaces;

public interface INaldDataLookupService
{
    public Task<NaldData?> GetNaldDataLineAsync(
        string? licenceNumber,
        int regionCode);
}