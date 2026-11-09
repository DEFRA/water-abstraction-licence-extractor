using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Services.Services;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.DocumentType.AbstractionLicence.Formats;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.DocumentType.AbstractionLicence.Interfaces;

namespace WRADI.Services.ProcessFile.AbstractionLicence.Implementations;

public class FileProcessSingleService(
    FileProcessAppSettings settings,
    IAbstractionLicenceCacheService abstractionLicenceCacheService,
    ICacheService cacheService,
    IAbstractionLicenceOutputService abstractionLicenceOutputService,
    IOutputService outputService,
    IFileService fileService,
    IPdfDataExtractorService pdfDataExtractor,
    INaldDataLookupService naldDataLookupService)
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

        var firstNamesCsvTask = cacheService.GetFirstNamesAsync();
        var licenceNumberSuccessorsTask = abstractionLicenceCacheService.GetNaldLicenceNumberHistoryAsync();
        
        var abstractionAndImpoundmentLicencesTask =
            SharedHelper.GetNaldImpoundmentAndAbstractionLicencesAsync(abstractionLicenceCacheService);
        
        var dmsAndNaldFileDataTask = DmsHelper.GetDmsAndNaldFileData(
            abstractionLicenceCacheService,
            fileId!.Value);
        
        var licenceNumberService = new AbstractionLicenceNumber(
            await abstractionAndImpoundmentLicencesTask,
            await licenceNumberSuccessorsTask);

        var dmsLookupService = new DmsLookupService();
        
        var naldLinkedLicenceHelperTask = NaldLinkedLicenceHelper.CreateAsync(
            abstractionLicenceCacheService,
            licenceNumberService);
        
        var naldLinkedLicenceHelper = await naldLinkedLicenceHelperTask;
        var (dmsFileData, naldLicence) = await dmsAndNaldFileDataTask;
        
        var lookupConfig = new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            await firstNamesCsvTask,
            fileService,
            cacheService,
            outputService,
            licenceNumberService,
            null,
            null,
            dmsLookupService,
            fileProcessSingleRequest.RegionId,
            fileProcessSingleRequest.RequestedAt,
            fileProcessSingleRequest.LockRetryCount,
            naldLinkedLicenceHelper: naldLinkedLicenceHelper);

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
            var (stopExecution, licenceSets, errorMessage) = await ScrapeDocumentAsync(
                fileProcessSingleRequest.FilePath,
                lookupConfig,
                dmsFileData,
                naldDataLookupService,
                naldLicence.LicenceNumber,
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
            
            if (licenceSets.Count > 0)
            {
                await SharedHelper.UpdateAndSaveLicenceSetsAsync(
                    [licenceSets],
                    licenceSets,
                    abstractionLicenceOutputService,
                    processRun);

                ConsoleHelper.WriteLine(
                    $"INFO - {nameof(FileProcessSingleService)} - Marking process run file as complete for " +
                    $"{fileProcessSingleRequest.FilePath} processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            
            // Marked complete even on failure: MarkProcessRunCompleteIfCompleteAsync waits for
            // every file to carry an end date before the run reports finished.
            await outputService.MarkProcessRunFileCompleteAsync(processRunFile);

            ConsoleHelper.WriteLine(
                $"INFO - {nameof(FileProcessSingleService)} - Attempted marking batch as completed (if completed) " +
                $"processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            processRun = await outputService.MarkProcessRunCompleteIfCompleteAsync(processRun);

            var processRunCompleted = processRun is { EndDateTimeUtc: not null }
                && processRun.EndDateTimeUtc > DateTime.MinValue;

            ConsoleHelper.WriteLine(errorMessage == null
                ? $"INFO - {nameof(FileProcessSingleService)} - Successfully finished processing " +
                    $"{fileProcessSingleRequest.FilePath} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                : $"ERROR - {nameof(FileProcessSingleService)} - Finished {fileProcessSingleRequest.FilePath} " +
                    $"without extracting it at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            if (!processRunCompleted)
            {
                return true;
            }
            
            await AddCompleteProcessRunDataAsync(
                processRun,
                lookupConfig,
                naldDataLookupService);

            FireAndForgetDataRefresh(processRun.ProcessRunId);
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
    
    private void FireAndForgetDataRefresh(int processRunId)
    {
        _ = Task.Run((Func<Task?>)(async () =>
        {
            try
            {
                await outputService
                    .UpdateProcessRunListProjectionAsync(processRunId);
            }
            catch
            {
               // intentionally swallowed
            }
        }));
    }
    
    private async Task<(bool StopExecution, List<LicenceSet> LicenceSets, string? ErrorMessage)> ScrapeDocumentAsync(
        string pdfFilename,
        LookupConfiguration lookupConfig,
        DmsFileData dmsDataForFile,
        INaldDataLookupService naldDataLookupService,
        string naldLicenceNumber,
        ProcessRun processRun)
    {
        var dtStart = DateTime.Now;
        ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessSingleService)} - Started {pdfFilename} " +
            $"at {dtStart:yyyy-MM-dd HH:mm:ss}");

        try
        {
            var previouslyParsedFiles = new List<string>
            {
                pdfFilename
            };
            
            var (stopExecution, alreadySaved, matchesResult) = await pdfDataExtractor.GetMatchesAsync(
                pdfFilename,
                dmsDataForFile,
                lookupConfig,
                previouslyParsedFiles,
                processRun.ProcessRunId);

            if (stopExecution)
            {
                return (stopExecution, [], null);
            }

            if (alreadySaved != true)
            {
                await pdfDataExtractor.SaveMatchResultAsync(
                    matchesResult!,
                    dmsDataForFile.FileId,
                    processRun.ProcessRunId,
                    lookupConfig.UseLockExclusivity);
            }

            var duration = (DateTime.Now - dtStart).TotalMilliseconds;
            ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessSingleService)} - Finished ({pdfFilename} in " +
                $"{duration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return (false, await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
                matchesResult!,
                pdfDataExtractor,
                processRun.ProcessRunId,
                lookupConfig,
                abstractionLicenceCacheService,
                naldDataLookupService,
                dmsDataForFile,
                naldLicenceNumber), null);
        }
        catch (TooManyPagesException)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - Skipped '{pdfFilename}' " +
                $"as too many pages");
            
            return (false, [], null);
        }
        catch (TooManyImagesException)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - Skipped '{pdfFilename}' " +
                $"as too many images");
            
            return (false, [], null);
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
            return (false, [], ex.ToString());
        }
        finally
        {
            pdfDataExtractor.InUse = false;
        }
    }
    
    private async Task AddCompleteProcessRunDataAsync(
        ProcessRun processRun,
        LookupConfiguration lookupConfiguration,
        INaldDataLookupService naldDataLookupService)
    {
        ConsoleHelper.WriteLine(
            $"INFO - {nameof(FileProcessSingleService)} - started processing all license sets at " +
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
        var allLicenceSets1 =
            await abstractionLicenceOutputService.GetProcessRunLicenceSetsAsync(processRun.ProcessRunId);

        var licenceSetGroups = allLicenceSets1
            .Values
            .Select(IReadOnlyList<LicenceSet> (licenceSet) => new List<LicenceSet> { licenceSet })
            .ToList();

        var allLicenceSets = await AbstractionLicenceSchemaConverter.AddAdditionalLicenceSetsAsync(
            licenceSetGroups,
            lookupConfiguration,
            naldDataLookupService);

        ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessSingleService)} - Converted into all licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        AbstractionLicenceSchemaConverter.CalculateCombinedAggregates(allLicenceSets);
        
        await SharedHelper.UpdateAndSaveLicenceSetsAsync(
            licenceSetGroups,
            allLicenceSets,
            abstractionLicenceOutputService,
            processRun);
        
        ConsoleHelper.WriteLine(
            $"INFO - {nameof(FileProcessSingleService)} - completed processing all license sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }
}