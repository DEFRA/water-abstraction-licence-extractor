using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;
using WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay.DTOs;

namespace WALE.ProcessFile.Core.Interfaces;

public interface ILicenceListRepository
{
    Task<long> UpsertLicenceListItemAsync(
        UpsertLicenceListItem item,
        CancellationToken cancellationToken = default);

    Task UpsertLicenceListItemManyAsync(
        IReadOnlyCollection<UpsertLicenceListItem> items,
        CancellationToken cancellationToken = default);

    Task<List<LicenceListItemAggregate>> GetLicencesListSearchAsync(
        int processRunId,
        ProcessRunQuery query);

    Task<List<string>> GetLicenceListIssuersAsync(int processRunId);
    
    Task<List<string>> GetLicenceListLicenceSetIdsAsync(int processRunId);
    
    Task<List<string>> GetLicenceListIssueYearsAsync(int processRunId);
}  