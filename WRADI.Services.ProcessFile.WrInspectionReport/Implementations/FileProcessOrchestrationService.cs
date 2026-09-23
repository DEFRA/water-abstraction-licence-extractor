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
