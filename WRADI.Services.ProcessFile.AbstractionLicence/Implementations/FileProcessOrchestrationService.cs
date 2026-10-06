using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WRADI.Services.ProcessFile.AbstractionLicence.Implementations;

public class FileProcessOrchestrationService(
    FileProcessAppSettings settings,
    ICacheService cacheService,
    IAbstractionLicenceCacheService abstractionLicenceCacheService,
    IOutputService outputService,
    IFileService fileService,
    IMessageQueueService messageQueueService)
    : IFileProcessOrchestrator
{
    public async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - Started");

        if (settings.RefreshCache)
        {
            await cacheService.ClearCacheAsync();
        }
        
        await cacheService.SetupAsync();
        await outputService.SetupAsync();

        var (dmsFilesToProcess, _) =
            await DmsHelper.GetDmsAndNaldFilesAndMappingAsync(
                fileService,
                string.Empty,
                false,
                abstractionLicenceCacheService);

        if (dmsFilesToProcess.Count == 0)
        {
            ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - No DMS files to process");
            return true;
        }
        
        // SQS's own batch limit, mirrored here so the orchestrator sends whole batches.
        const int QueueBatchSize = 10;

        var processRun = await outputService.StartProcessRunAsync(
            new ProcessRun
            {
                Description = $"Batch process request for single file process from: {fileService.IngressFolderPath}",
                StartDateTimeUtc = DateTime.UtcNow,
                NumberOfFiles = dmsFilesToProcess.Count,
                Status = "Batch",
                DocumentType = "AbstractionLicence"
            });

        var requests = dmsFilesToProcess
            .Select(entry => new FileProcessSingleRequest
            {
                FilePath = entry.Key,
                DestinationFileName = entry.Value.DmsFileData.DestinationFileName,
                DmsPath = entry.Value.DmsFileData.DmsPath,
                FileId = entry.Value.DmsFileData.FileId,
                PermitNumber = entry.Value.DmsFileData.PermitNumber,
                RegionId = entry.Value.NaldLicence.RegionCode,
                ProcessRunId = processRun.ProcessRunId,
                RequestedAt = DateTime.UtcNow,
                DocumentType = "AbstractionLicence"
            })
            .ToList();

        var enqueued = 0;
        var failed = 0;

        // Chunked, and the failure handling is per chunk rather than around the whole loop: one
        // rejected send previously threw out of the entire foreach, leaving every remaining file
        // silently unqueued while the process run still claimed the full count.
        foreach (var batch in requests.Chunk(QueueBatchSize))
        {
            try
            {
                var result = await messageQueueService.AddToFileProcessQueueBatch(batch);

                enqueued += result.Enqueued;
                failed += result.FailedFileIds.Count;

                foreach (var failedFileId in result.FailedFileIds)
                {
                    ConsoleHelper.WriteLine($"ERROR - {nameof(FileProcessOrchestrationService)} - SQS rejected "
                        + $"file {failedFileId}, not queued");
                }
            }
            catch (Exception exception)
            {
                failed += batch.Length;

                ConsoleHelper.WriteLine($"ERROR - {nameof(FileProcessOrchestrationService)} - Error sending a batch of "
                    + $"{batch.Length} to the single file processing queue, continuing with the rest: {exception}");
            }
        }

        ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - {enqueued} of {requests.Count} "
            + $"files sent to the single process file queue, {failed} failed");

        // NumberOfFiles is written before sending, because every message needs the run id, so a send
        // failure leaves the row overstating the run. Logged rather than corrected: there is no write
        // path for it on IOutputService today, and adding one is its own change.
        if (failed > 0)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessOrchestrationService)} - process run "
                + $"{processRun.ProcessRunId} records {requests.Count} files but only {enqueued} were queued");
        }

        ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - Finished processing " +
            $"at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        
        return true;
    }
}