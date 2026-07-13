using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
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

    var pdfDataExtractors = services.PdfDataExtractorServices!;
    var maxConcurrentScrapers = services.MaxConcurrentScrapers;

    if (services.RefreshCache)
    {
        await cacheService.ClearCacheAsync();
    }

    await cacheService.SetupAsync();
    await outputService.SetupAsync();

    var firstNamesTask = cacheService.GetFirstNamesAsync();
    
    var abstractionAndImpoundmentLicencesTask =
        SharedHelper.GetNaldImpoundmentAndAbstractionLicencesAsync(cacheService);
    var naldLinkedLicenceHelperTask = NaldLinkedLicenceHelper.CreateAsync(cacheService);
    
    var (dmsFilesToProcess, _) =
        await DmsHelper.GetDmsFilesAndMappingAsync(
            services.FileService!,
            services.DmsReportPath!,
            false,
            cacheService);
    
    var processRunTask = outputService.StartProcessRunAsync(
        new ProcessRun
        {
            Description = $"Run using {services.FileService!.FolderPath}",
            StartDateTimeUtc = DateTime.UtcNow,
            NumberOfFiles = dmsFilesToProcess.Count
        });

    var firstNamesCsv = await firstNamesTask;
    var processRun = await processRunTask;

    var abstractionAndImpoundmentLicences = await abstractionAndImpoundmentLicencesTask;
    
    LicenceNumber.Instance = new LicenceNumber(abstractionAndImpoundmentLicences);
    var naldLinkedLicenceHelper = await naldLinkedLicenceHelperTask;

    const int unsetRegionCode = GeneralConstants.UnsetRegionCode;
    var licenceSetGroups = new List<IReadOnlyList<LicenceSet>>();
    
    var lookupConfig = new LookupConfiguration(
        WalLabelConfiguration.GetLabels(),
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

    var allLicenceSets = await WalSchemaConverter.AddAdditionalLicenceSetsAsync(
        licenceSetGroups,
        lookupConfig);

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Converted into all licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    WalSchemaConverter.CalculateCombinedAggregates(allLicenceSets);

    await SharedHelper.UpdateAndSaveLicenceSetsAsync(
        licenceSetGroups,
        allLicenceSets,
        outputService,
        processRun);

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Saved licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    await outputService.FinishProcessRunAsync(processRun);

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Finished processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    ConsoleHelper.WriteLine(
        $"INFO - WALE.Cmd - Finished all in {(processRun.EndDateTimeUtc!.Value - processRun.StartDateTimeUtc!.Value).TotalSeconds} seconds - process run id {processRun.ProcessRunId}");
}

async Task<List<LicenceSet>> ScrapeDocumentAsync(
    string pdfFilename,
    int fileNumber,
    int totalNumber,
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
    catch (Exception ex)
    {
        ConsoleHelper.WriteLine($"FATAL ERROR - WALE.Cmd - {pdfFilename} threw fatal error - {ex.Message}");
        return [];
    }
    finally
    {
        pdfDataExtractor.InUse = false;
    }
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
            var regionName = Environment.GetEnvironmentVariable("AwsRegionName")
                ?? throw new NullReferenceException("AwsRegionName");
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