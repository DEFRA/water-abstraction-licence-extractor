using Microsoft.Extensions.Configuration;
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


var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>(
        optional: true)
    .AddEnvironmentVariables()
    .Build();

await ProgramAsync(configuration);
return;

async Task ProgramAsync(IConfiguration configurationItem)
{
    ConsoleHelper.WriteLine("INFO - WALE.Cmd - Started");
    var startDateTimeUtc = DateTime.UtcNow;
    
    var services = ConfigureServices(configurationItem);

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
            configuration.GetValue<bool>("CheckWeHaveFileToProcess"));

    // For debugging uncheck sections of the following
    filesToProcess = filesToProcess
        //.Where(x => x.Key.Contains("22722027", StringComparison.OrdinalIgnoreCase)
        //|| x.Key.Contains("1asdssdds", StringComparison.OrdinalIgnoreCase))
        .Where(x => x.Key.Contains("22715041", StringComparison.OrdinalIgnoreCase))
        //.Where(x => x.Value.Item2.RegionCode == 3) // North east
        //.Skip(10)
        .Take(20)
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
    
    var licenceNumberService = new AbstractionLicenceNumber(abstractionAndImpoundmentLicences);
    services.LicenceNumberService = licenceNumberService;
    
    var naldLinkedLicenceHelper = await NaldLinkedLicenceHelper.CreateAsync(
        abstractionLicenceCacheService,
        licenceNumberService);

    var licenceSetGroups = new List<IReadOnlyList<LicenceSet>>();
    
    var lookupConfig = new LookupConfiguration(
        AbstractionLicenceLabelConfiguration.GetLabels(),
        firstNamesCsv,
        services.FileService,
        services.CacheService!,
        services.OutputService!,
        services.LicenceNumberService!,
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

    var allLicenceSets = await AbstractionLicenceSchemaConverter.AddAdditionalLicenceSetsAsync(
        licenceSetGroups,
        lookupConfig,
        abstractionLicenceCacheService);

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Converted into all licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    AbstractionLicenceSchemaConverter.CalculateCombinedAggregates(allLicenceSets);

    await SharedHelper.UpdateAndSaveLicenceSetsAsync(
        licenceSetGroups,
        allLicenceSets,
        abstractionLicenceOutputService,
        processRun);

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Saved licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    await abstractionLicenceOutputService.FinishProcessRunAsync(processRun);
    
    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Starting Licence List Data Refresh processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    
    FireAndForgetDataRefresh(abstractionLicenceOutputService, processRun);
    
    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Finished Licence List Data Refresh at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

    ConsoleHelper.WriteLine($"INFO - WALE.Cmd - Finished processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    ConsoleHelper.WriteLine(
        $"INFO - WALE.Cmd - Finished all in {(processRun.EndDateTimeUtc!.Value - processRun.StartDateTimeUtc!.Value).TotalSeconds} seconds - process run id {processRun.ProcessRunId}");
}

