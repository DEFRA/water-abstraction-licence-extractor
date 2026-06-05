using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Net;
using System.Text;
using ExcelDataReader;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.AwsS3;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WaterAbstractionLicenseExtractor.Cmd;

await ProgramAsync();
return;

async Task ProgramAsync()
{
    ConsoleHelper.WriteLine("INFO - WALE.Cmd - Started");
    var services = ConfigureServices();

    var cacheService = services.CacheService!;
    var outputService = services.OutputService!;
    var outputFolder = services.OutputFolder!;

    var pdfDataExtractors = services.PdfDataExtractorServices!;
    var maxConcurrentScrapers = services.MaxConcurrentScrapers;

    if (services.RefreshCache)
    {
        await cacheService.ClearCacheAsync();
    }

    await cacheService.SetupAsync();
    await outputService.SetupAsync();

    var naldDataTask = GetNaldDataAsync(null, cacheService);
    var firstNamesTask = CompanyName.GetFirstNamesCsvFromFileAsync();
    var dmsFileIdInformationListTask = cacheService.GetDmsFileIdInformationAsync();
    var naldLicenceStatusDataTask = cacheService.GetNaldLicenceStatusDataAsync();

    var dtStartGetDms = DateTime.Now;
    ConsoleHelper.WriteLine("INFO - WALE.Cmd - Getting DMS files to process");
    
    var (dmsFilesToProcess, allDmsData) =
        await GetDmsFilesAndMappingAsync(
            services.FileService!,
            services.DmsReportPath!,
            false,
            cacheService);

    var saveDuration = (DateTime.Now - dtStartGetDms).TotalMilliseconds;

    ConsoleHelper.WriteLine(
        $"INFO - WALE.Cmd - Got {dmsFilesToProcess.Count} DMS files to process in {saveDuration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    
    var processRunTask = outputService.StartProcessRunAsync(new ProcessRun
    {
        Description = $"Run using {services.FileService!.FolderPath}",
        StartDateTimeUtc = DateTime.UtcNow,
        NumberOfFiles = dmsFilesToProcess.Count
    });

    var naldLicenceStatusData  = await naldLicenceStatusDataTask;
    var firstNamesCsv = await firstNamesTask;
    var processRun = await processRunTask;

    var allNaldData =  await naldDataTask;
    
    LicenceNumber.Instance = new LicenceNumber(allNaldData.AbstractionAndImpoundmentLicences!);

    var naldLinkedLicenceHelper = await NaldLinkedLicenceHelper.CreateAsync(cacheService);
    var naldData = ExternalDataHelper.TransformNaldData(allNaldData, allDmsData);

    var dmsFileIdInformationDict = TranformDmsFileIdInformation(
        await dmsFileIdInformationListTask);

    const int unsetRegionCode = GeneralConstants.GenericRegionCode;
    var licenceSetGroups = new List<IReadOnlyList<LicenceSet>>();
    
    var lookupConfig = new LookupConfiguration(
        WalLabelConfiguration.GetLabels(),
        allDmsData,
        dmsFileIdInformationDict,
        firstNamesCsv,
        services.FileService,
        services.CacheService!,
        unsetRegionCode,
        naldLinkedLicenceHelper: naldLinkedLicenceHelper);
    
    try
    {
        var scrapingTasks = new List<Task<List<LicenceSet>>>();
        var processCount = 1;
        var minimumToFreeUp = maxConcurrentScrapers / 3;

        var extractorLock = new Lock();
        
        foreach (var (filePath, dmsDataForFile) in dmsFilesToProcess)
        {
            if (services.DelayPerProcessMs > 0)
            {
                await Task.Delay(services.DelayPerProcessMs);
            }

            scrapingTasks.Add(
                ScrapeDocumentAsync(
                    filePath,
                    processCount++,
                    processRun.NumberOfFiles,
                    naldLicenceStatusData,
                    naldData,
                    outputService,
                    pdfDataExtractors,
                    processRun,
                    extractorLock,
                    lookupConfig,
                    dmsDataForFile));

            if (scrapingTasks.Count != maxConcurrentScrapers)
            {
                continue;
            }

            while (scrapingTasks.Count >= maxConcurrentScrapers - minimumToFreeUp)
            {
                await Task.WhenAny(scrapingTasks);
                var toRemoveList = new List<Task<List<LicenceSet>>>();
                
                foreach (var scrapingTask in scrapingTasks)
                {
                    if (!scrapingTask.IsCompleted)
                    {
                        continue;
                    }

                    var scrapeResultLicenceSets = scrapingTask.Result;

                    if (scrapeResultLicenceSets.Count != 0)
                    {
                        licenceSetGroups.Add(scrapeResultLicenceSets);
                    }
                    
                    toRemoveList.Add(scrapingTask);
                }

                foreach (var toRemoveItem in toRemoveList)
                {
                    scrapingTasks.Remove(toRemoveItem);
                }
            }
        }

        foreach (var scrapingTask in scrapingTasks)
        {
            var scrapeResultLicenceSets = await scrapingTask;

            if (scrapeResultLicenceSets.Count == 0)
            {
                continue;
            }

            licenceSetGroups.Add(scrapeResultLicenceSets);
        }

        foreach (var pdfDataExtractor in pdfDataExtractors)
        {
            pdfDataExtractor.Dispose();
        }
    }
    catch (Exception e)
    {
        ConsoleHelper.WriteLine($"ERROR - WALE.Cmd - Error during scraping: {e}");
        throw;
    }

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - All scraped at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

    var allLicenceSets = WalSchemaConverter.AddAdditionalLicenceSets(
        licenceSetGroups,
        naldLicenceStatusData,
        naldData,
        allDmsData);

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Converted into all licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

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
            // This shouldn't happen
            ConsoleHelper.WriteLine("WARNING - WALE.Cmd - Empty licence set group found");
            continue;
        }

        foreach (var licenceSetLoop in licenceSetGroup)
        {
            foreach (var licenceLoop in licenceSetLoop.Licences)
            {
                var filename = licenceLoop.Filename;

                if (licenceLoop.LicenceNumber != null
                    && (!savedLicenceNumbers.TryGetValue(licenceLoop.LicenceNumber?.Value!, out _)
                        || (licenceLoop.Status == LicenceStatus.Ok &&
                            notFoundSavedLicenceNumbers.TryGetValue(licenceLoop.LicenceNumber?.Value!, out _))))
                {
                    int loopLicenceId;
                    var savedVersionIsStatusNotFound =
                        notFoundSavedLicenceNumbers.TryGetValue(licenceLoop.LicenceNumber?.Value!, out var existingLicenceId);

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

                    savedLicenceNumbers.TryAdd(licenceLoop.LicenceNumber?.Value!, loopLicenceId);

                    if (!string.IsNullOrWhiteSpace(filename))
                    {
                        savedLicenceFilenames.TryAdd(filename, loopLicenceId);
                    }

                    if (licenceLoop.Status == LicenceStatus.NotFound)
                    {
                        notFoundSavedLicenceNumbers.TryAdd(licenceLoop.LicenceNumber?.Value!, loopLicenceId);
                    }
                    else
                    {
                        notFoundSavedLicenceNumbers.Remove(licenceLoop.LicenceNumber?.Value!);
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

                foreach (var licenceSet in newLicenceSetsLoop)
                {
                    // Not batched as we get a 413 on the server
                    await outputService.SaveLicenceSetAsync(
                        licenceSet.Value,
                        licenceLoop.DmsFileId,
                        processRun.ProcessRunId);   
                }
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

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Saved licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    var saveListsToFile = false;

    if (saveListsToFile)
    {
        // The following is just for some reports and charts - saves to filestream
        await JsOutputHelper.SaveListDataAsync(
            outputLines,
            outputFolder,
            outputService,
            services.RegenerateMappingJson,
            processRun,
            saveListsToFile);
    }

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Saved list at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    await outputService.FinishProcessRunAsync(processRun);

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Finished processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    ConsoleHelper.WriteLine(
        $"INFO - WALE.Cmd - Finished all in {(processRun.EndDateTimeUtc!.Value - processRun.StartDateTimeUtc!.Value).TotalSeconds} seconds - process run id {processRun.ProcessRunId}");
}

async Task<NaldDataCollection> GetNaldDataAsync(short? regionCode, ICacheService cacheService)
{
    const int take = 10_000;
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

ConcurrentDictionary<Guid, List<DmsFileIdInformation>> TranformDmsFileIdInformation(
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

Dictionary<string, LicenceSet> GetLicenceSetsForLicenceSetIds(
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

ConfiguredServices ConfigureServices()
{
    var maxConcurrentScrapers = int.Parse(Environment.GetEnvironmentVariable("ConcurrentCount")
                                          ?? throw new NullReferenceException("ConcurrentCount"));
    var regenerateMappingJson = bool.Parse(Environment.GetEnvironmentVariable("REGENERATE_MAPPING_JSON")
                                           ?? throw new NullReferenceException("REGENERATE_MAPPING_JSON"));
    var loadAiJs = bool.Parse(Environment.GetEnvironmentVariable("LOAD_AI_JS")
                              ?? throw new NullReferenceException("LOAD_AI_JS"));
    var refreshCache = bool.Parse(Environment.GetEnvironmentVariable("RefreshCache")
                                  ?? throw new NullReferenceException("RefreshCache"));
    var reportTemplatePath = Environment.GetEnvironmentVariable("ReportTemplatePath")
                             ?? throw new NullReferenceException("ReportTemplatePath");
    var outputFolder = Environment.GetEnvironmentVariable("OutputFolder")
                       ?? throw new NullReferenceException("OutputFolder");
    var listDataPath = Environment.GetEnvironmentVariable("ListDataPath")
                       ?? throw new NullReferenceException("ListDataPath");
    var processRunsDataPath = Environment.GetEnvironmentVariable("ProcessRunsDataPath")
                              ?? throw new NullReferenceException("ProcessRunsDataPath");
    var internalDataPath = Environment.GetEnvironmentVariable("InternalDataPath")
                           ?? throw new NullReferenceException("InternalDataPath");
    var licenceDataPath = Environment.GetEnvironmentVariable("LicenceDataPath")
                          ?? throw new NullReferenceException("LicenceDataPath");
    var licenceSetsDataPath = Environment.GetEnvironmentVariable("LicenceSetsDataPath")
                              ?? throw new NullReferenceException("LicenceSetsDataPath");
    var thumbnailImageDataPath = Environment.GetEnvironmentVariable("ThumbnailImageDataPath")
                                 ?? throw new NullReferenceException("ThumbnailImageDataPath");
    var fullImageDataPath = Environment.GetEnvironmentVariable("FullImageDataPath")
                            ?? throw new NullReferenceException("FullImageDataPath");
    var fileMappingPath = Environment.GetEnvironmentVariable("FileMappingPath")
                          ?? throw new NullReferenceException("FileMappingPath");
    var dotnetPath = Environment.GetEnvironmentVariable("DotnetPath")
                     ?? throw new NullReferenceException("DotnetPath");
    var tesseractExeName = Environment.GetEnvironmentVariable("TesseractExeName")
                           ?? throw new NullReferenceException("TesseractExeName");
    var tesseractExeDirectory = Environment.GetEnvironmentVariable("TesseractExeDirectory")
                                ?? throw new NullReferenceException("TesseractExeDirectory");
    var tessDataPrefix = Environment.GetEnvironmentVariable("TESSDATA_PREFIX")
                         ?? throw new NullReferenceException("TESSDATA_PREFIX");
    var apiBaseUrl = Environment.GetEnvironmentVariable("ApiBaseUrl")
                         ?? throw new NullReferenceException("ApiBaseUrl");

    var delayPerProcessMs = 1000;
    var httpClient = HttpHelper.GetResilientHttpClient(apiBaseUrl, 100, 30);
    
    var fileServiceType = "api";
    IFileService fileService;

    switch (fileServiceType)
    {
        case "api":
            fileService = new ApiFileService(httpClient);
            break;
        case "s3":
        {
            var accessKey = Environment.GetEnvironmentVariable("AwsS3AccessKey")
                ?? throw new NullReferenceException("AwsS3AccessKey");
            var secretKey = Environment.GetEnvironmentVariable("AwsS3SecretKey")
                ?? throw new NullReferenceException("AwsS3SecretKey");
            var regionName = Environment.GetEnvironmentVariable("AwsS3RegionName")
                ?? throw new NullReferenceException("AwsS3RegionName");
            var bucketName = Environment.GetEnvironmentVariable("AwsS3BucketName")
                ?? throw new NullReferenceException("AwsS3BucketName");
        
            fileService = new AwsS3FileService(
                regionName,
                bucketName,
                accessKey,
                secretKey,
                null);
            break;
        }
        default:
        {
            var pdfFolderPath = Environment.GetEnvironmentVariable("PdfFolderPath")
                ?? throw new NullReferenceException("PdfFolderPath");
        
            if (!pdfFolderPath.EndsWith('/'))
            {
                pdfFolderPath += "/";
            }
        
            fileService = new LocalFileService(pdfFolderPath);
            break;
        }
    }
    
    var cacheService = new ApiCacheService(httpClient);
    var outputService = new ApiOutputService(httpClient);

    var pdfPigDocumentService = new PdfPigNoOcrPdfDocumentService();
    var docnetAlternativeDocumentService = new DocnetNoOcrAlternativePdfDocumentService();
    
    var pdfDataExtractors = new List<IPdfDataExtractorService>();

    for (var idx = 0; idx < maxConcurrentScrapers; idx++)
    {
        var id = idx + 1;
        var pdfPigNoOcr = new PdfPigNoOcrDataExtractorService();

        var tesseractOcrSparse = new TesseractOcrDataExtractorService(
            tessDataPrefix,
            WALE.ProcessFile.Core.Enums.PageSegMode.SparseTextOsd,
            cacheService,
            outputService,
            dotnetPath,
            tesseractExeName,
            tesseractExeDirectory,
            id);

        var tesseractOcrDefault = new TesseractOcrDataExtractorService(
            tessDataPrefix,
            WALE.ProcessFile.Core.Enums.PageSegMode.Auto,
            cacheService,
            outputService,
            dotnetPath,
            tesseractExeName,
            tesseractExeDirectory,
            id);

        var azureAiServices = new AzureAiVisionOcrDataExtractorService(
            Environment.GetEnvironmentVariable("AzureAIVisionEndpoint")
            ?? throw new NullReferenceException("AzureAIVisionEndpoint"),
            Environment.GetEnvironmentVariable("AzureAIVisionKey")
            ?? throw new NullReferenceException("AzureAIVisionKey"),
            cacheService,
            outputService,
            id);

        var pdfDataExtractor = new PdfDataExtractorService(
            pdfPigNoOcr,
            [
                tesseractOcrSparse,
                tesseractOcrDefault,
                azureAiServices
            ],
            cacheService,
            outputService,
            pdfPigDocumentService,
            docnetAlternativeDocumentService,
            id);

        pdfDataExtractors.Add(pdfDataExtractor);
    }

    return new ConfiguredServices
    {
        CacheService = cacheService,
        OutputService = outputService,
        PdfDataExtractorServices = pdfDataExtractors,
        MaxConcurrentScrapers = maxConcurrentScrapers,
        OutputFolder = outputFolder,
        RegenerateMappingJson = regenerateMappingJson,
        FileService = fileService,
        ReportTemplatePath = reportTemplatePath,
        LoadAiJs = loadAiJs,
        ListDataPath = listDataPath,
        ProcessRunsDataPath = processRunsDataPath,
        InternalDataPath = internalDataPath,
        LicenceDataPath = licenceDataPath,
        LicenceSetsDataPath = licenceSetsDataPath,
        ThumbnailImageDataPath = thumbnailImageDataPath,
        FullImageDataPath = fullImageDataPath,
        RefreshCache = refreshCache,
        DmsReportPath = fileMappingPath,
        DelayPerProcessMs = delayPerProcessMs
    };
}

async Task<List<LicenceSet>> ScrapeDocumentAsync(
    string pdfFilename,
    int fileNumber,
    int totalNumber,
    NaldLicenceStatusData naldLicenceStatusData,
    Dictionary<string, List<NaldData>> naldData,
    IOutputService outputService,
    List<IPdfDataExtractorService> pdfDataExtractors,
    ProcessRun processRun,
    Lock extractorLock,
    LookupConfiguration lookupConfig,
    DmsFileData dmsDataForFile)
{
    var filenameNoExtension = FileHelper.GetFilenameWithoutExtension(pdfFilename);

    var dtStart = DateTime.Now;
    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Started {filenameNoExtension} ({fileNumber} of {totalNumber}) at {dtStart:yyyy-MM-dd HH:mm:ss}");

    IPdfDataExtractorService pdfDataExtractor;

    lock (extractorLock)
    {
        pdfDataExtractor = pdfDataExtractors.First(extractor => !extractor.InUse);
        pdfDataExtractor.InUse = true;
    }

    try
    {
        var previouslyParsedFiles = new List<string>
        {
            pdfFilename
        };

        lookupConfig = lookupConfig.Clone();
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
                $"INFO - WALE.Cmd - Saved ({fileNumber} of {totalNumber}) in {saveDuration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        var duration = (DateTime.Now - dtStart).TotalMilliseconds;
        ConsoleHelper.WriteLine(
            $"INFO - WALE.Cmd - Finished ({fileNumber} of {totalNumber}) in {duration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

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
    catch (TooManyPagesException)
    {
        ConsoleHelper.WriteLine($"WARNING - WALE.Cmd - Skipped ({fileNumber} of {totalNumber}) as too many pages");
        return [];
    }
    catch (TooManyImagesException)
    {
        ConsoleHelper.WriteLine($"WARNING - WALE.Cmd - Skipped ({fileNumber} of {totalNumber}) as too many pages");
        return [];
    }
    catch (Exception)
    {
        ConsoleHelper.WriteLine($"FATAL ERROR - WALE.Cmd - {pdfFilename} threw fatal error");
        return [];
    }
    finally
    {
        pdfDataExtractor.InUse = false;
    }
}

async Task<(Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers,
    Dictionary<string, DmsFileData> LicenceNumbersWithFilenames)>
    GetDmsFilesAndMappingAsync(
        IFileService fileService,
        string dmsReportPath,
        bool getFromFile,
        ICacheService cacheService)
{
    //var filesAndMapping = GetFilesAndMappingFromFolders(services.PdfFolderPath!);
    (Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers, Dictionary<string, DmsFileData>
        LicenceNumbersWithFilenames) filesAndMapping;
    
    if (getFromFile)
    {
        filesAndMapping = await GetFilesAndMappingFromExcelDownloadInfoFileAsync(
            fileService,
            dmsReportPath);
    }
    else
    {
        filesAndMapping = await GetFilesAndMappingFromLicenceFinderResultsAsync(
            fileService,
            cacheService);
    }
    
    /*filesAndMapping.FilepathsWithLicenceNumbers = filesAndMapping.FilepathsWithLicenceNumbers
        .Where(filePath => filePath.Key.Contains("22722086"))
        .ToDictionary(filePath => filePath.Key, k => k.Value);*/

    filesAndMapping.FilenamesWithLicenceNumbers = filesAndMapping.FilenamesWithLicenceNumbers
        .OrderBy(filePath => filePath.Key)
        //.Where(x => x.Key.Contains("12405035_")) // TODO This file is slow (3X slower then some others - work out why)
        //.Where(x => /*x.Key.Contains("12100063") || */ x.Key.Contains("12504175r01__bf7b7908-fa43-61ef-b29e-475502aa2f94"))
        .Where(x => x.Value.RegionId == 3) // North east
        //.Skip(155)
        //.Take(5)
        .ToDictionary(filePath => filePath.Key, filePath => filePath.Value);

    return filesAndMapping;
}

async Task<(Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers, Dictionary<string, DmsFileData>
        LicenceNumbersWithFilenames)>
    GetFilesAndMappingFromLicenceFinderResultsAsync(IFileService fileService, ICacheService cacheService)
{
    var filenamesWithLicenceNumbers = new Dictionary<string, DmsFileData>();
    var licenceNumbersWithFilenames = new Dictionary<string, DmsFileData>();

    var allDestinationFilenames = await fileService.GetAllFilesAsync();

    var lowercaseFilesInFolder = allDestinationFilenames.Select(f => f.ToLower()).ToHashSet();
    var licenceFinderResults = await cacheService.GetLicenceFinderResultsAsync();

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

async Task<(Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers, Dictionary<string, DmsFileData> LicenceNumbersWithFilenames)>
    GetFilesAndMappingFromExcelDownloadInfoFileAsync(
        IFileService fileService,
        string dmsReportPath)
{
    var filenamesWithLicenceNumbers = new Dictionary<string, DmsFileData>();
    var licenceNumbersWithFilenames = new Dictionary<string, DmsFileData>();

    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    var filesInFolder = await fileService.GetAllFilesAsync();

    await using (var stream = File.Open(dmsReportPath, FileMode.Open, FileAccess.Read))
    {
        using (var reader = ExcelReaderFactory.CreateReader(stream))
        {
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

                string destinationFileName;
                string dmsPath;
                Guid fileId;
                
                if (dataTable.Columns.Contains("Definitive URL"))
                {
                    dmsPath = (string)row["Definitive URL"];
                    destinationFileName = (string)row[1];
                    
                    var filenameParts = destinationFileName.Split("__");
                    fileId = filenameParts.Length >= 2
                        ? Guid.Parse(filenameParts[1])
                        : throw new Exception("Filename format was incorrect");
                }
                else
                {
                    var fileIdColumn = row["File Id"] != DBNull.Value ? (string)row["File Id"] : null;
                    
                    if (!Guid.TryParse(fileIdColumn, out fileId))
                    {
                        continue;
                    }
                    
                    dmsPath = (string)row["File URL"];
                    destinationFileName = $"{permitNumber}_{fileId}.pdf";
                }
                
                if (!filesInFolder.Contains(destinationFileName))
                {
                    continue;
                }

                var naldLicenceRef = (string)row["License Number"];

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
        }
    }

    return (
        filenamesWithLicenceNumbers,
        licenceNumbersWithFilenames
    );
}