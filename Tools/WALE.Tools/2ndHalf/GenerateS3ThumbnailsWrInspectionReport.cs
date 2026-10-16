using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.Output;
using WALE.Tools.Config;

namespace WALE.Tools._2ndHalf;

/// <summary>
/// WR51 equivalent of GenerateS3Thumbnails, which can only walk AbstractionLicence records.
/// File ids come from matches_result instead, which is document-type agnostic.
///
/// Needed because the thumbnail is only written when a document is actually rendered, and
/// no_ocr_pages_metadata_cache is keyed on file id and service name with no process-run
/// component - so any run over an already-cached corpus takes the fast path and writes none.
/// The page screenshots still exist, so the API can regenerate from them.
///
/// Safe to re-run: the upload endpoint skips keys already present.
/// </summary>
public static class GenerateS3ThumbnailsWrInspectionReport
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri(KeyConfig.ApiBaseUrl)
    };

    private static readonly IOutputService OutputService = new ApiOutputService(HttpClient);

    public static async Task RunAsync(int processRunId)
    {
        var fileIds = (await OutputService.GetSimpleMatchResults(processRunId))
            .Select(result => result.FileId)
            .Where(fileId => fileId != Guid.Empty)
            .Distinct()
            .ToList();

        ConsoleHelper.WriteLine($"Generating thumbnails for {fileIds.Count} files in process run {processRunId}");

        var tasks = new List<Task<bool>>();
        const int maxConcurrent = 12;
        var loopIdx = 1;
        var failures = 0;

        foreach (var fileId in fileIds)
        {
            tasks.Add(SaveThumbnailAsync(fileId, loopIdx++, fileIds.Count));

            while (tasks.Count >= maxConcurrent)
            {
                failures += await RemoveCompletedAsync(tasks);
            }
        }

        while (tasks.Count != 0)
        {
            failures += await RemoveCompletedAsync(tasks);
        }

        ConsoleHelper.WriteLine(
            $"Finished - {fileIds.Count - failures} of {fileIds.Count} generated, {failures} failed");
    }

    private static async Task<int> RemoveCompletedAsync(List<Task<bool>> tasks)
    {
        await Task.WhenAny(tasks);

        var completed = tasks.Where(task => task.IsCompleted).ToList();
        var failures = 0;

        foreach (var task in completed)
        {
            tasks.Remove(task);

            // Awaited rather than read off IsFaulted so the result is observed either way.
            if (!await task)
            {
                failures++;
            }
        }

        return failures;
    }

    private static async Task<bool> SaveThumbnailAsync(Guid fileId, int fileNumber, int totalNumber)
    {
        try
        {
            await OutputService.SaveThumbnailAsync(fileId);

            if (fileNumber % 500 == 0)
            {
                ConsoleHelper.WriteLine($"{fileNumber} of {totalNumber}");
            }

            return true;
        }
        catch (Exception ex)
        {
            // A handful of documents are malformed enough that the renderer can't produce a page
            // image at all. Skipped rather than aborting the backfill - the AbstractionLicence
            // version has no handling here and would stop on the first one.
            ConsoleHelper.WriteLine($"ERROR - could not generate thumbnail for {fileId} - {ex.Message}");
            return false;
        }
    }
}
