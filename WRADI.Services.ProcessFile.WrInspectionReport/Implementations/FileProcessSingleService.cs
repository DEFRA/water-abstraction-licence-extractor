using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Services.Services;
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

        var lookupConfig = new LookupConfiguration(
            WrInspectionReportLabelConfiguration.GetLabels(),
            [],
            fileService,
            cacheService,
            outputService,
            new NullLicenceNumberService(),
            new DmsLookupService(),
            fileProcessSingleRequest.RegionId,
            fileProcessSingleRequest.RequestedAt,
            fileProcessSingleRequest.LockRetryCount,
            lineHeight: 6,
            minimumRowsForDigital: 30,
            useAnchoredLineGrouping: true);

        var processRuns = await outputService.GetAllProcessRunsAsync();
        var processRun = processRuns.Single(pr => pr.ProcessRunId == fileProcessSingleRequest.ProcessRunId);

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

            var stopExecution = await ScrapeDocumentAsync(
                fileProcessSingleRequest.FilePath,
                lookupConfig,
                dmsFileData,
                processRun);

            if (stopExecution)
            {
                return true;
            }

            await outputService.MarkProcessRunFileCompleteAsync(processRunFile);

            ConsoleHelper.WriteLine(
                $"INFO - {nameof(FileProcessSingleService)} - Attempted marking batch as completed (if completed) " +
                $"processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            await outputService.MarkProcessRunCompleteIfCompleteAsync(processRun);

            ConsoleHelper.WriteLine(
                $"INFO - {nameof(FileProcessSingleService)} - Successfully finished processing {fileProcessSingleRequest.FilePath} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

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

    private async Task<bool> ScrapeDocumentAsync(
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
            var (stopExecution, alreadySaved, item, _) = await WrInspectionReportExtractionOrchestrator.ExtractAsync(
                pdfFilename,
                dmsDataForFile,
                lookupConfig,
                [pdfFilename],
                processRun.ProcessRunId,
                pdfDataExtractor);

            if (stopExecution)
            {
                return true;
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

            return false;
        }
        catch (TooManyPagesException)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - Skipped '{pdfFilename}' " +
                $"as too many pages");

            return false;
        }
        catch (TooManyImagesException)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - Skipped '{pdfFilename}' " +
                $"as too many images");

            return false;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"FATAL ERROR - {nameof(FileProcessSingleService)} - {pdfFilename} threw " +
                $"fatal error - {ex}");

            return false;
        }
        finally
        {
            pdfDataExtractor.InUse = false;
        }
    }
}
