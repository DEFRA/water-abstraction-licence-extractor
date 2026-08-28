using WRADI.Core.AbstractionLicence.Models;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay.DTOs;

namespace WRADI.Core.AbstractionLicence.Interfaces;

public interface IAbstractionLicenceDatabaseWriteService
{
    Task UpdateLicenceSetLicenceAsync(LicenceSetLicence licenceSetLicence);
    
    Task InsertLicenceSetLicenceAsync(int licenceSetId, int? licenceId, string? licenceNumber, string licenceVersionId, int processRunId);

    Task SaveLicenceSetTypeAsync(int licenceSetId, int licenceSetType, int processRunId);
    
    Task SaveAggregateSetAsync(int licenceSetId, string? aggregateSetAggregateSetId, string serialize, int processRunId);
    
    Task<int> SaveLicenceSectionVerificationAsync(LicenceSectionVerification verification);

    Task<int> DeleteLicenceSectionVerificationAsync(int licenceSectionVerificationId);

    Task SaveLicenceFinderResultsAsync(List<LicenceFinderResult> results);

    Task ClearLicenceFinderResultsAsync();

    Task ClearVersionFilesAsync();

    Task ClearVersionFilesToDownloadAsync();
    
    Task SaveVersionFilesToDownloadAsync(List<VersionFileToDownload> results);

    Task SaveVersionFilesAsync(List<VersionFile> results);
    
    public Task<int> SaveLicenceSetAsync(string licenceSetId, string shortLicenceSetId, int processRunId);

    public Task UpdateLicenceAsync(int licenceId, string licenceData, Guid fileId, int processRunId, string status);

    public Task<int> SaveLicenceAsync(
        string? licenceNumber,
        string? filename,
        string status,
        string licenceData,
        Guid? fileId,
        string? permitNumber,
        int processRunId);
    
    Task<long> UpsertLicenceListItemAsync(UpsertLicenceListItem item, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<long>> UpsertLicenceListItemManyAsync(IReadOnlyCollection<UpsertLicenceListItem> items,
        CancellationToken cancellationToken = default);
}