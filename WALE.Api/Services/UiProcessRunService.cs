using System.Diagnostics;
using WALE.Api.Interfaces;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;
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
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var processRunRawDataList = await GetProcessRunRawDataList(processRunId, query);
        ConsoleHelper.WriteLine($"UpdateLicenceListProcessRunAsync - started - processRunId-{processRunId} for {processRunRawDataList.Count} licences at {DateTime.UtcNow}");

        await UpdateLicenceListRepo(processRunRawDataList);
        
        stopwatch.Stop();
        ConsoleHelper.WriteLine($"UpdateLicenceListProcessRunAsync - completed - in {stopwatch.Elapsed.Seconds} seconds - processRunId-{processRunId} for {processRunRawDataList.Count} licences at {DateTime.UtcNow}");

        return $"Updated Process Run: {processRunId} for {processRunRawDataList.Count} licences";
    }

    public async Task<string> UpdateProcessRunByLicenceNumbersAsync(int processRunId, string[] licenceNumbers)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        ConsoleHelper.WriteLine($"UpdateProcessRunByLicenceNumbersAsync - started - processRunId-{processRunId} for {licenceNumbers.Length} licences at {DateTime.UtcNow}");
       
        var query = new ProcessRunQuery
        {
            Skip = 0,
            Take = int.MaxValue,
            LicenceNumbers = licenceNumbers
        };

        var processRunRawDataList = await GetProcessRunRawDataList(processRunId, query);
        await UpdateLicenceListRepo(processRunRawDataList);
        
        stopwatch.Stop();
        ConsoleHelper.WriteLine($"UpdateProcessRunByLicenceNumbersAsync - time taken - {stopwatch.Elapsed.Seconds} seconds - Completed - processRunId-{processRunId} for {processRunRawDataList.Count} DB licences found to be updated for trigger licences : {string.Join(",", licenceNumbers)} at {DateTime.UtcNow}");

        return $"Updated Process Run: {processRunId} for {processRunRawDataList.Count} licences";
    }
    
    public async Task<IReadOnlyList<OutputListDataItem>> GetProcessRunRawDataList(int processRunId, ProcessRunQuery query)
    {
        var completeNumber = 1;
        var fileNumber = 1;
        
        var verificationsBySectionTask =
            abstractionLicenceOutputService.GetVerificationLookupsBySectionNameAsync(processRunId);
        var fileIdTask = abstractionLicenceOutputService.GetLicenceFileIdsAsync(processRunId);
        var licenceNumberFlagReasonsTask =
            abstractionLicenceOutputService.GetLicenceNumberFlagReasonsAsync(processRunId);
        
        var licences = await abstractionLicenceOutputService.GetLicencesSearchAsync(processRunId, query);
        var licenceSets =
            await abstractionLicenceOutputService.GetLicenceSetsAsync(processRunId, licences); 
        
        var verificationsBySection = await verificationsBySectionTask;
        var fileIdToLicenceNumberMapping = await fileIdTask;
        var licenceNumberFlagReasons = await licenceNumberFlagReasonsTask;
        
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

        foreach (var listDataItem in paginationListData)
        {
            if (licenceNumberFlagReasons.TryGetValue(listDataItem.licenceId, out var flagReason))
            {
                listDataItem.isLicenceNumberFlagged = true;
                listDataItem.licenceNumberFlagReason = flagReason;
            }
        }

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