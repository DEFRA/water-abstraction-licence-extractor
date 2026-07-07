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

        var firstNamesCsvTask = cacheService.GetFirstNamesAsync();

        var abstractionAndImpoundmentLicencesTask =
            SharedHelper.GetNaldImpoundmentAndAbstractionLicencesAsync(cacheService);

        var dmsFileTask = DmsHelper.GetDmsFilesAndMappingAsync( // TODO only need 1, could do it on the fly
            fileService,
            string.Empty,
            false,
            cacheService);
        
        var naldLinkedLicenceHelperTask = NaldLinkedLicenceHelper.CreateAsync(cacheService);
        
        var (dmsFilesToProcess, allDmsData) = await dmsFileTask;

        LicenceNumber.Instance = new LicenceNumber(await abstractionAndImpoundmentLicencesTask);
        var naldLinkedLicenceHelper = await naldLinkedLicenceHelperTask;
        
        var lookupConfig = new LookupConfiguration(
            WalLabelConfiguration.GetLabels(),
            allDmsData,
            await firstNamesCsvTask,
            fileService,
            cacheService,
            GeneralConstants.UnsetRegionCode,
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
            var licenceSet = await ScrapeDocumentAsync(
                fileProcessSingleRequest.FilePath,
                lookupConfig,
                dmsFilesToProcess.FirstOrDefault().Value,
                processRun);

            await SharedHelper.UpdateAndSaveLicenceSetsAsync(
                [licenceSet],
                licenceSet,
                outputService,
                processRun);

            ConsoleHelper.WriteLine(
                $"INFO - {nameof(FileProcessSingleService)} - Marking process run file as complete for " +
                $"{fileProcessSingleRequest.FilePath} processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

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
                allDmsData,
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
    
    private async Task<List<LicenceSet>> ScrapeDocumentAsync(
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
            var previouslyParsedFiles = new List<string>
            {
                pdfFilename
            };

            lookupConfig.RegionId = dmsDataForFile.RegionId;
            
            var matchesFull = await pdfDataExtractor.GetMatchesAsync(
                pdfFilename,
                dmsDataForFile,
                lookupConfig,
                previouslyParsedFiles,
                processRun.ProcessRunId);

            var matchResultId = await outputService.SaveMatchResultAsync(
                matchesFull,
                dmsDataForFile.FileId,
                processRun.ProcessRunId);

            var dtStartSaveMatches = DateTime.Now;

            if (matchesFull.Matches != null)
            {
                var matches = matchesFull.Matches
                    .Select(match => (
                        matchResultId,
                        match.MatchedLabel?.Name,
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

            return await WalSchemaConverter.ToLicenceSetsAsync(
                matchesFull,
                pdfDataExtractor,
                processRun.ProcessRunId,
                lookupConfig,
                dmsDataForFile);
        }
        catch (TooManyPagesException)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - Skipped '{pdfFilename}' " +
                $"as too many pages");
            
            return [];
        }
        catch (TooManyImagesException)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleService)} - Skipped '{pdfFilename}' " +
                $"as too many images");
            
            return [];
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"FATAL ERROR - {nameof(FileProcessSingleService)} - {pdfFilename} threw " +
                $"fatal error - {ex}");
            
            return [];
        }
        finally
        {
            pdfDataExtractor.InUse = false;
        }
    }
    
    private async Task AddCompleteProcessRunDataAsync(
        ProcessRun processRun,
        Dictionary<string, DmsFileData> allDmsData,
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
            allDmsData,
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