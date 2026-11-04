using WALE.Api.Areas.BFF.SignalR;
using WALE.Api.Interfaces;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using Microsoft.AspNetCore.SignalR;

namespace WALE.Api.Services;

public class LicenceEstateExportRunner(
    ILicenceEstateExportService licenceEstateExportService,
    IFileService fileService,
    IHubContext<ProcessHub> hubContext) : ILicenceEstateExportRunner
{
    public void StartRunExport(int processRunId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ExportAsync(processRunId);
            }
            catch (Exception exception)
            {
                ConsoleHelper.WriteLine($"ERROR - {nameof(LicenceEstateExportRunner)} - process "
                    + $"run {processRunId} - {exception.Message}");

                await NotifyFailedAsync(processRunId, exception.Message);
            }
        });
    }

    public async Task<string?> GetRunExportUrlAsync(int processRunId)
    {
        var key = Key(processRunId);

        return await fileService.ExistsAsync(key, StorageFolder.Assets)
            ? await fileService.GetPresignedUrlAsync(key, StorageFolder.Assets)
            : null;
    }

    private async Task ExportAsync(int processRunId)
    {
        // A file rather than memory: a run's zip reaches tens of megabytes.
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

        // Deleted before the run, so the url endpoint cannot serve the last zip as this one.
        await fileService.DeleteAsync(Key(processRunId), StorageFolder.Assets);

        try
        {
            LicenceEstateExportSummary summary;

            await using (var file = File.Create(tempPath))
            {
                summary = await licenceEstateExportService.ExportRunAsync(processRunId, file);
            }

            await using (var upload = File.OpenRead(tempPath))
            {
                await fileService.UploadFileAsStreamAsync(
                    Key(processRunId),
                    upload,
                    "application/zip",
                    StorageFolder.Assets);
            }

            await NotifyCompletedAsync(processRunId, summary);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static string Key(int processRunId) => $"exports/licence-estate-run-{processRunId}.zip";

    // Not ProcessCompleted/ProcessFailed: UiProcessRunService sends those on the same
    // process-{id} group, and RefreshLicenceListData takes any of them as its own.
    private Task NotifyCompletedAsync(int processRunId, LicenceEstateExportSummary summary) =>
        hubContext.Clients
            .Group($"process-{processRunId}")
            .SendAsync("LicenceEstateExportCompleted", new
            {
                ProcessRunId = processRunId,
                Status = "Completed",
                Message = $"Exported {summary.Written} licences for process run {processRunId}"
                    + $", {summary.NotExtracted} the run did not extract."
            });

    private Task NotifyFailedAsync(int processRunId, string reason) =>
        hubContext.Clients
            .Group($"process-{processRunId}")
            .SendAsync("LicenceEstateExportFailed", new
            {
                ProcessRunId = processRunId,
                Status = "Failed",
                Message = $"Export failed for process run {processRunId} - {reason}"
            });
}
