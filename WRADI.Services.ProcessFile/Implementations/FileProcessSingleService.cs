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
        SingleFileProcessRequest singleFileProcessRequest,
        CancellationToken cancellationToken)
    {
        if (singleFileProcessRequest.FilePath == null)
        {
            throw new ArgumentNullException(nameof(singleFileProcessRequest));
        }
        
        if (settings.RefreshCache)
        {
            await cacheService.ClearCacheAsync();
        }

        await cacheService.SetupAsync();
        await outputService.SetupAsync();

        var firstNamesTask = cacheService.GetFirstNamesAsync();
        
        var naldDataTask = SharedHelper.GetNaldDataAsync(null, cacheService);
        var naldLicenceStatusDataTask = cacheService.GetNaldLicenceStatusDataAsync();
        
        var dmsFileIdInformationListTask = cacheService.GetDmsFileIdInformationAsync();
        var dmsFileTask = DmsHelper.GetDmsFilesAndMappingAsync(
            fileService,
            string.Empty,
            false,
            cacheService);
        
        var firstNamesCsv = await firstNamesTask;

        var naldLicenceStatusData = await naldLicenceStatusDataTask;
        var allNaldData = await naldDataTask;

        var (dmsFilesToProcess, allDmsData) = await dmsFileTask;

        LicenceNumber.Instance = new LicenceNumber(allNaldData.AbstractionAndImpoundmentLicences!);

        var naldLinkedLicenceHelper = await NaldLinkedLicenceHelper.CreateAsync(
            cacheService);

        var naldData = ExternalDataHelper.TransformNaldData(
            allNaldData,
            allDmsData);

        var dmsFileIdInformationDict = DmsHelper.TranformDmsFileIdInformation(
            await dmsFileIdInformationListTask);
        
        var lookupConfig = new LookupConfiguration(
            WalLabelConfiguration.GetLabels(),
            allDmsData,
            dmsFileIdInformationDict,
            firstNamesCsv,
            fileService,
            cacheService,
            GeneralConstants.UnsetRegionCode,
            naldLinkedLicenceHelper: naldLinkedLicenceHelper);

        var processRuns = await outputService.GetAllProcessRunsAsync();
        var processRun = processRuns.Single(pr => pr.ProcessRunId == singleFileProcessRequest.ProcessRunId);

        ConsoleHelper.WriteLine(
            $"INFO - {nameof(FileProcessSingleService)} - Start file to output for " +
            $"{singleFileProcessRequest.FilePath} processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        
        var processRunFile = await outputService.AddProcessRunFileAsync(
            new ProcessRunFile
            {
                ProcessRunId = processRun.ProcessRunId,
                FileName = singleFileProcessRequest.FilePath
            });

        try
        {
            var licenceSet = await ScrapeDocumentAsync(
                singleFileProcessRequest.FilePath,
                naldLicenceStatusData,
                naldData,
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
                $"{singleFileProcessRequest.FilePath} processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            await outputService.MarkProcessRunFileCompleteAsync(processRunFile);

            ConsoleHelper.WriteLine(
                $"INFO - {nameof(FileProcessSingleService)} - Attempted marking batch as completed (if completed) " +
                $"processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            processRun = await outputService.MarkProcessRunCompleteIfCompleteAsync(processRun);

            var processRunCompleted = processRun is { EndDateTimeUtc: not null }
                && processRun.EndDateTimeUtc > DateTime.MinValue;

            ConsoleHelper.WriteLine(
                $"INFO - {nameof(FileProcessSingleService)} - Successfully finished processing {singleFileProcessRequest.FilePath} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            if (!processRunCompleted)
            {
                return true;
            }
            
            await AddCompleteProcessRunDataAsync(
                processRun,
                naldLicenceStatusData,
                naldData,
                allDmsData);

            return true;
        }
        catch (Exception e)
        {
            ConsoleHelper.WriteLine(
                $" {e.Message} = {e}, ERROR - {nameof(FileProcessSingleService)} - Exception on processing {singleFileProcessRequest.FilePath} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            throw;
        }
        finally
        {
            pdfDataExtractor.Dispose();
        }
    }
    
    private async Task<List<LicenceSet>> ScrapeDocumentAsync(
        string pdfFilename,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
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
                naldLicenceStatusData,
                naldData,
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
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> allDmsData)
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

        var allLicenceSets = WalSchemaConverter.AddAdditionalLicenceSets(
            licenceSetGroups,
            naldLicenceStatusData,
            naldData,
            allDmsData);

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