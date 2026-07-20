using System.Collections.Concurrent;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;

namespace WRADI.Services.ProcessFile.Implementations;

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
        var firstNamesCsvTask = cacheService.GetFirstNamesAsync();

        var abstractionAndImpoundmentLicencesTask =
            SharedHelper.GetNaldImpoundmentAndAbstractionLicencesAsync(cacheService);
        
        var dmsAndNaldFileDataTask = DmsHelper.GetDmsAndNaldFileData(cacheService, fileId!.Value);
        var naldLinkedLicenceHelperTask = NaldLinkedLicenceHelper.CreateAsync(cacheService);
        
        LicenceNumber.Instance = new LicenceNumber(await abstractionAndImpoundmentLicencesTask);
        var naldLinkedLicenceHelper = await naldLinkedLicenceHelperTask;
        var (dmsFileData, naldLicence) = await dmsAndNaldFileDataTask;
        
        var lookupConfig = new LookupConfiguration(
            WalLabelConfiguration.GetLabels(),
            await firstNamesCsvTask,
            fileService,
            cacheService,
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
            var (stopExecution, licenceSets) = await ScrapeDocumentAsync(
                fileProcessSingleRequest.FilePath,
                lookupConfig,
                dmsFileData,
                naldLicence.LicenceNumber,
                processRun);

            if (stopExecution)
            {
                return true;
            }
            
            if (licenceSets.Count > 0)
            {
                await SharedHelper.UpdateAndSaveLicenceSetsAsync(
                    [licenceSets],
                    licenceSets,
                    outputService,
                    processRun);

                ConsoleHelper.WriteLine(
                    $"INFO - {nameof(FileProcessSingleService)} - Marking process run file as complete for " +
                    $"{fileProcessSingleRequest.FilePath} processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            
            await outputService.MarkProcessRunFileCompleteAsync(processRunFile);

            ConsoleHelper.WriteLine(
                $"INFO - {nameof(FileProcessSingleService)} - Attempted marking batch as completed (if completed) " +
                $"processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            processRun = await outputService.MarkProcessRunCompleteIfCompleteAsync(processRun);

            var processRunCompleted = processRun is { EndDateTimeUtc: not null }
                && processRun.EndDateTimeUtc > DateTime.MinValue;

            ConsoleHelper.WriteLine(
                $"INFO - {nameof(FileProcessSingleService)} - Successfully finished processing {fileProcessSingleRequest.FilePath} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            if (!processRunCompleted)
            {
                return true;
            }
            
            await AddCompleteProcessRunDataAsync(
                processRun,
                lookupConfig);

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
    
    private async Task<(bool StopExecution, List<LicenceSet> LicenceSets)> ScrapeDocumentAsync(
        string pdfFilename,
        LookupConfiguration lookupConfig,
        DmsFileData dmsDataForFile,
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
                return (true, []);
            }
            
            if (alreadySaved == true)
            {
                // Everything already saved (or we're waiting), should have licence sets etc...
                return (false, []);
            }
            
            var matchResultId = await outputService.SaveMatchResultAsync(
                matchesResult!,
                dmsDataForFile.FileId,
                processRun.ProcessRunId);

            var dtStartSaveMatches = DateTime.Now;

            if (matchesResult!.Matches != null)
            {
                var matches = matchesResult.Matches
                    .Select(match => (
                        matchResultId,
                        match.MatchedLabelName,
                        match.LabelGroupName,
                        match))
                    .ToList();

                await outputService.SaveMatchesAsync(matches);

                var saveDuration = (DateTime.Now - dtStartSaveMatches).TotalMilliseconds;
                ConsoleHelper.WriteLine(
                    $"INFO - {nameof(FileProcessSingleService)} - Saved '{pdfFilename}' in {saveDuration}ms " +
                    $"at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }

            var duration = (DateTime.Now - dtStart).TotalMilliseconds;
            ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessSingleService)} - Finished ({pdfFilename} in " +
                $"{duration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return (false, await WalSchemaConverter.ToLicenceSetsAsync(
                matchesResult,
                pdfDataExtractor,
                processRun.ProcessRunId,
                lookupConfig,
                dmsDataForFile,
                naldLicenceNumber));
        }
        catch (TooManyPagesException)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - Skipped '{pdfFilename}' " +
                $"as too many pages");
            
            return (false, []);
        }
        catch (TooManyImagesException)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - Skipped '{pdfFilename}' " +
                $"as too many images");
            
            return (false, []);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"FATAL ERROR - {nameof(FileProcessSingleService)} - {pdfFilename} threw " +
                $"fatal error - {ex}");
            
            return (false, []);
        }
        finally
        {
            pdfDataExtractor.InUse = false;
        }
    }
    
    private async Task AddCompleteProcessRunDataAsync(
        ProcessRun processRun,
        LookupConfiguration lookupConfiguration)
    {
        ConsoleHelper.WriteLine(
            $"INFO - {nameof(FileProcessSingleService)} - started processing all license sets at " +
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
        var allLicenceSets1 =
            await outputService.GetProcessRunLicenceSetsAsync(processRun.ProcessRunId);

        var licenceSetGroups = allLicenceSets1
            .Values
            .Select(IReadOnlyList<LicenceSet> (licenceSet) => new List<LicenceSet> { licenceSet })
            .ToList();

        var allLicenceSets = await WalSchemaConverter.AddAdditionalLicenceSetsAsync(
            licenceSetGroups,
            lookupConfiguration);

        ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessSingleService)} - Converted into all licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        WalSchemaConverter.CalculateCombinedAggregates(allLicenceSets);
        
        await SharedHelper.UpdateAndSaveLicenceSetsAsync(
            licenceSetGroups,
            allLicenceSets,
            outputService,
            processRun);
        
        ConsoleHelper.WriteLine(
            $"INFO - {nameof(FileProcessSingleService)} - completed processing all license sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }
}