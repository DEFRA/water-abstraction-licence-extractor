using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tabula;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Services;

namespace WRADI.Services.ProcessFile.WrInspectionReport.Implementations;

public class FileProcessSingleService(
    FileProcessAppSettings settings,
    ICacheService cacheService,
    IOutputService outputService,
    IFileService fileService,
    IPdfDataExtractorService pdfDataExtractor)
    : IFileProcessSingleService
{
    public async Task<bool> RunAsync(
        FileProcessSingleRequest fileProcessSingleRequest,
        CancellationToken cancellationToken)
    {
        ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessSingleService)} - Started");

        if (fileProcessSingleRequest.FilePath == null || fileProcessSingleRequest.ProcessRunId == null)
        {
            throw new ArgumentNullException(nameof(fileProcessSingleRequest));
        }

        if (settings.RefreshCache)
        {
            await cacheService.ClearCacheAsync();
        }

        await cacheService.SetupAsync();
        await outputService.SetupAsync();

        var fileId = FileHelper.ExtractFileId(fileProcessSingleRequest.FilePath);

        if (fileId == null)
        {
            throw new ArgumentException($"Could not extract a file id from '{fileProcessSingleRequest.FilePath}'");
        }

        // Built through the shared factory so the accuracy harness scores this exact pipeline.
        var lookupConfig = WrInspectionReportLookupConfigurationFactory.Build(
            fileService,
            cacheService,
            outputService,
            new TabulaTableExtractorService(cacheService),
            fileProcessSingleRequest.RegionId,
            fileProcessSingleRequest.RequestedAt,
            fileProcessSingleRequest.LockRetryCount);

        if (fileProcessSingleRequest.ProcessRunId == null)
        {
            throw new InvalidOperationException(
                $"No process run id on the request for {fileProcessSingleRequest.FilePath}");
        }

        var processRun = new ProcessRun { ProcessRunId = fileProcessSingleRequest.ProcessRunId.Value };

        ConsoleHelper.WriteLine(
            $"INFO - {nameof(FileProcessSingleService)} - Start file to output for " +
            $"{fileProcessSingleRequest.FilePath} processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var processRunFile = await outputService.AddProcessRunFileAsync(
            new ProcessRunFile
            {
                ProcessRunId = processRun.ProcessRunId,
                FileName = fileProcessSingleRequest.FilePath
            });

        try
        {
            var dmsFileData = new DmsFileData
            {
                FileId = fileId.Value,
                PermitNumber = fileProcessSingleRequest.PermitNumber,
                DmsPath = fileProcessSingleRequest.DmsPath,
                DestinationFileName = fileProcessSingleRequest.DestinationFileName
            };

            var (stopExecution, errorMessage) = await ScrapeDocumentAsync(
                fileProcessSingleRequest.FilePath,
                lookupConfig,
                dmsFileData,
                processRun);

            if (stopExecution)
            {
                return true;
            }

            if (errorMessage != null)
            {
                processRunFile.ErrorMessage = errorMessage;
                await outputService.ReportErrorProcessRunFileAsync(processRunFile);
            }

            // Marked complete even on failure: MarkProcessRunCompleteIfCompleteAsync waits for
            // every file to carry an end date before the run reports finished.
            await outputService.MarkProcessRunFileCompleteAsync(processRunFile);

            ConsoleHelper.WriteLine(
                $"INFO - {nameof(FileProcessSingleService)} - Attempted marking batch as completed (if completed) " +
                $"processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            processRun = await outputService.MarkProcessRunCompleteIfCompleteAsync(processRun);

            ConsoleHelper.WriteLine(errorMessage == null
                ? $"INFO - {nameof(FileProcessSingleService)} - Successfully finished processing " +
                    $"{fileProcessSingleRequest.FilePath} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                : $"ERROR - {nameof(FileProcessSingleService)} - Finished {fileProcessSingleRequest.FilePath} " +
                    $"without extracting it at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            var processRunCompleted = processRun is { EndDateTimeUtc: not null }
                && processRun.EndDateTimeUtc > DateTime.MinValue;

            if (processRunCompleted)
            {
                FireAndForgetListProjection(processRun.ProcessRunId);
            }

            return true;
        }
        catch (Exception e)
        {
            ConsoleHelper.WriteLine(
                $" {e.Message} = {e}, ERROR - {nameof(FileProcessSingleService)} - Exception on processing {fileProcessSingleRequest.FilePath} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            throw;
        }
        finally
        {
            pdfDataExtractor.Dispose();
        }
    }

    /// Null when the file cannot be read, which leaves the overlay off for that document rather
    /// than failing the whole extraction.
    private async Task<byte[]?> GetPdfBytesAsync(string pdfFilename)
    {
        try
        {
            await using var stream = await fileService.GetFileAsStreamAsync(pdfFilename);

            if (stream == null)
            {
                return null;
            }

            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            return memoryStream.ToArray();
        }
        catch (Exception exception)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - could not read "
                + $"{pdfFilename} for the table overlay, continuing without it: {exception.Message}");

            return null;
        }
    }

    /// Projects the whole run once, from whichever file turns out to be the last. Detached and
    /// swallowing, because the file it runs under has already succeeded and a projection failure
    /// must not fail it - the rebuild tool re-runs this on demand.
    private void FireAndForgetListProjection(int processRunId)
    {
        _ = Task.Run((Func<Task?>)(async () =>
        {
            try
            {
                await outputService.UpdateProcessRunListProjectionAsync(processRunId);
            }
            catch (Exception exception)
            {
                ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - could not "
                    + $"project the list for process run {processRunId}: {exception.Message}");
            }
        }));
    }

    private async Task<(bool StopExecution, string? ErrorMessage)> ScrapeDocumentAsync(
        string pdfFilename,
        LookupConfiguration lookupConfig,
        DmsFileData dmsDataForFile,
        ProcessRun processRun)
    {
        var dtStart = DateTime.Now;
        ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessSingleService)} - Started {pdfFilename} " +
            $"at {dtStart:yyyy-MM-dd HH:mm:ss}");

        try
        {
            var tableExtractorService = lookupConfig.UnstructuredTableExtractorService;

            // The grid overlay needs the file's own bytes, which the orchestrator reopens with
            // PdfClown rather than reusing the extractor's PdfPig document.
            var pdfBytes = await GetPdfBytesAsync(pdfFilename);

            var (stopExecution, alreadySaved, item, _) =
                await WrInspectionReportExtractionOrchestrator.ExtractAsync(
                    pdfFilename,
                    dmsDataForFile,
                    lookupConfig,
                    lookupConfig,
                    [pdfFilename],
                    processRun.ProcessRunId,
                    pdfDataExtractor,
                    // Resolves the LicenceProvisions grid fields, leaving any it cannot resolve
                    // with their existing heuristic result. Tabula only - the paid Document
                    // Intelligence extractor stays behind the orchestrator's cost-optimised path.
                    pdfBytes == null ? null : tableExtractorService,
                    pdfBytes);

            if (stopExecution)
            {
                return (true, null);
            }

            if (alreadySaved != true && item != null)
            {
                await pdfDataExtractor.SaveMatchResultAsync(
                    item,
                    dmsDataForFile.FileId,
                    processRun.ProcessRunId,
                    lookupConfig.UseLockExclusivity);
            }

            var duration = (DateTime.Now - dtStart).TotalMilliseconds;
            ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessSingleService)} - Finished ({pdfFilename} in " +
                $"{duration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return (false, null);
        }
        catch (TooManyPagesException)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - Skipped '{pdfFilename}' " +
                $"as too many pages");

            return (false, null);
        }
        catch (TooManyImagesException)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - Skipped '{pdfFilename}' " +
                $"as too many images");
            
            return (false, null);
        }
        catch (OcrServiceTransientException ex)
        {
            // Deliberately not swallowed: the matches_result row has been left as TransientError and
            // the queue message must be marked failed so SQS redelivers it once the throttle passes.
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - '{pdfFilename}' failed on a " +
                $"transient OCR error from {ex.ServiceName}, leaving for retry - {ex.Message}");

            throw;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"FATAL ERROR - {nameof(FileProcessSingleService)} - {pdfFilename} threw " +
                $"fatal error - {ex}");

            // Recorded rather than marked failed: which failures earn a redelivery, and which are
            // a permanently unreadable file that would cycle the queue instead, is not decided yet.
            return (false, ex.ToString());
        }
        finally
        {
            pdfDataExtractor.InUse = false;
        }
    }
}
