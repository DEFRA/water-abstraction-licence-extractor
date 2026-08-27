using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Interfaces;

public interface INaldDataLookupService
{
    public Task<NaldAbstractionData?> GetNaldAbstractionDataLineAsync(
        string? licenceNumber,
        int regionCode);

    public Task<NaldImpoundmentData?> GetNaldImpoundmentDataLineAsync(
        string? licenceNumber,
        int regionCode);
}