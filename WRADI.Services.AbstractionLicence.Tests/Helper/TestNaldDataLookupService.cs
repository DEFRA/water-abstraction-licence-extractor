using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Interfaces;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public class TestNaldDataLookupService : INaldDataLookupService
{
    public Task<NaldAbstractionData?> GetNaldAbstractionDataLineAsync(
        string? licenceNumber,
        int regionCode,
        bool slashesRemoved = false)
    {
        return Task.FromResult((NaldAbstractionData?)null);
    }

    public Task<NaldImpoundmentData?> GetNaldImpoundmentDataLineAsync(
        string? licenceNumber,
        int regionCode)
    {
        throw new NotImplementedException();
    }

    public Task<(NaldPurposeData[] Purposes, string? MatchType)> GetRelevantNaldPurposesAsync(
        List<NaldPurposeData> naldPurposes,
        string? documentDescription,
        List<string> excludeNaldPurposeIds,
        string licenceNumber,
        bool saveMatches)
    {
        throw new NotImplementedException();
    }
}