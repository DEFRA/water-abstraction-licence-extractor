using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay;
using WRADI.Core.AbstractionLicence.Models.Table;

namespace WRADI.Core.AbstractionLicence.Interfaces;

public interface IAbstractionLicenceDatabaseReadService
{
    Task<List<Licence>> GetLicencesSearchAsync(int processRunId, ProcessRunQuery query);
    
    Task<List<Licence>> GetLicencesAsync(int processRunId, int skip, int take);
    
    Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(int processRunId);

    Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(Guid fileId, int processRunId);
    
    Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int processRunId);
    
    Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int licenceSetId, int processRunId);
    
    Task<LicenceSetType[]> GetLicenceSetTypes(int licenceSetId);
    
    Task<List<(int LicenceSetId, LicenceSetType Type)>> GetLicenceSetTypesForProcessRun(int processRunId);
    
    Task<AggregateSet[]?> GetAggregateSets(int licenceSetId);
    
    Task<List<(int LicenceSetId, AggregateSet AggregateSet)>> GetAggregateSetsForProcessRun(int processRunId);
    
    Task<Licence?> GetLicenceAsync(Guid fileId, int processRunId);
    
    Task<Licence?> GetLicenceAsync(string licenceNumber, int processRunId);

    Task<Licence?> GetNewestLicenceAsync(string permitNumber);
    
    Task<IEnumerable<LicenceSectionVerification>> GetLicenceSectionVerificationsAsync(Guid licenceFileId);

    Task<IEnumerable<LicenceSectionVerification>> GetAllVerificationsAsync(int maxProcessRunId);
    
    Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync();

    Task<List<NaldLicence>> GetNaldImpoundmentAndAbstractionLicencesAsync(int skip, int take);

    Task<(
        HashSet<(string, int)> Live,
        HashSet<(string, int)> Lapsed,
        HashSet<(string, int)> Expired,
        HashSet<(string, int)> Revoked,
        HashSet<(string, int)> Impoundment)> GetNaldLicenceNumbersAsync(short? regionCode);

    Task<List<NaldAbstractionLicenceDataLine>> GetNaldAbsLicencesAsync(short? regionCode, int skip, int take);

    Task<List<NaldLicenceVersionDataLine>> GetNaldLicenceVersionsAsync(short? regionCode, bool allVersions, int skip, int take);

    Task<List<NaldLicencePurposeDataLine>> GetNaldLicencePurposesAsync(short? regionCode, int skip, int take);

    Task<List<NaldLicencePointDataLine>> GetNaldLicencePointsAsync(short? regionCode, int skip, int take);

    Task<List<NaldLicenceQuantitiesDataLine>> GetNaldLicenceQuantitiesAsync(short? regionCode, int skip, int take);
    
    Task<int> GetNaldLicenceIncrementNumberAsync(string permitNumber, int issueNumber);
    
    Task<NaldData?> GetNaldLicenceAsync(string licenceNumber);
    
    Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync(int skip, int take);
    
    Task<List<VersionFileToDownload>> GetVersionFilesToDownloadAsync();
    
    Task<List<VersionFile>> GetVersionFilesAsync();
    
    Task<LicenceFinderResult> GetLicenceFinderResultAsync(Guid fileId);
    
    Task<int> GetTotalLicenceCountAsync(int processRunId,  ProcessRunQuery processRunQuery);
    
    Task<List<string>> GetDistinctIssuersAsync(int processRunId);
    
    Task<List<string>> GetDistinctIssueDatesAsync(int processRunId);

    Task<Dictionary<Guid, string>> GetLicenceFileIdsAsync(int processRunId);

    Task<List<LicenceListItemAggregate>> GetLicencesListSearchAsync(
        int processRunId,
        ProcessRunQuery query,
        CancellationToken cancellationToken = default);
    
    Task<List<string>> GetLicenceListDistinctIssuersAsync(int processRunId);
    
    Task<List<string>> GetLicenceListLicenceSetIdsAsync(int processRunId);
    
    Task<List<string>> GetLicenceListIssueYearsAsync(int processRunId);

    Task<int> GetLicencesListSearchCountAsync(
        int processRunId,
        ProcessRunQuery query);
}