void FireAndForgetDataRefresh(IAbstractionLicenceOutputService abstractionLicenceOutputService, ProcessRun processRun)
{
    _ = Task.Run((Func<Task?>)(async () =>
    {
        try
        {
            await abstractionLicenceOutputService
                .UpdateLicenceListProcessRunAsync(processRun.ProcessRunId);
        }
        catch
        {
            // intentionally swallowed
        }
    }));
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

        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
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

ConfiguredServices ConfigureServices(
    IConfiguration configurationForServices)
{
    var maxConcurrentScrapers =
        configurationForServices.GetRequiredValue<int>(
            "ConcurrentCount");

    var regenerateMappingJson =
        configurationForServices.GetRequiredValue<bool>(
            "REGENERATE_MAPPING_JSON");

    var loadAiJs =
        configurationForServices.GetRequiredValue<bool>(
            "LOAD_AI_JS");

    var refreshCache =
        configurationForServices.GetRequiredValue<bool>(
            "RefreshCache");

    var reportTemplatePath =
        configurationForServices.GetRequiredValue<string>(
            "ReportTemplatePath");

    var outputFolder =
        configurationForServices.GetRequiredValue<string>(
            "OutputFolder");

    var listDataPath =
        configurationForServices.GetRequiredValue<string>(
            "ListDataPath");

    var processRunsDataPath =
        configurationForServices.GetRequiredValue<string>(
            "ProcessRunsDataPath");

    var internalDataPath =
        configurationForServices.GetRequiredValue<string>(
            "InternalDataPath");

    var licenceDataPath =
        configurationForServices.GetRequiredValue<string>(
            "LicenceDataPath");

    var licenceSetsDataPath =
        configurationForServices.GetRequiredValue<string>(
            "LicenceSetsDataPath");

    var thumbnailImageDataPath =
        configurationForServices.GetRequiredValue<string>(
            "ThumbnailImageDataPath");

    var fullImageDataPath =
        configurationForServices.GetRequiredValue<string>(
            "FullImageDataPath");

    var fileMappingPath =
        configurationForServices.GetRequiredValue<string>(
            "FileMappingPath");

    var dotnetPath =
        configurationForServices.GetRequiredValue<string>(
            "DotnetPath");

    var tesseractExeName =
        configurationForServices.GetRequiredValue<string>(
            "TesseractExeName");

    var tesseractExeDirectory =
        configurationForServices.GetRequiredValue<string>(
            "TesseractExeDirectory");

    var tessDataPrefix =
        configurationForServices.GetRequiredValue<string>(
            "TESSDATA_PREFIX");

    var apiBaseUrl =
        configurationForServices.GetRequiredValue<string>(
            "ApiBaseUrl");

    var delayPerProcessMs =
        configurationForServices.GetValue<int?>(
            "DelayPerProcessMs")
        ?? 200;

    var httpClient = HttpHelper.GetResilientHttpClient(
        apiBaseUrl,
        100,
        30);

    var fileServiceType =
        configurationForServices.GetValue<string>(
            "FileServiceType")
        ?? "api";

    IFileService fileService;

    switch (fileServiceType.ToLowerInvariant())
    {
        case "api":
            fileService = new ApiFileService(httpClient);
            break;

        case "s3":
        {
            var accessKey =
                configurationForServices.GetRequiredValue<string>(
                    "AwsS3AccessKey");

            var secretKey =
                configurationForServices.GetRequiredValue<string>(
                    "AwsS3SecretKey");

            var regionName =
                configurationForServices.GetRequiredValue<string>(
                    "AwsRegionName");

            var bucketName =
                configurationForServices.GetRequiredValue<string>(
                    "AwsS3BucketName");

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
            var pdfFolderPath =
                configurationForServices.GetRequiredValue<string>(
                    "PdfFolderPath");

            if (!pdfFolderPath.EndsWith('/'))
            {
                pdfFolderPath += "/";
            }

            fileService = new LocalFileService(pdfFolderPath);
            break;
        }
    }

    var cacheService =
        new ApiCacheService(httpClient);

    IAbstractionLicenceCacheService
        abstractionLicenceCacheService =
            new ApiAbstractionLicenceCacheService(
                httpClient);

    var outputService =
        new ApiOutputService(httpClient);

    IAbstractionLicenceOutputService
        abstractionLicenceOutputService =
            new ApiAbstractionLicenceOutputService(
                httpClient);

    var messageQueueService =
        new ApiMessageQueueService(httpClient);

    var pdfPigDocumentService =
        new PdfPigNoOcrPdfDocumentService();

    var docnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();

    var pdfDataExtractors =
        new List<IPdfDataExtractorService>();

    var azureAiVisionEndpoint =
        configurationForServices.GetRequiredValue<string>(
            "AzureAIVisionEndpoint");

    var azureAiVisionKey =
        configurationForServices.GetRequiredValue<string>(
            "AzureAIVisionKey");

    for (var idx = 0;
         idx < maxConcurrentScrapers;
         idx++)
    {
        var id = idx + 1;

        var pdfPigNoOcr =
            new PdfPigNoOcrDataExtractorService();

        var tesseractOcrSparse =
            new TesseractOcrDataExtractorService(
                tessDataPrefix,
                WALE.ProcessFile.Core.Enums.PageSegMode
                    .SparseTextOsd,
                cacheService,
                outputService,
                dotnetPath,
                tesseractExeName,
                tesseractExeDirectory,
                id);

        var tesseractOcrDefault =
            new TesseractOcrDataExtractorService(
                tessDataPrefix,
                WALE.ProcessFile.Core.Enums.PageSegMode.Auto,
                cacheService,
                outputService,
                dotnetPath,
                tesseractExeName,
                tesseractExeDirectory,
                id);

        var azureAiServices =
            new AzureAiVisionOcrDataExtractorService(
                azureAiVisionEndpoint,
                azureAiVisionKey,
                cacheService,
                outputService,
                id);

        var pdfDataExtractor =
            new PdfDataExtractorService(
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
        AbstractionLicenceCacheService =
            abstractionLicenceCacheService,
        OutputService = outputService,
        AbstractionLicenceOutputService =
            abstractionLicenceOutputService,
        LicenceNumberService = null,
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
        ThumbnailImageDataPath =
            thumbnailImageDataPath,
        FullImageDataPath = fullImageDataPath,
        RefreshCache = refreshCache,
        DmsReportPath = fileMappingPath,
        DelayPerProcessMs = delayPerProcessMs
    };
}