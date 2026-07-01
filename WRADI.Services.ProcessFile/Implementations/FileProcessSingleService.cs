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
    : IScrapeFileService
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

        var naldDataTask = GetNaldDataAsync(null);
        var firstNamesTask = CompanyName.GetFirstNamesCsvFromFileAsync();
        var dmsFileIdInformationListTask = cacheService.GetDmsFileIdInformationAsync();
        var naldLicenceStatusDataTask = cacheService.GetNaldLicenceStatusDataAsync();
        var naldLicenceStatusData = await naldLicenceStatusDataTask;
        var firstNamesCsv = await firstNamesTask;
        var allNaldData = await naldDataTask;

        var (dmsFilesToProcess, allDmsData) =
            await DmsHelper.GetDmsFilesAndMappingAsync(
                fileService,
                string.Empty,
                false,
                cacheService);

        LicenceNumber.Instance = new LicenceNumber(allNaldData.AbstractionAndImpoundmentLicences!);

        var naldLinkedLicenceHelper = await NaldLinkedLicenceHelper.CreateAsync(
            cacheService);

        var naldData = ExternalDataHelper.TransformNaldData(
            allNaldData,
            allDmsData);

        var dmsFileIdInformationDict = TranformDmsFileIdInformation(
            await dmsFileIdInformationListTask);

        const int unsetRegionCode = GeneralConstants.GenericRegionCode;
        
        var lookupConfig = new LookupConfiguration(
            WalLabelConfiguration.GetLabels(),
            allDmsData,
            dmsFileIdInformationDict,
            firstNamesCsv,
            fileService,
            cacheService,
            unsetRegionCode,
            naldLinkedLicenceHelper: naldLinkedLicenceHelper);

        var processRuns = await outputService.GetAllProcessRunsAsync();
        var processRun = processRuns.Single(pr => pr.ProcessRunId == singleFileProcessRequest.ProcessRunId);

        ConsoleHelper.WriteLine(
            $"INFO - SingleFileProcessService - Start file to output for {singleFileProcessRequest.FilePath} " +
            $"processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        
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

            await ProcessAllLicenceSets(
                [licenceSet],
                naldLicenceStatusData,
                naldData,
                allDmsData,
                processRun);

            ConsoleHelper.WriteLine(
                $"INFO - SingleFileProcessService - Marking process run file as complete for " +
                $"{singleFileProcessRequest.FilePath} processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            await outputService.MarkProcessRunFileCompleteAsync(processRunFile);

            ConsoleHelper.WriteLine(
                $"INFO - SingleFileProcessService - Attempted marking batch as completed (if completed) " +
                $"processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            processRun = await outputService.MarkProcessRunCompleteIfCompleteAsync(processRun);

            var processRunCompleted = processRun is { EndDateTimeUtc: not null }
                && processRun.EndDateTimeUtc > DateTime.MinValue;

            if (processRunCompleted)
            {
                await RunPostProcessingAsync(
                    processRun,
                    naldLicenceStatusData,
                    naldData,
                    allDmsData);
            }

            ConsoleHelper.WriteLine(
                $"INFO - SingleFileProcessService - Successfully finished processing {singleFileProcessRequest.FilePath} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return true;
        }
        catch (Exception e)
        {
            ConsoleHelper.WriteLine(
                $" {e.Message} = {e}, ERROR - SingleFileProcessService - Exception on processing {singleFileProcessRequest.FilePath} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

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
        ConsoleHelper.WriteLine($"INFO - OrchestrateFileProcessService - Started {pdfFilename} " +
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
                    .Select(match => (matchResultId, match.MatchedLabel?.Name, match.LabelGroupName, match))
                    .ToList();

                await outputService.SaveMatchesAsync(matches);

                var saveDuration = (DateTime.Now - dtStartSaveMatches).TotalMilliseconds;
                ConsoleHelper.WriteLine(
                    $"INFO - WALE.Cmd - Saved '{pdfFilename}' in {saveDuration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }

            var duration = (DateTime.Now - dtStart).TotalMilliseconds;
            ConsoleHelper.WriteLine($"INFO - OrchestrateFileProcessService - Finished ({pdfFilename} in " +
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
            ConsoleHelper.WriteLine($"WARNING - WALE.Cmd - Skipped '{pdfFilename}' as too many pages");
            return [];
        }
        catch (TooManyImagesException)
        {
            ConsoleHelper.WriteLine($"WARNING - WALE.Cmd - Skipped '{pdfFilename}' as too many images");
            return [];
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"FATAL ERROR - WALE.Cmd - {pdfFilename} threw fatal error - {ex}");
            return [];
        }
        finally
        {
            pdfDataExtractor.InUse = false;
        }
    }
    
        private async Task RunPostProcessingAsync(
        ProcessRun processRun,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> allDmsData)
    {
        ConsoleHelper.WriteLine(
            $"INFO - SingleFileProcessService - started processing all license sets at " +
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
        var licenceSets =
            await outputService.GetProcessRunLicenceSetsAsync(processRun.ProcessRunId);

        var licenceSetGroups = licenceSets
            .Values
            .Select(IReadOnlyList<LicenceSet> (licenceSet) => new List<LicenceSet> { licenceSet }.AsReadOnly())
            .ToList();
                
        await ProcessAllLicenceSets(
            licenceSetGroups,
            naldLicenceStatusData,
            naldData,
            allDmsData,
            processRun);
               
        ConsoleHelper.WriteLine(
            $"INFO - SingleFileProcessService - completed processing all license sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
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

    private Dictionary<string, LicenceSet> GetLicenceSetsForLicenceSetIds(
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
    
    async Task<NaldDataCollection> GetNaldDataAsync(short? regionCode)
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

    private async Task ProcessAllLicenceSets(
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