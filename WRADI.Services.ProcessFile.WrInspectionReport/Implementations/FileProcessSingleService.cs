using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Services.PdfClown;
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
            WrInspectionReportTextBasedLabelConfiguration.GetLabels(),
            [],
            fileService,
            cacheService,
            outputService,
            new NullLicenceNumberService(),
            null!,
            null!,
            new DmsLookupService(),
            fileProcessSingleRequest.RegionId,
            fileProcessSingleRequest.RequestedAt,
            fileProcessSingleRequest.LockRetryCount,
            lineHeight: 6,
            minimumRowsForDigital: 30);

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
            // KNOWN ISSUE (see git history / session notes): PdfClownGridTableExtractorService
            // depends on System.Drawing.Common/libgdiplus, and PDFClown.NET's own ContentScanner
            // was found to have at least two distinct memory-corruption bugs on Linux - a native
            // Matrix handle leak in GraphicsState.Clone()/CopyTo() that reliably segfaults after a
            // document-dependent number of table-border rectangles, and a separate
            // AccessViolationException in CompositeFont.LoadEncoding() on documents using
            // composite/CID-keyed fonts. Both crash the whole process rather than degrading
            // gracefully - this was deliberately reverted to Tabula for that reason. Wired back in
            // here on request for review, not because the underlying risk has changed.
            var pdfBytesForTableExtraction = await ReadPdfBytesAsync(pdfFilename);
            var tableExtractorService = new PdfClownGridTableExtractorService(cacheService);

            // WR51's own fluent rule builder (FromTableGrid/FromLetterAndTableGrid/etc. -
            // WrFluentRule.cs) marks every table-based label LayoutExtractorTableShape.Unstructured,
            // so PdfDataExtractorService.GetMatchesInternalAsync's needsToParseUnstructuredTables
            // check is always true here and throws NoNullAllowedException if this isn't set (added
            // 2026-09-23, see git blame - lookupConfig was never updated to supply it, so every
            // WR51 document failed at this point). Same ITableExtractorService instance/interface
            // as the orchestrator's own overlay below, so this is just wiring, not new extraction.
            lookupConfig.StructuredTableExtractorService = tableExtractorService;
            lookupConfig.UnstructuredTableExtractorService = tableExtractorService;

            var (stopExecution, alreadySaved, item, _) =
                await WrInspectionReportExtractionOrchestrator.ExtractAsync(
                    pdfFilename,
                    dmsDataForFile,
                    lookupConfig,
                    lookupConfig,
                    [pdfFilename],
                    processRun.ProcessRunId,
                    pdfDataExtractor,
                    tableExtractorService,
                    pdfBytesForTableExtraction);

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

    // A second, separate read of the same file pdfDataExtractor already reads internally for the
    // heuristic pass - same pattern the golden-set harness already uses (Wr51GroundTruthAccuracyTests.
    // RunHarnessAsync). Table extraction is a pure accuracy overlay, never load-bearing, so a read
    // failure here degrades to null (heuristic-only, today's behaviour) rather than failing the
    // document.
    private async Task<byte[]?> ReadPdfBytesAsync(string pdfFilename)
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
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - Could not read '{pdfFilename}' " +
                $"for table extraction, falling back to heuristic-only matching - {ex.Message}");

            return null;
        }
    }
}
