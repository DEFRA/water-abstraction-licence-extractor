using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.Cache.WrInspectionReport.Interfaces;

namespace WRADI.Services.ProcessFile.WrInspectionReport.Implementations;

// WR51 equivalent of WRADI.Services.ProcessFile.AbstractionLicence's FileProcessOrchestrationService.
// Sources candidates from InspectionReportFinderResult (the SharePoint-discovery candidate list)
// instead of NALD/licence_finder_result. No "already processed" exclusion here, matching
// AbstractionLicence's own orchestrator exactly - it re-enumerates every candidate on every
// trigger and re-enqueues all of them; de-duplication happens later, per-file, via the generic
// check-then-claim pattern already inside PdfDataExtractorService.GetMatchesAsync.
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

        var processRun = await outputService.StartProcessRunAsync(
            new ProcessRun
            {
                Description = "Batch process request for WR51 single file process from inspection_report_finder_result",
                StartDateTimeUtc = DateTime.UtcNow,
                NumberOfFiles = candidates.Count,
                Status = "Batch",
                DocumentType = "WrInspectionReport"
            });

        try
        {
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate.PermitNumber)
                    || string.IsNullOrEmpty(candidate.FileId)
                    || !Guid.TryParse(candidate.FileId, out var fileId))
                {
                    continue;
                }

                var destinationFileName = $"{candidate.PermitNumber.ToLower()}__{candidate.FileId.ToLower()}.pdf";

                await messageQueueService.AddToFileProcessQueue(
                    new FileProcessSingleRequest
                    {
                        FilePath = destinationFileName,
                        DestinationFileName = destinationFileName,
                        DmsPath = candidate.FileUrl,
                        FileId = fileId,
                        PermitNumber = candidate.PermitNumber,
                        RegionId = GeneralConstants.UnsetRegionCode,
                        ProcessRunId = processRun.ProcessRunId,
                        RequestedAt = DateTime.UtcNow,
                        DocumentType = "WrInspectionReport"
                    });

                ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - {destinationFileName} sent to single process file queue");
            }
        }
        catch (Exception exception)
        {
            ConsoleHelper.WriteLine($"ERROR - {nameof(FileProcessOrchestrationService)} - Error during sending to single " +
                $"file processing queue: {exception}");

            throw;
        }

        ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - Finished processing " +
            $"at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        return true;
    }
}
