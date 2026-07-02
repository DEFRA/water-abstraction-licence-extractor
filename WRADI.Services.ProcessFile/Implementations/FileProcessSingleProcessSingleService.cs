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

public class FileProcessSingleProcessSingleService(
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
        
        var naldDataTask = GetNaldDataAsync(null);
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

        var dmsFileIdInformationDict = TranformDmsFileIdInformation(
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
            $"INFO - {nameof(FileProcessSingleProcessSingleService)} - Start file to output for " +
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

            await UpdateAndSaveLicenceSets(
                [licenceSet],
                naldLicenceStatusData,
                naldData,
                allDmsData,
                processRun);

            ConsoleHelper.WriteLine(
                $"INFO - {nameof(FileProcessSingleProcessSingleService)} - Marking process run file as complete for " +
                $"{singleFileProcessRequest.FilePath} processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            await outputService.MarkProcessRunFileCompleteAsync(processRunFile);

            ConsoleHelper.WriteLine(
                $"INFO - {nameof(FileProcessSingleProcessSingleService)} - Attempted marking batch as completed (if completed) " +
                $"processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            processRun = await outputService.MarkProcessRunCompleteIfCompleteAsync(processRun);

            var processRunCompleted = processRun is { EndDateTimeUtc: not null }
                && processRun.EndDateTimeUtc > DateTime.MinValue;

            if (processRunCompleted)
            {
                await AddCompleteProcessRunDataAsync(
                    processRun,
                    naldLicenceStatusData,
                    naldData,
                    allDmsData);
            }

            ConsoleHelper.WriteLine(
                $"INFO - {nameof(FileProcessSingleProcessSingleService)} - Successfully finished processing {singleFileProcessRequest.FilePath} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return true;
        }
        catch (Exception e)
        {
            ConsoleHelper.WriteLine(
                $" {e.Message} = {e}, ERROR - {nameof(FileProcessSingleProcessSingleService)} - Exception on processing {singleFileProcessRequest.FilePath} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

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
        ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessSingleProcessSingleService)} - Started {pdfFilename} " +
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
                    $"INFO - {nameof(FileProcessSingleProcessSingleService)} - Saved '{pdfFilename}' in {saveDuration}ms " +
                    $"at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }

            var duration = (DateTime.Now - dtStart).TotalMilliseconds;
            ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessSingleProcessSingleService)} - Finished ({pdfFilename} in " +
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
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleProcessSingleService)} - Skipped '{pdfFilename}' " +
                $"as too many pages");
            
            return [];
        }
        catch (TooManyImagesException)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(FileProcessSingleProcessSingleService)} - Skipped '{pdfFilename}' " +
                $"as too many images");
            
            return [];
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"FATAL ERROR - {nameof(FileProcessSingleProcessSingleService)} - {pdfFilename} threw " +
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
            $"INFO - {nameof(FileProcessSingleProcessSingleService)} - started processing all license sets at " +
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
        var allLicenceSets =
            await outputService.GetProcessRunLicenceSetsAsync(processRun.ProcessRunId);

        var licenceSetGroups = allLicenceSets
            .Values
            .Select(IReadOnlyList<LicenceSet> (licenceSet) => new List<LicenceSet> { licenceSet }.AsReadOnly())
            .ToList();
                
        await UpdateAndSaveLicenceSets(
            licenceSetGroups,
            naldLicenceStatusData,
            naldData,
            allDmsData,
            processRun);
               
        WalSchemaConverter.CalculateCombinedAggregates(allLicenceSets
            .Select(lsKvp => lsKvp.Value)
            .ToList());
        
        ConsoleHelper.WriteLine(
            $"INFO - {nameof(FileProcessSingleProcessSingleService)} - completed processing all license sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }
        
    // TODO - check the following methods are the same as they used to be
        
    private static ConcurrentDictionary<Guid, List<DmsFileIdInformation>> TranformDmsFileIdInformation(
        List<DmsFileIdInformation> dmsFileIdInformationList)
    {
        var dmsFileIdInformationDict = new ConcurrentDictionary<Guid, List<DmsFileIdInformation>>();

        foreach (var dmsFileIdInformation in dmsFileIdInformationList)
        {
            if (!dmsFileIdInformationDict.TryGetValue(dmsFileIdInformation.FileId, out var changeList))
            {
                changeList = [];
                dmsFileIdInformationDict.TryAdd(dmsFileIdInformation.FileId, changeList);
            }

            changeList.Add(dmsFileIdInformation);
        }

        return dmsFileIdInformationDict;
    }

    private static Dictionary<string, LicenceSet> GetLicenceSetsForLicenceSetIds(
        IReadOnlyList<LicenceSetReference> licenceSetIds,
        IReadOnlyList<LicenceSet> licenceSets)
    {
        var returnDict = new Dictionary<string, LicenceSet>();

        foreach (var licenceSet in licenceSets)
        {
            if (licenceSetIds.All(lsi => lsi.LicenceSetId != licenceSet.LicenceSetId))
            {
                continue;
            }

            returnDict.TryAdd(licenceSet.LicenceSetId, licenceSet);
        }

        return returnDict;
    }

    private async Task<NaldDataCollection> GetNaldDataAsync(short? regionCode)
    {
        const int take = 100000;
        var allNaldData = new NaldDataCollection
        {
            AbstractionAndImpoundmentLicences = [],
            AbstractionLicencePoints = [],
            AbstractionLicencePurposes = [],
            AbstractionLicenceQuantities = [],
            AbstractionLicences = [],
            AbstractionLicenceVersions = []
        };

        var allNaldDataPartial = new NaldDataCollection();
        var loopIdx = 0;

        while (loopIdx == 0
               || allNaldDataPartial.AbstractionAndImpoundmentLicences!.Count == take
               || allNaldDataPartial.AbstractionLicencePoints!.Count == take
               || allNaldDataPartial.AbstractionLicencePurposes!.Count == take
               || allNaldDataPartial.AbstractionLicenceQuantities!.Count == take
               || allNaldDataPartial.AbstractionLicences!.Count == take
               || allNaldDataPartial.AbstractionLicenceVersions!.Count == take)
        {
            var skip = take * loopIdx++;
                
            allNaldDataPartial = await cacheService.GetNaldDataAsync(regionCode, false, skip, take);
            allNaldData.AbstractionAndImpoundmentLicences!.AddRange(allNaldDataPartial.AbstractionAndImpoundmentLicences!);
            allNaldData.AbstractionLicencePoints!.AddRange(allNaldDataPartial.AbstractionLicencePoints!);
            allNaldData.AbstractionLicencePurposes!.AddRange(allNaldDataPartial.AbstractionLicencePurposes!);
            allNaldData.AbstractionLicenceQuantities!.AddRange(allNaldDataPartial.AbstractionLicenceQuantities!);
            allNaldData.AbstractionLicences!.AddRange(allNaldDataPartial.AbstractionLicences!);
            allNaldData.AbstractionLicenceVersions!.AddRange(allNaldDataPartial.AbstractionLicenceVersions!);
        }
        
        return allNaldData;
    }

    private async Task UpdateAndSaveLicenceSets(
        List<IReadOnlyList<LicenceSet>> licenceSetGroups,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> allDmsData,
        ProcessRun processRun)
    {
        var allLicenceSets = WalSchemaConverter.AddAdditionalLicenceSets(
            licenceSetGroups,
            naldLicenceStatusData,
            naldData,
            allDmsData);

        var savedLicenceNumbers = new Dictionary<string, int>();
        var savedLicenceFilenames = new Dictionary<string, int>();
        var notFoundSavedLicenceNumbers = new Dictionary<string, int>();

        var savedLicenceSetIds = new HashSet<string>();

        foreach (var licenceSetGroup in licenceSetGroups)
        {
            if (licenceSetGroup.Count == 0)
            {
                continue;
            }

            foreach (var licenceSetLoop in licenceSetGroup)
            {
                foreach (var licenceLoop in licenceSetLoop.Licences)
                {
                    var filename = licenceLoop.Filename;

                    if (licenceLoop.LicenceNumber?.Value != null
                        && (!savedLicenceNumbers.TryGetValue(
                                licenceLoop.LicenceNumber.Value, out _)
                            || (licenceLoop.Status == LicenceStatus.Ok &&
                                notFoundSavedLicenceNumbers.TryGetValue(licenceLoop.LicenceNumber.Value, out _))))
                    {
                        int loopLicenceId;
                        var savedVersionIsStatusNotFound =
                            notFoundSavedLicenceNumbers.TryGetValue(licenceLoop.LicenceNumber.Value,
                                out var existingLicenceId);

                        if (savedVersionIsStatusNotFound && licenceLoop.Status == LicenceStatus.Ok)
                        {
                            await outputService.UpdateLicenceAsync(
                                licenceLoop,
                                existingLicenceId,
                                processRun.ProcessRunId);

                            loopLicenceId = existingLicenceId;
                        }
                        else
                        {
                            loopLicenceId = await outputService.SaveLicenceAsync(
                                licenceLoop,
                                processRun.ProcessRunId);
                        }

                        savedLicenceNumbers.TryAdd(licenceLoop.LicenceNumber.Value, loopLicenceId);

                        if (!string.IsNullOrWhiteSpace(filename))
                        {
                            savedLicenceFilenames.TryAdd(filename, loopLicenceId);
                        }

                        if (licenceLoop.Status == LicenceStatus.NotFound)
                        {
                            notFoundSavedLicenceNumbers.TryAdd(licenceLoop.LicenceNumber.Value, loopLicenceId);
                        }
                        else
                        {
                            notFoundSavedLicenceNumbers.Remove(licenceLoop.LicenceNumber.Value);
                        }

                        licenceLoop.NoneSchemaData["licenceId"] = loopLicenceId;
                    }
                    else if (licenceLoop.LicenceNumber == null
                             && !string.IsNullOrEmpty(filename)
                             && !savedLicenceFilenames.TryGetValue(filename, out _))
                    {
                        var loopLicenceId = await outputService.SaveLicenceAsync(
                            licenceLoop,

                            processRun.ProcessRunId);

                        savedLicenceFilenames.Add(filename, loopLicenceId);
                        licenceLoop.NoneSchemaData.Add("licenceId", loopLicenceId);
                    }

                    var licenceSetsLoop = GetLicenceSetsForLicenceSetIds(
                        licenceLoop.LicenceSets,
                        allLicenceSets);

                    var newLicenceSetsLoop = new Dictionary<string, LicenceSet>();

                    foreach (var kvp in licenceSetsLoop.Where(kvp => !savedLicenceSetIds.Contains(kvp.Key)))
                    {
                        newLicenceSetsLoop.Add(kvp.Key, kvp.Value);
                        savedLicenceSetIds.Add(kvp.Key);
                    }

                    await outputService.SaveLicenceSetsAsync(
                        newLicenceSetsLoop,
                        licenceLoop.DmsFileId,
                        processRun.ProcessRunId);
                }
            }
        }
    }
}