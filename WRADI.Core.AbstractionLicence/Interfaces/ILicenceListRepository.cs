using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay.DTOs;

namespace WRADI.Core.AbstractionLicence.Interfaces;

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
    
    Task<int> GetLicencesListSearchCountAsync(
        int processRunId,
        ProcessRunQuery query);

    Task<List<string>> GetLicenceListIssuersAsync(int processRunId);
    
    Task<List<string>> GetLicenceListLicenceSetIdsAsync(int processRunId);
    
    Task<List<string>> GetLicenceListIssueYearsAsync(int processRunId);
}