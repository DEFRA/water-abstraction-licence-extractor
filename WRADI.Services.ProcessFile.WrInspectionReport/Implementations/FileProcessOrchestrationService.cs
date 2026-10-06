using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.Cache.WrInspectionReport.Interfaces;

namespace WRADI.Services.ProcessFile.WrInspectionReport.Implementations;

public class FileProcessOrchestrationService(
    FileProcessAppSettings settings,
    ICacheService cacheService,
    IInspectionReportFinderCacheService inspectionReportFinderCacheService,
    IOutputService outputService,
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

        var candidates = await inspectionReportFinderCacheService.GetInspectionReportFinderResultsAsync(0, int.MaxValue);

        if (candidates.Count == 0)
        {
            ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - No inspection report files to process");
            return true;
        }

        // SQS's own batch limit, mirrored here so the orchestrator sends whole batches.
        const int QueueBatchSize = 10;

        // Filtered before the run is created, not during the send loop: NumberOfFiles previously
        // counted every candidate including the ones skipped just below, so a run's own total was
        // already wrong before any send failure.
        var sendable = candidates
            .Where(candidate => !string.IsNullOrEmpty(candidate.PermitNumber)
                && !string.IsNullOrEmpty(candidate.FileId)
                && Guid.TryParse(candidate.FileId, out _))
            .ToList();

        if (sendable.Count != candidates.Count)
        {
            ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - "
                + $"{candidates.Count - sendable.Count} of {candidates.Count} candidates skipped for a missing "
                + "permit number or an unparseable file id");
        }

        // Applied after filtering, before the run is created, so NumberOfFiles matches what is sent.
        if (settings.MaxFilesPerRun is > 0 && sendable.Count > settings.MaxFilesPerRun)
        {
            ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - capping this run at "
                + $"{settings.MaxFilesPerRun} of {sendable.Count} files (MaxFilesPerRun)");

            sendable = sendable.Take(settings.MaxFilesPerRun.Value).ToList();
        }

        var processRun = await outputService.StartProcessRunAsync(
            new ProcessRun
            {
                Description = "Batch process request for WR51 single file process from inspection_report_finder_result",
                StartDateTimeUtc = DateTime.UtcNow,
                NumberOfFiles = sendable.Count,
                Status = "Batch",
                DocumentType = "WrInspectionReport"
            });

        var requests = sendable
            .Select(candidate =>
            {
                var destinationFileName = $"{candidate.PermitNumber!.ToLower()}__{candidate.FileId!.ToLower()}.pdf";

                return new FileProcessSingleRequest
                {
                    FilePath = destinationFileName,
                    DestinationFileName = destinationFileName,
                    DmsPath = candidate.FileUrl,
                    FileId = Guid.Parse(candidate.FileId),
                    PermitNumber = candidate.PermitNumber,
                    RegionId = GeneralConstants.UnsetRegionCode,
                    ProcessRunId = processRun.ProcessRunId,
                    RequestedAt = DateTime.UtcNow,
                    DocumentType = "WrInspectionReport"
                };
            })
            .ToList();

        var enqueued = 0;
        var failed = 0;

        // Chunked, and the failure handling is per chunk rather than around the whole loop: a run is
        // tens of thousands of files, and one rejected send previously threw out of the entire
        // foreach, leaving every remaining file silently unqueued while the process run still
        // claimed the full count.
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

        // NumberOfFiles is written before sending, because every message needs the run id, so a
        // send failure leaves the row overstating the run. Logged rather than corrected: there is no
        // write path for it on IOutputService today, and adding one is its own change.
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
