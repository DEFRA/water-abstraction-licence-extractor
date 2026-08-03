using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.AwsS3;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.DocumentType.AbstractionLicence.Formats;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.ProcessFile.Cmd.AbstractionLicence;
using WRADI.Services.Cache.AbstractionLicence;
using WRADI.Services.Output.AbstractionLicence;

await ProgramAsync();
return;

async Task ProgramAsync()
{
    ConsoleHelper.WriteLine("INFO - WALE.Cmd - Started");
    var startDateTimeUtc = DateTime.UtcNow;
    
    var services = ConfigureServices();

    var cacheService = services.CacheService!;
    var abstractionLicenceCacheService = services.AbstractionLicenceCacheService!;
    var outputService = services.OutputService!;
    var abstractionLicenceOutputService = services.AbstractionLicenceOutputService!;

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
        SharedHelper.GetNaldImpoundmentAndAbstractionLicencesAsync(abstractionLicenceCacheService);
    
    var (filesToProcess, _) =
        await DmsHelper.GetDmsAndNaldFilesAndMappingAsync(
            services.FileService!,
            services.DmsReportPath!,
            false,
            abstractionLicenceCacheService,
            false);

    // For debugging uncheck sections of the following
    filesToProcess = filesToProcess
        //.Where(x => x.Key.Contains("22722027", StringComparison.OrdinalIgnoreCase)
        //|| x.Key.Contains("1asdssdds", StringComparison.OrdinalIgnoreCase))
        .Where(x => x.Key.Contains("22723435"))
        //.Where(x => x.Value.Item2.RegionCode == 3) // North east
        //.Skip(10)
        //.Take(500)
        .ToDictionary(
            filePath => filePath.Key,
            filePath => filePath.Value);
    
    var processRunTask = outputService.StartProcessRunAsync(
        new ProcessRun
        {
            Description = $"Run using {services.FileService!.FolderPath}",
            StartDateTimeUtc = startDateTimeUtc,
            NumberOfFiles = filesToProcess.Count
        });

    var firstNamesCsv = await firstNamesTask;
    var processRun = await processRunTask;

    var abstractionAndImpoundmentLicences = await abstractionAndImpoundmentLicencesTask;
    
    AbstractionLicenceNumber.Instance = new AbstractionLicenceNumber(abstractionAndImpoundmentLicences);
    var naldLinkedLicenceHelper = await NaldLinkedLicenceHelper.CreateAsync(abstractionLicenceCacheService);

    var licenceSetGroups = new List<IReadOnlyList<LicenceSet>>();
    
    var lookupConfig = new LookupConfiguration(
        WalLabelConfiguration.GetLabels(),
        firstNamesCsv,
        services.FileService,
        services.CacheService!,
        services.OutputService!,
        GeneralConstants.UnsetRegionCode,
        DateTime.Now,
        naldLinkedLicenceHelper: naldLinkedLicenceHelper,
        lockInProcess: true);
    
    try
    {
        var scrapingTasks = new List<Task<List<LicenceSet>>>();
        var processCount = 1;
        
        foreach (var (filePath, (dmsFileData, naldLicence)) in filesToProcess)
        {
            if (services.DelayPerProcessMs > 0)
            {
                await Task.Delay(services.DelayPerProcessMs);
            }

            var loopLookupConfig = lookupConfig.Clone();
            loopLookupConfig.RegionId = naldLicence.RegionCode;
            
            var pdfDataExtractor = pdfDataExtractors.First(extractor => !extractor.InUse);
            pdfDataExtractor.InUse = true;
            
            scrapingTasks.Add(
                ScrapeDocumentAsync(
                    filePath,
                    processCount++,
                    processRun.NumberOfFiles,
                    pdfDataExtractor,
                    processRun,
                    loopLookupConfig,
                    abstractionLicenceCacheService,
                    dmsFileData,
                    naldLicence.LicenceNumber));

            while (scrapingTasks.Count >= maxConcurrentScrapers)
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
        lookupConfig,
        abstractionLicenceCacheService);

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Converted into all licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    WalSchemaConverter.CalculateCombinedAggregates(allLicenceSets);

    await SharedHelper.UpdateAndSaveLicenceSetsAsync(
        licenceSetGroups,
        allLicenceSets,
        abstractionLicenceOutputService,
        processRun);

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Saved licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    await abstractionLicenceOutputService.FinishProcessRunAsync(processRun);

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Finished processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    ConsoleHelper.WriteLine(
        $"INFO - WALE.Cmd - Finished all in {(processRun.EndDateTimeUtc!.Value - processRun.StartDateTimeUtc!.Value).TotalSeconds} seconds - process run id {processRun.ProcessRunId}");
}

async Task<List<LicenceSet>> ScrapeDocumentAsync(
    string pdfFilename,
    int fileNumber,
    int totalNumber,
    IPdfDataExtractorService pdfDataExtractor,
    ProcessRun processRun,
    LookupConfiguration lookupConfig,
    IAbstractionLicenceCacheService cacheService,
    DmsFileData dmsDataForFile,
    string naldLicenceNumber)
{
    var dtStart = DateTime.Now;
    ConsoleHelper.WriteLine($"INFO - WALE.Cmd:{pdfDataExtractor.Id} - Started {pdfFilename} ({fileNumber} of {totalNumber}) at {dtStart:yyyy-MM-dd HH:mm:ss}");
    
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
            return [];
        }

        if (alreadySaved != true)
        {
            await pdfDataExtractor.SaveMatchResultAsync(
                matchesResult!,
                dmsDataForFile.FileId,
                processRun.ProcessRunId);
        }

        var licenceSets = await WalSchemaConverter.ToLicenceSetsAsync(
            matchesResult!,
            pdfDataExtractor,
            processRun.ProcessRunId,
            lookupConfig,
            cacheService,
            dmsDataForFile,
            naldLicenceNumber);

        var duration = (DateTime.Now - dtStart).TotalMilliseconds;
        ConsoleHelper.WriteLine(
            $"INFO - WALE.Cmd:{pdfDataExtractor.Id}  - Finished (save licence sets etc..) {dmsDataForFile.FileId} ({fileNumber} of {totalNumber}) in {duration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        
        return licenceSets;
    }
    catch (TooManyPagesException)
    {
        ConsoleHelper.WriteLine($"WARNING - WALE.Cmd:{pdfDataExtractor.Id}  - Skipped ({fileNumber} of {totalNumber}) as too many pages");
        return [];
    }
    catch (TooManyImagesException)
    {
        ConsoleHelper.WriteLine($"WARNING - WALE.Cmd:{pdfDataExtractor.Id}  - Skipped ({fileNumber} of {totalNumber}) as too many pages");
        return [];
    }
    catch (Exception ex)
    {
        ConsoleHelper.WriteLine($"FATAL ERROR - WALE.Cmd:{pdfDataExtractor.Id}  - {pdfFilename} threw fatal error - {ex}");
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

    var delayPerProcessMs = 200; // TODO get from variable
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
    
    var abstractionLicenceCacheService =
        (IAbstractionLicenceCacheService?)new ApiAbstractionLicenceCacheService(httpClient);
    
    var outputService = new ApiOutputService(httpClient);
    var abstractionLicenceOutputService =
        (IAbstractionLicenceOutputService?)new ApiAbstractionLicenceOutputService(httpClient);
    
    var messageQueueService = new ApiMessageQueueService(httpClient);

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
            messageQueueService,
            id);

        pdfDataExtractors.Add(pdfDataExtractor);
    }

    return new ConfiguredServices
    {
        CacheService = cacheService,
        AbstractionLicenceCacheService = abstractionLicenceCacheService,
        OutputService = outputService,
        AbstractionLicenceOutputService = abstractionLicenceOutputService,
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