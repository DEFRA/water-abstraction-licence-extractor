using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;

namespace WRADI.Services.ProcessFile.Implementations;

public class FileProcessSingleService(
    FileProcessAppSettings settings,
    ICacheService cacheService,
    IOutputService outputService,
    IFileService fileService,
    List<IPdfDataExtractorService> pdfDataExtractors)
    : IScrapeFileService
{
    private ConcurrentDictionary<Guid, List<DmsFileIdInformation>> TranformDmsFileIdInformation(
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
    
    private async Task<List<LicenceSet>> ScrapeSingleDocumentAsync(
        string pdfFilename,
        NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData,
        Lock extractorLock,
        LookupConfiguration lookupConfig,
        DmsFileData dmsDataForFile,
        ProcessRun processRun)
    {
        var filenameNoExtension = FileHelper.GetFilenameWithoutExtension(pdfFilename);

        var dtStart = DateTime.Now;
        ConsoleHelper.WriteLine($"INFO - OrchestrateFileProcessService - Started {filenameNoExtension} at {dtStart:yyyy-MM-dd HH:mm:ss}");

        IPdfDataExtractorService pdfDataExtractor;

        lock (extractorLock)
        {
            pdfDataExtractor = pdfDataExtractors.First(x => !x.InUse);
            pdfDataExtractor.InUse = true;
        }

        try
        {
            var previouslyParsedFiles = new List<string>
            {
                pdfFilename
            };

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
                var saveTasks = matchesFull.Matches
                    .Select(match => outputService.SaveMatchAsync(
                        matchResultId,
                        match.MatchedLabel?.Name,
                        match.LabelGroupName,
                        match))
                    .ToList();

                await Task.WhenAll(saveTasks);

                var saveDuration = (DateTime.Now - dtStartSaveMatches).TotalMilliseconds;

                if (saveDuration >= 1000)
                {
                    ConsoleHelper.WriteLine(
                        $"INFO - OrchestrateFileProcessService - Saved ({pdfFilename} in {saveDuration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                }
            }

            var duration = (DateTime.Now - dtStart).TotalMilliseconds;
            ConsoleHelper.WriteLine($"INFO - OrchestrateFileProcessService - Finished ({pdfFilename} in {duration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
                matchesFull,
                naldLicenceStatusData,
                naldData,
                pdfDataExtractor,
                processRun.ProcessRunId,
                lookupConfig,
                dmsDataForFile);

            return licenceSets;
        }
        finally
        {
            pdfDataExtractor.InUse = false;
        }
    }
    
    private async
        Task<(Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers, Dictionary<string, DmsFileData>
        LicenceNumbersWithFilenames)>
    GetFilesAndMappingFromLicenceFinderResultsAsync(string filePath)
{
    var filenamesWithLicenceNumbers = new Dictionary<string, DmsFileData>();
    var licenceNumbersWithFilenames = new Dictionary<string, DmsFileData>();

    var allDestinationFilenames = new List<string> { filePath };

    var lowercaseFilesInFolder = allDestinationFilenames.Select(f => f.ToLower()).ToHashSet();
    var licenceFinderResults = await cacheService.GetLicenceFinderResultsAsync(0, int.MaxValue);

    foreach (var licenceFinderResult in licenceFinderResults)
    {
        if (licenceFinderResult.FileId == null)
        {
            continue;
        }
        
        var destinationFileName = $"{licenceFinderResult.PermitNumber.ToLower()}__{licenceFinderResult.FileId!.ToLower()}.pdf";
        
        if (!lowercaseFilesInFolder.Contains(destinationFileName))
        {
            continue;
        }

        // Fix casing
        destinationFileName = allDestinationFilenames.First(fname =>
            fname.Equals(destinationFileName, StringComparison.CurrentCultureIgnoreCase));
        
        var regionId = RegionHelper.GetRegionId(licenceFinderResult.Region);
        
        var dmsFileData = new DmsFileData
        {
            DestinationFileName = destinationFileName,
            NaldLicenceRef = licenceFinderResult.LicenseNumber,
            PermitNumber = licenceFinderResult.PermitNumber,
            DmsPath = licenceFinderResult.FileUrl,
            StrippedLicenceNumber = FormattingHelper.StripForComparison(
                licenceFinderResult.LicenseNumber,
                regionId)!,
            FileId = Guid.Parse(licenceFinderResult.FileId!),
            RegionId = regionId
        };

        filenamesWithLicenceNumbers.Add(destinationFileName, dmsFileData);
        licenceNumbersWithFilenames.TryAdd(dmsFileData.StrippedLicenceNumber, dmsFileData);
    }
    
    return (
        filenamesWithLicenceNumbers,
        licenceNumbersWithFilenames
    );
}
    
    private async Task<
            (Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers,
            Dictionary<string, DmsFileData> LicenceNumbersWithFilenames)>
        GetDmsSingleFilesAndMappingAsync(string singleFilePath, string dmsReportPath, bool getFromFile)
    {
        
        (Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers, Dictionary<string, DmsFileData>
            LicenceNumbersWithFilenames) filesAndMapping;
    
        if (getFromFile)
        {
            filesAndMapping = await GetSingleFileAndMappingFromExcelDownloadInfoFileAsync(singleFilePath, dmsReportPath);
        }
        else
        {
            filesAndMapping = await GetFilesAndMappingFromLicenceFinderResultsAsync(singleFilePath);
        }

        filesAndMapping.FilenamesWithLicenceNumbers = filesAndMapping.FilenamesWithLicenceNumbers
            .OrderBy(filePath => filePath.Key)
            .Skip(0)
            .Take(1000)
            .ToDictionary(filePath => filePath.Key, filePath => filePath.Value);

        return filesAndMapping;
    }
    
    
    private async Task<
            (Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers,
            Dictionary<string, DmsFileData> LicenceNumbersWithFilenames)>
        GetSingleFileAndMappingFromExcelDownloadInfoFileAsync(string filePath, string dmsReportPath)
    {
        var filenamesWithLicenceNumbers = new Dictionary<string, DmsFileData>();
        var licenceNumbersWithFilenames = new Dictionary<string, DmsFileData>();

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var filesInFolder = new List<string> { filePath };

        await using var stream = File.Open(dmsReportPath, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = true
            }
        });

        if (dataSet.Tables.Count == 0)
        {
            throw new InvalidOperationException("No worksheets found in the Excel file.");
        }

        var dataTable = dataSet.Tables[0];

        if (dataTable.Rows.Count == 0)
        {
            throw new InvalidOperationException("Excel file is empty.");
        }

        foreach (DataRow row in dataTable.Rows)
        {
            var permitNumberField = row["Permit Number"];
            string permitNumber;

            if (permitNumberField is string permitNumberValue)
            {
                permitNumber = permitNumberValue;
            }
            else
            {
                permitNumber = ((double)permitNumberField).ToString(CultureInfo.InvariantCulture);
            }

            var dmsPath = (string)row["File URL"];
            var destinationFileName = (string)row[1];

            if (!filesInFolder.Contains(destinationFileName))
            {
                continue;
            }

            var naldLicenceRef = (string)row["License Number"];

            var filenameParts = destinationFileName.Split("__");
            var fileId = filenameParts.Length >= 3 ? Guid.Parse(filenameParts[1]) : throw new Exception("Filename format was incorrect");;

            var dmsFileData = new DmsFileData
            {
                DestinationFileName = destinationFileName,
                NaldLicenceRef = naldLicenceRef,
                PermitNumber = permitNumber,
                DmsPath = dmsPath,
                StrippedLicenceNumber = FormattingHelper.StripForComparison(naldLicenceRef, -1)!,
                FileId = fileId,
                RegionId = GeneralConstants.GenericRegionCode
            };

            filenamesWithLicenceNumbers.Add(destinationFileName, dmsFileData);
            licenceNumbersWithFilenames.Add(dmsFileData.StrippedLicenceNumber, dmsFileData);
        }

        return (
            filenamesWithLicenceNumbers,
            licenceNumbersWithFilenames
        );
    }

    public async Task<bool> RunAsync(SingleFileProcessRequest singleFileProcessRequest,
        CancellationToken cancellationToken = default)
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

        const short regionCode = 3;

        var naldDataTask = GetNaldDataAsync(null, cacheService);
        var firstNamesTask = CompanyName.GetFirstNamesCsvFromFileAsync();
        var dmsFileIdInformationListTask = cacheService.GetDmsFileIdInformationAsync();
        var naldLicenceStatusDataTask = cacheService.GetNaldLicenceStatusDataAsync(regionCode);
        var naldLicenceStatusData = await naldLicenceStatusDataTask;
        var firstNamesCsv = await firstNamesTask;
        var allNaldData = await naldDataTask;

        var (dmsFilesToProcess, allDmsData) =
            await GetDmsSingleFilesAndMappingAsync(singleFileProcessRequest.FilePath, settings.FileMappingPath, false);

        LicenceNumber.Instance = new LicenceNumber(allNaldData.AbstractionAndImpoundmentLicences!);

        var naldLinkedLicenceHelper = await NaldLinkedLicenceHelper.CreateAsync(
            cacheService);

        var naldData = ExternalDataHelper.TransformNaldData(
            allNaldData,
            allDmsData);

        var dmsFileIdInformationDict = TranformDmsFileIdInformation(
            await dmsFileIdInformationListTask);

        var maxConcurrentScrapers = settings.ConcurrentCount;
        var minimumToFreeUp = maxConcurrentScrapers / 3;

        var extractorLock = new Lock();

        var lookupConfig = new LookupConfiguration(
            WalLabelConfiguration.GetLabels(),
            allDmsData,
            dmsFileIdInformationDict,
            firstNamesCsv,
            fileService,
            cacheService,
            regionCode,
            naldLinkedLicenceHelper: naldLinkedLicenceHelper);

        var processRuns = await outputService.GetAllProcessRunsAsync();

        var processRun = processRuns.First(pr => pr.ProcessRunId == singleFileProcessRequest.ProcessRunId);

        ConsoleHelper.WriteLine(
            $"INFO - SingleFileProcessService - Start file to output for {singleFileProcessRequest.FilePath} processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        var processRunFile = await outputService.AddProcessRunFileAsync(new ProcessRunFile
        {
            ProcessRunId = processRun.ProcessRunId,
            FileName = singleFileProcessRequest.FilePath
        });


        try
        {
            var licenceSetGroups = new List<IReadOnlyList<LicenceSet>>();

            var result = await ScrapeSingleDocumentAsync(
                singleFileProcessRequest.FilePath,
                naldLicenceStatusData,
                naldData,
                extractorLock,
                lookupConfig,
                dmsFilesToProcess.FirstOrDefault().Value,
                processRun);

            licenceSetGroups.Add(result);

            foreach (var pdfDataExtractor in pdfDataExtractors)
            {
                pdfDataExtractor.Dispose();
            }
            
            await ProcessAllLicenceSets(licenceSetGroups, naldLicenceStatusData, naldData, allDmsData, regionCode, processRun);

            ConsoleHelper.WriteLine(
                $"INFO - SingleFileProcessService - Completing process run file for {singleFileProcessRequest.FilePath} processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await outputService.CompleteProcessRunFileAsync(processRunFile);

            ConsoleHelper.WriteLine(
                $"INFO - SingleFileProcessService - Attempted marking batch as completed processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            var latestProcessRun = await outputService.MarkProcessRunCompleteAsync(processRun);

            if (latestProcessRun is { EndDateTimeUtc: not null} && latestProcessRun.EndDateTimeUtc > DateTime.MinValue)
            {
                ConsoleHelper.WriteLine(
                    $"INFO - SingleFileProcessService - started processing all license sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                var licenceSets =
                    await outputService.GetProcessRunLicenceSetsAsync(processRun.ProcessRunId);

                var processRunLicenceSetGroups = licenceSets
                    .Values
                    .Select(x => (IReadOnlyList<LicenceSet>)new List<LicenceSet> { x }.AsReadOnly())
                    .ToList();
                
                var outputLines = await ProcessAllLicenceSets(processRunLicenceSetGroups, naldLicenceStatusData, naldData, allDmsData, regionCode, processRun);
               
                ConsoleHelper.WriteLine(
                $"INFO - SingleFileProcessService - completed processing all license sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                
                var saveListsToFile = false;
                if (saveListsToFile)
                {
                    var allLatestLicenceSectionVerificationsTask =
                        outputService.GetLatestLicenceSectionVerificationsAsync();
                    var allLatestLicenceSectionVerifications =
                        (await allLatestLicenceSectionVerificationsTask).ToList();
                    
                    ConsoleHelper.WriteLine(
                        $"INFO - SingleFileProcessService - Saved licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    await JsOutputHelper.ToListDataAsync(
                        outputLines,
                        outputService,
                        processRun,
                        saveListsToFile,
                        allLatestLicenceSectionVerifications);
                } 
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
    }
    
    async Task<NaldDataCollection> GetNaldDataAsync(short? regionCode, ICacheService cacheService)
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

    private async Task<List<IntermediateOutputLicence>> ProcessAllLicenceSets(List<IReadOnlyList<LicenceSet>> licenceSetGroups, NaldLicenceStatusData naldLicenceStatusData,
        Dictionary<string, List<NaldData>> naldData, Dictionary<string, DmsFileData> allDmsData, short regionCode, ProcessRun processRun)
    {
        var allLicenceSets = WalSchemaConverter.AddAdditionalLicenceSets(
            licenceSetGroups,
            naldLicenceStatusData,
            naldData,
            allDmsData);

        var outputLines = new List<IntermediateOutputLicence>();

        var fileNumber = 1;
        var completeNumber = 1;

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

            var licence = licenceSetGroup[0].Licences.First();

            var licenceSets = GetLicenceSetsForLicenceSetIds(
                licence.LicenceSets,
                allLicenceSets);

            var outputLine = JsOutputHelper.ToOutputLine(
                licence,
                DateTime.Now,
                completeNumber++,
                fileNumber++,
                licenceSets);

            outputLines.Add(outputLine);
        }

        return outputLines;
    }
}