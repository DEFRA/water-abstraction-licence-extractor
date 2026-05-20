using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Net;
using System.Text;
using ExcelDataReader;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums.OutputSchema;
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

    // Filter to Yorks/North region (hard-coded for now - this will need reconsidering
    // when we want to handle more than one region)
    const short regionCode = 3;

    var naldDataTask = cacheService.GetNaldDataAsync(regionCode);
    var firstNamesTask = CompanyName.GetFirstNamesCsvFromFileAsync();
    var dmsFileIdInformationListTask = cacheService.GetDmsFileIdInformationAsync();
    var naldLicenceStatusDataTask = cacheService.GetNaldLicenceStatusDataAsync(regionCode);

    var dtStartGetDms = DateTime.Now;
    ConsoleHelper.WriteLine("INFO - WALE.Cmd - Getting DMS files to process");
    
    var (dmsFilesToProcess, allDmsData) =
        await GetDmsFilesAndMappingAsync(
            services.FileService!,
            services.DmsReportPath!,
            regionCode,
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

    var naldLinkedLicenceHelper = await NaldLinkedLicenceHelper.CreateAsync(
        cacheService,
        regionCode);
    
    var naldData = ExternalDataHelper.TransformNaldData(
        allNaldData,
        allDmsData,
        regionCode);

    var dmsFileIdInformationDict = TranformDmsFileIdInformation(
        await dmsFileIdInformationListTask);
    
    var licenceSetGroups = new List<IReadOnlyList<LicenceSet>>();
    
    try
    {
        var scrapingTasks = new List<Task<List<LicenceSet>>>();
        var processCount = 1;
        var minimumToFreeUp = maxConcurrentScrapers / 3;

        var extractorLock = new Lock();

        var lookupConfig = new LookupConfiguration(
            WalLabelConfiguration.GetLabels(),
            allDmsData,
            dmsFileIdInformationDict,
            firstNamesCsv,
            services.FileService,
            services.CacheService!,
            regionCode,
            naldLinkedLicenceHelper: naldLinkedLicenceHelper);
        
        foreach (var (filePath, dmsDataForFile) in dmsFilesToProcess)
        {
//            await Task.Delay(2000);
            
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

            while (scrapingTasks.Count > maxConcurrentScrapers - minimumToFreeUp)
            {
                var licenceSetsTask = await Task.WhenAny(scrapingTasks);
                scrapingTasks.Remove(licenceSetsTask);

                var scrapeResultLicenceSets = await licenceSetsTask;

                if (scrapeResultLicenceSets.Count == 0)
                {
                    throw new Exception("An empty licence set was returned");
                }

                licenceSetGroups.Add(scrapeResultLicenceSets);
            }
        }

        if (scrapingTasks.Count != 0)
        {
            foreach (var scrapingTask in scrapingTasks)
            {
                var scrapeResultLicenceSets = await scrapingTask;

                if (scrapeResultLicenceSets.Count == 0)
                {
                    throw new Exception("An empty licence set was returned");
                }

                licenceSetGroups.Add(scrapeResultLicenceSets);
            }
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
        allDmsData,
        regionCode);

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
            // TODO log this - it shouldn't happen
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
    await outputService.FinishProcessRunAsync(processRun, regionCode);

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Finished processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    ConsoleHelper.WriteLine(
        $"INFO - WALE.Cmd - Finished all in {(processRun.EndDateTimeUtc!.Value - processRun.StartDateTimeUtc!.Value).TotalSeconds} seconds - process run id {processRun.ProcessRunId}");
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

    #pragma warning disable SYSLIB0014
    ServicePointManager.DefaultConnectionLimit = 100;
    #pragma warning restore SYSLIB0014
    
    var clientHandler = new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    };
    
    var httpClient = new HttpClient(clientHandler);
    httpClient.BaseAddress = new Uri(apiBaseUrl);
    
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
        DmsReportPath = fileMappingPath
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
            // TODO move this to one batch save
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
                    $"INFO - WALE.Cmd - Saved ({fileNumber} of {totalNumber}) in {saveDuration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
        }

        var duration = (DateTime.Now - dtStart).TotalMilliseconds;
        ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Finished ({fileNumber} of {totalNumber}) in {duration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            matchesFull,
            naldLicenceStatusData,
            naldData,
            pdfDataExtractor,
            processRun.ProcessRunId,
            lookupConfig);

        return licenceSets;
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
        int regionCode,
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
            dmsReportPath,
            regionCode);
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
        .Skip(0)
        //.Where(x => x.Key.Contains("12405035_")) // TODO This file is slow (3X slower then some others - work out why)
        //.Where(x => /*x.Key.Contains("12100063") || */ x.Key.Contains("22728083"))
        .Take(10)
        .ToDictionary(filePath => filePath.Key, filePath => filePath.Value);

    return filesAndMapping;
}

async Task<(Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers, Dictionary<string, DmsFileData>
        LicenceNumbersWithFilenames)>
    GetFilesAndMappingFromLicenceFinderResultsAsync(IFileService fileService, ICacheService cacheService)
{
    var filenamesWithLicenceNumbers = new Dictionary<string, DmsFileData>();
    var licenceNumbersWithFilenames = new Dictionary<string, DmsFileData>();

    var lowercaseFilesInFolder = (await fileService.GetAllFilesAsync()).Select(f => f.ToLower()).ToList();
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

        var regionName = licenceFinderResult.Region;
        
        var dmsFileData = new DmsFileData
        {
            DestinationFileName = destinationFileName,
            NaldLicenceRef = licenceFinderResult.LicenseNumber,
            PermitNumber = licenceFinderResult.PermitNumber,
            DmsPath = licenceFinderResult.FileUrl,
            StrippedLicenceNumber = FormattingHelper.StripForComparison(
                licenceFinderResult.LicenseNumber,
                RegionHelper.GetRegionId(regionName))!,
            FileId = Guid.Parse(licenceFinderResult.FileId!)
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
        string dmsReportPath,
        int regionCode)
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
                    StrippedLicenceNumber = FormattingHelper.StripForComparison(naldLicenceRef, regionCode)!,
                    FileId = fileId
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