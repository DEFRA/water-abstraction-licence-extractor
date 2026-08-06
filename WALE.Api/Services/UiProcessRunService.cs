using WALE.Api.Interfaces;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WALE.Api.Services;

public class UiProcessRunService( 
    IAbstractionLicenceOutputService abstractionLicenceOutputService,
    ILicenceListItemModelService licenceListItemModelService,
    ILicenceListRepository licenceListRepository) 
    : IUiProcessRunService
{
    public async Task<string> UpdateLicenceListProcessRunAsync(int processRunId)
    {
        var query = new ProcessRunQuery
        {
            Skip = 0,
            Take = int.MaxValue
        };

        var processRunRawDataList = await GetProcessRunRawDataList(processRunId, query);

        await UpdateLicenceListRepo(processRunRawDataList);

        return $"Updated Process Run: {processRunId} for {processRunRawDataList.Count} licences";
    }

    public async Task<string> UpdateProcessRunByLicenceNumbersAsync(int processRunId, string[] licenceNumbers)
    {
        var query = new ProcessRunQuery
        {
            Skip = 0,
            Take = int.MaxValue,
            LicenceNumbers = licenceNumbers
        };

        var processRunRawDataList = await GetProcessRunRawDataList(processRunId, query);

        await UpdateLicenceListRepo(processRunRawDataList);

        return $"Updated Process Run: {processRunId} for {processRunRawDataList.Count} licences";
    }
    
    public async Task<IReadOnlyList<OutputListDataItem>> GetProcessRunRawDataList(int processRunId, ProcessRunQuery query)
    {
        var completeNumber = 1;
        var fileNumber = 1;
        
        var verificationsBySectionTask =
            abstractionLicenceOutputService.GetVerificationLookupsBySectionNameAsync(processRunId);
        var fileIdTask = abstractionLicenceOutputService.GetLicenceFileIdsAsync(processRunId);
        
        var licences = await abstractionLicenceOutputService.GetLicencesSearchAsync(processRunId, query);
        var licenceSets =
            await abstractionLicenceOutputService.GetLicenceSetsAsync(processRunId, licences); 
        
        var verificationsBySection = await verificationsBySectionTask;
        var fileIdToLicenceNumberMapping = await fileIdTask;
        
        var paginationOutputLines = licences
            .Where(licence => licence.Status == ScrapeStatus.Ok)
            .Select(licence => JsOutputHelper.ToOutputLine(
                licence,
                DateTime.Now,
                completeNumber++,
                fileNumber++,
                licenceSets))
            .ToList();
        
        var paginationListData = JsOutputHelper.ToListData(
            paginationOutputLines,
            processRunId,
            verificationsBySection,
            fileIdToLicenceNumberMapping);

        return paginationListData;
    }
    
    private async Task UpdateLicenceListRepo(IReadOnlyList<OutputListDataItem> processRunRawDataList)
    {
        var dbItems = licenceListItemModelService
            .ConvertToUpsertLicenceListItems(processRunRawDataList)
            .ToList();

        foreach (var batch in dbItems.Chunk(50))
        {
            await licenceListRepository.UpsertLicenceListItemManyAsync(
                batch,
                CancellationToken.None);
        }
    }
}