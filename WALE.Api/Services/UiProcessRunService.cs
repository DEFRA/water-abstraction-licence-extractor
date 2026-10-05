using WALE.Api.Interfaces;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
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
        
        ConsoleHelper.WriteLine(
            $"DataRefresh - Found {processRunRawDataList.Count} output data items to process");
        
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

        const int maxRetries = 3;

        const int delayAfterBatchProcessing = 5;
        var processedCount = 0;
        const int batchSize = 75;
        var totalListCount = processRunRawDataList.Count;

        foreach (var batch in dbItems.Chunk(batchSize))
        {
            var attempt = 0;

            while (true)
            {
                try
                {
                    await licenceListRepository.UpsertLicenceListItemManyAsync(
                        batch);

                    await Task.Delay(TimeSpan.FromSeconds(delayAfterBatchProcessing));
                    processedCount += batch.Length;

                    ConsoleHelper.WriteLine(
                        $"DataRefresh - Now processed {processedCount} of {totalListCount} data items to process");

                    break;
                }
                catch (Exception exception) when (attempt < maxRetries)
                {
                    ConsoleHelper.WriteLine(
                        $"DataRefresh - ERROR - Exception on update - {exception.Message} on count of  {processedCount} data items processed");

                    attempt++;

                    var delay = TimeSpan.FromSeconds(
                        Math.Pow(2, attempt));

                    await Task.Delay(
                        delay);
                }
                catch (Exception exception)
                {
                       ConsoleHelper.WriteLine(
                        $"DataRefresh - ERROR - Final Exception on update - {exception.Message} on count of  {processedCount} data items processed");
                }
            }
        }
    }
}