using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Interfaces;

public interface IAbstractionLicenceOutputService
{
    public string? OutputFolder { get; set; }

    public Task SaveLicenceSetsAsync(Dictionary<string, LicenceSet> licenceSets, Guid? fileId, int processRunId);

    public Task SaveLicenceSetAsync(LicenceSet licenceSet, Guid? fileId, int processRunId);

    public Task<int> SaveLicenceAsync(Licence licence, int processRunId);

    public Task UpdateLicenceAsync(Licence licence, int licenceId, int processRunId);
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId);
    
    Task<List<Licence>> GetLicencesAsync(int processRunId, int skip, int take);

    Task<List<Licence>> GetLicencesSearchAsync(int processRunId, ProcessRunQuery processRunQuery);

    Task<Dictionary<string, LicenceSet>> GetProcessRunLicenceSetsAsync(int processRunId);
    
    Task<Dictionary<string, LicenceSet>> GetLicenceSetsAsync(int processRunId, List<Licence> licences);
    
    Task<List<LicenceSet>> GetLicenceSetsAsync(Guid fileId);

    Task<Licence?> GetLicenceAsync(Guid fileId, int processRunId, bool applyVerifications = false);

    Task<Licence?> GetLicenceAsync(string licenceNumber, int processRunId, bool applyVerifications = false);
    
    Task<LinkedLicence[]?> GetLinkedLicencesAsync(string permitNumber);

    Task<IEnumerable<LicenceSectionVerification>> GetLicenceSectionVerificationsAsync(Guid licenceFileId);

    Task<IEnumerable<LicenceSectionVerification>> GetAllVerificationsAsync(int maxProcessRunId);

    Task<Dictionary<string, LicenceVerificationLookups>> GetVerificationLookupsBySectionNameAsync(int maxProcessRunId);

    Task<int> SaveLicenceSectionVerificationAsync(LicenceSectionVerification verification);

    Task<int> DeleteLicenceSectionVerificationAsync(int licenceSectionVerificationId);

    Task<int> GetTotalLicenceCountAsync(int processRunId, ProcessRunQuery processRunQuery);

    Task<List<string>> GetDistinctIssuersAsync(int processRunId);

    Task<List<string>> GetDistinctIssueDatesAsync(int processRunId);

    Task<Dictionary<Guid, string>> GetLicenceFileIdsAsync(int processRunId);
    
    Task FinishProcessRunAsync(ProcessRun processRun);

    Task UpdateProcessRunByLicenceNumbersAsync(
        int processRunId,
        string[] licenceNumbers);

    Task UpdateLicenceListProcessRunAsync(
        int processRunId);
    
    Task<List<DocumentNaldPurposeMap>> GetDocumentNaldPurposeMapAsync();
}