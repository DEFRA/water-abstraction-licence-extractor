using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
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
    Console.WriteLine("INFO - WALE.Cmd - Started");
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

    var moveReportHtmlFilesTask = MoveReportHtmlFilesAsync(
        services.ReportTemplatePath!,
        outputFolder,
        services.LoadAiJs,
        services.ListDataPath!,
        services.ProcessRunsDataPath!,
        services.InternalDataPath!,
        services.LicenceDataPath!,
        services.LicenceSetsDataPath!,
        services.ThumbnailImageDataPath!,
        services.FullImageDataPath!);

    // Filter to Yorks/North region (hard-coded for now - this will need reconsidering
    // when we want to handle more than one region)
    const short regionCode = 3;
    
    var naldLicenceStatusDataTask = cacheService.GetNaldLicenceStatusDataAsync(
        regionCode);

    var dtStartGetDms = DateTime.Now;
    Console.WriteLine("INFO - WALE.Cmd - Getting DMS files to process");
    
    var (dmsFilesToProcess, allDmsData) =
        GetDmsFilesAndMapping(services, regionCode);

    var saveDuration = (DateTime.Now - dtStartGetDms).TotalMilliseconds;

    Console.WriteLine(
        $"INFO - WALE.Cmd - Got DMS files to process in {saveDuration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    
    var naldDataTask = cacheService.GetNaldDataAsync(regionCode);
    var firstNamesTask = CompanyName.GetFirstNamesCsvFromFileAsync();
    var processRunTask = outputService.StartProcessRunAsync(new ProcessRun
    {
        Description = $"Run using {services.PdfFolderPath}",
        StartDateTimeUtc = DateTime.UtcNow,
        NumberOfFiles = dmsFilesToProcess.Count
    });

    var naldLicenceStatusData  = await naldLicenceStatusDataTask;
    var firstNamesCsv = await firstNamesTask;
    var processRun = await processRunTask;
    await moveReportHtmlFilesTask;

    var allNaldData =  await naldDataTask;
    LicenceNumber.Instance = new LicenceNumber(allNaldData.LicencesAlternateFormat!);

    var naldLinkedLicenceHelper = await NaldLinkedLicenceHelper.CreateAsync(
        cacheService,
        regionCode);
    
    var naldData = ExternalDataHelper.TransformNaldData(
        allNaldData,
        allDmsData,
        regionCode);
    
    var licenceSetGroups = new List<IReadOnlyList<LicenceSet>>();

    try
    {
        var scrapingTasks = new List<Task<List<LicenceSet>>>();
        var processCount = 1;
        var minimumToFreeUp = maxConcurrentScrapers / 3;

        var extractorLock = new Lock();

        foreach (var (filePath, _) in dmsFilesToProcess)
        {
            scrapingTasks.Add(
                ScrapeDocumentAsync(
                    filePath,
                    regionCode,
                    processCount++,
                    processRun.NumberOfFiles,
                    allDmsData,
                    naldLicenceStatusData,
                    naldData,
                    outputService,
                    pdfDataExtractors,
                    firstNamesCsv,
                    processRun,
                    extractorLock));

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
            await Task.WhenAll(scrapingTasks);

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
        Console.WriteLine("ERROR - WALE.Cmd - Error during scraping: {e}");
        throw;
    }

    Console.WriteLine($"INFO - WALE.Cmd - All scraped at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

    var allLicenceSets = SchemaConverter.AddAdditionalLicenceSets(
        licenceSetGroups,
        naldLicenceStatusData,
        allDmsData,
        regionCode);

    Console.WriteLine($"INFO - WALE.Cmd - Converted into all licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

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
                var linkedLicences =
                    naldLinkedLicenceHelper.GetLinkedLicences(licenceLoop.LicenceNumber);
                
                if (linkedLicences.Count != 0)
                {
                    licenceLoop.NoneSchemaData["NaldLinkedLicences"] = linkedLicences;
                }

                var filename = licenceLoop.Filename;

                if (licenceLoop.LicenceNumber != null
                    && (!savedLicenceNumbers.TryGetValue(licenceLoop.LicenceNumber, out _)
                        || (licenceLoop.Status == LicenceStatus.Ok &&
                            notFoundSavedLicenceNumbers.TryGetValue(licenceLoop.LicenceNumber, out _))))
                {
                    int loopLicenceId;
                    var savedVersionIsStatusNotFound =
                        notFoundSavedLicenceNumbers.TryGetValue(licenceLoop.LicenceNumber, out var existingLicenceId);

                    if (savedVersionIsStatusNotFound && licenceLoop.Status == LicenceStatus.Ok)
                    {
                        await outputService.UpdateLicenceAsync(
                            licenceLoop,
                            existingLicenceId,
                            filename,
                            processRun.ProcessRunId);

                        loopLicenceId = existingLicenceId;
                    }
                    else
                    {
                        loopLicenceId = await outputService.SaveLicenceAsync(
                            licenceLoop,
                            filename,
                            processRun.ProcessRunId);
                    }

                    savedLicenceNumbers.TryAdd(licenceLoop.LicenceNumber, loopLicenceId);

                    if (!string.IsNullOrWhiteSpace(filename))
                    {
                        savedLicenceFilenames.TryAdd(filename, loopLicenceId);
                    }

                    if (licenceLoop.Status == LicenceStatus.NotFound)
                    {
                        notFoundSavedLicenceNumbers.TryAdd(licenceLoop.LicenceNumber, loopLicenceId);
                    }
                    else
                    {
                        notFoundSavedLicenceNumbers.Remove(licenceLoop.LicenceNumber);
                    }

                    licenceLoop.NoneSchemaData["licenceId"] = loopLicenceId;
                }
                else if (licenceLoop.LicenceNumber == null
                     && !string.IsNullOrEmpty(filename)
                     && !savedLicenceFilenames.TryGetValue(filename, out _))
                {
                    var loopLicenceId = await outputService.SaveLicenceAsync(
                        licenceLoop,
                        filename,
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
                    licenceLoop.Filename!,
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

    Console.WriteLine($"INFO - WALE.Cmd - Saved licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    const bool saveListsToFile = false;

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

    Console.WriteLine($"INFO - WALE.Cmd - Saved list at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

    processRun.EndDateTimeUtc = DateTime.UtcNow;
    await outputService.FinishProcessRunAsync(processRun, regionCode);

    Console.WriteLine($"INFO - WALE.Cmd - Finished processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine(
        $"INFO - WALE.Cmd - Finished all in {(processRun.EndDateTimeUtc.Value - processRun.StartDateTimeUtc!.Value).TotalSeconds} seconds - process run id {processRun.ProcessRunId}");

    //Console.WriteLine(SchemaConverter.DiffCounter + " licence number tweaks");
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
    var pdfFolderPath = Environment.GetEnvironmentVariable("PdfFolderPath")
                        ?? throw new NullReferenceException("PdfFolderPath");
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
    var postgresHost = Environment.GetEnvironmentVariable("POSTGRESQL_HOST")
                       ?? throw new NullReferenceException("POSTGRESQL_HOST");
    var postgresPort = int.Parse(Environment.GetEnvironmentVariable("POSTGRESQL_PORT")
                                 ?? throw new NullReferenceException("POSTGRESQL_PORT"));
    var postgresDatabaseName = Environment.GetEnvironmentVariable("POSTGRESQL_DBNAME")
                               ?? throw new NullReferenceException("POSTGRESQL_DBNAME");
    var postgresUsername = Environment.GetEnvironmentVariable("POSTGRESQL_USERNAME")
                           ?? throw new NullReferenceException("POSTGRESQL_USERNAME");
    var postgresPassword = Environment.GetEnvironmentVariable("POSTGRESQL_PASSWORD")
                           ?? throw new NullReferenceException("POSTGRESQL_PASSWORD");
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

    // This provider should have singleton lifetime and be shared for proper connection pooling
    var postgresDataSourceProvider = new NpgsqlDataSourceProvider(
        postgresHost,
        postgresPort,
        postgresDatabaseName,
        postgresUsername,
        postgresPassword);

    Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

    var databaseReadService = new PostgresReadService(postgresDataSourceProvider);
    var databaseAddService = new PostgresWriteService(postgresDataSourceProvider);
    
    var databaseCacheService = new DatabaseCacheService(
        databaseReadService,
        databaseAddService);

    var httpClient = new HttpClient();
    httpClient.BaseAddress = new Uri(apiBaseUrl);
    
    var apiCacheService = new ApiCacheService(httpClient);
    
    var cacheService = new MixedModeCacheService(apiCacheService, databaseCacheService);
    var outputService = new ApiOutputService(httpClient);

    var pdfPigDocumentService = new PdfPigNoOcrPdfDocumentService();
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
            pdfFolderPath,
            id);

        pdfDataExtractors.Add(pdfDataExtractor);
    }

    return new ConfiguredServices
    {
        CacheService = cacheService,
        OutputService = outputService,
        DatabaseReadService = databaseReadService,
        PdfDataExtractorServices = pdfDataExtractors,
        MaxConcurrentScrapers = maxConcurrentScrapers,
        OutputFolder = outputFolder,
        RegenerateMappingJson = regenerateMappingJson,
        PdfFolderPath = pdfFolderPath,
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
        FileMappingPath = fileMappingPath
    };
}

async Task<List<LicenceSet>> ScrapeDocumentAsync(
    string pdfFilePath,
    int regionCode,
    int fileNumber,
    int totalNumber,
    Dictionary<string, DmsFileData> licenceMapping,
    NaldLicenceStatusData naldLicenceStatusData,
    Dictionary<string, List<NaldData>> naldData,
    IOutputService outputService,
    List<IPdfDataExtractorService> pdfDataExtractors,
    HashSet<string> firstNamesCsv,
    ProcessRun processRun,
    Lock extractorLock)
{
    var fileName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);

    var dtStart = DateTime.Now;
    Console.WriteLine($"INFO - WALE.Cmd - Started {fileName} ({fileNumber} of {totalNumber}) at {dtStart:yyyy-MM-dd HH:mm:ss}");

    IPdfDataExtractorService pdfDataExtractor;

    lock (extractorLock)
    {
        pdfDataExtractor = pdfDataExtractors.First(x => !x.InUse);
        pdfDataExtractor.InUse = true;
    }

    try
    {
        var previouslyParsedPaths = new List<string>
        {
            pdfFilePath
        };

        var pdfFolder = pdfFilePath[..(pdfFilePath.LastIndexOf('/') + 1)];

        var lookupConfig = new LookupConfiguration(
            LabelConfiguration.GetLabels(),
            licenceMapping,
            firstNamesCsv,
            regionCode);

        var matchesFull = await pdfDataExtractor.GetMatchesAsync(
            pdfFilePath,
            lookupConfig,
            previouslyParsedPaths,
            processRun.ProcessRunId);

        var matchResultId = await outputService.SaveMatchResultAsync(
            matchesFull,
            pdfFilePath,
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
                Console.WriteLine(
                    $"INFO - WALE.Cmd - Saved ({fileNumber} of {totalNumber}) in {saveDuration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
        }

        var duration = (DateTime.Now - dtStart).TotalMilliseconds;
        Console.WriteLine($"INFO - WALE.Cmd - Finished ({fileNumber} of {totalNumber}) in {duration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var licenceSets = await SchemaConverter.ToLicenceSetsAsync(
            matchesFull,
            licenceMapping,
            naldLicenceStatusData,
            naldData,
            pdfDataExtractor,
            pdfFolder,
            processRun.ProcessRunId,
            lookupConfig);

        return licenceSets;
    }
    finally
    {
        pdfDataExtractor.InUse = false;
    }
}

async Task MoveReportHtmlFilesAsync(
    string reportTemplatePath,
    string outputFolder,
    bool loadAiJs,
    string listDataPath,
    string processRunsPath,
    string internalDataPath,
    string licenceDataPath,
    string licenceSetsDataPath,
    string thumbnailImageDataPath,
    string fullImageDataPath)
{
    Copy(reportTemplatePath, outputFolder);

    var aiFiles = Directory.GetFiles("Data");

    foreach (var aiFile in aiFiles)
    {
        if (!aiFile.EndsWith(".js"))
        {
            continue;
        }

        var aiFilePath = aiFile.Split('/').Last().Replace(".js", string.Empty);

        Directory.CreateDirectory($"{outputFolder}{aiFilePath}");
        File.Move(aiFile, $"{outputFolder}{aiFilePath}/ai-data.jsonp", true);
    }

    var reportPath = $"{outputFolder}report.html";
    File.Move($"{outputFolder}report-template.html", reportPath, true);

    var reportHtml = await File.ReadAllTextAsync(reportPath);
    reportHtml = reportHtml.Replace("[INTERNAL_DATA_PATH]", internalDataPath);
    reportHtml = reportHtml.Replace("[LICENCE_DATA_PATH]", licenceDataPath);
    reportHtml = reportHtml.Replace("[FULL_IMAGE_DATA_PATH]", fullImageDataPath);
    reportHtml = reportHtml.Replace("[LICENCE_SETS_DATA_PATH]", licenceSetsDataPath);

    await File.WriteAllTextAsync(reportPath, reportHtml);

    File.Move($"{outputFolder}licence-set-report-template.html", $"{outputFolder}licencesetreport.html", true);

    var processRunSelectorPath = $"{outputFolder}index.html";
    File.Move($"{outputFolder}process-runs-template.html", processRunSelectorPath, true);

    var processRunsHtml = await File.ReadAllTextAsync(processRunSelectorPath);
    processRunsHtml = processRunsHtml.Replace("[PROCESS_RUNS_DATA_PATH]", processRunsPath);

    await File.WriteAllTextAsync(processRunSelectorPath, processRunsHtml);

    var indexPath = $"{outputFolder}list.html";
    File.Move($"{outputFolder}list-template.html", indexPath, true);

    var indexHtml = await File.ReadAllTextAsync(indexPath);
    indexHtml = indexHtml.Replace("[LOAD_AI_JS]", loadAiJs.ToString().ToLower());
    indexHtml = indexHtml.Replace("[LIST_DATA_PATH]", listDataPath);
    indexHtml = indexHtml.Replace("[THUMBNAIL_IMAGE_DATA_PATH]", thumbnailImageDataPath);

    await File.WriteAllTextAsync(indexPath, indexHtml);
}

(Dictionary<string, DmsFileData> FilepathsWithLicenceNumbers, Dictionary<string, DmsFileData>
    LicenceNumbersWithFilenames)
    GetDmsFilesAndMapping(ConfiguredServices services, int regionCode)
{
    //var filesAndMapping = GetFilesAndMappingFromFolders(services.PdfFolderPath!);
    var filesAndMapping = GetFilesAndMappingFromExcelDownloadInfoFile(
        services.PdfFolderPath!,
        services.FileMappingPath!,
        regionCode);

    /*filesAndMapping.FilepathsWithLicenceNumbers = filesAndMapping.FilepathsWithLicenceNumbers
        .Where(filePath => filePath.Key.Contains("22722086"))
        .ToDictionary(filePath => filePath.Key, k => k.Value);*/

    filesAndMapping.FilepathsWithLicenceNumbers = filesAndMapping.FilepathsWithLicenceNumbers
        .OrderBy(filePath => filePath.Key)
        .Skip(0)
        .Take(10)
//       .Where(x => x.Key.Contains("12405035_")) // TODO This file is slow (3X slower then some others - work out why)
//        .Where(x => x.Key.Contains("22718077_"))
//        .Take(100)
        .ToDictionary(filePath => filePath.Key, filePath => filePath.Value);

    return filesAndMapping;
}

(Dictionary<string, DmsFileData> FilepathsWithLicenceNumbers, Dictionary<string, DmsFileData>
    LicenceNumbersWithFilenames)
    GetFilesAndMappingFromExcelDownloadInfoFile(string pdfFolderPath, string mappingFilePath, int regionCode)
{
    var filenames = new Dictionary<string, DmsFileData>();
    var mappingFile = new Dictionary<string, DmsFileData>();

    // Register encoding provider for ExcelDataReader
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    var filesInFolder = Directory
        .GetFiles(pdfFolderPath)
        .Select(path => path.Split('/').Last())
        .ToList();

    using (var stream = File.Open(mappingFilePath, FileMode.Open, FileAccess.Read))
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

                var dmsPath = (string)row["Definitive URL"];
                var dmsPathFilename = dmsPath.Split('/').Last();
                
                var destinationFileName = $"{permitNumber}__{dmsPathFilename}";
                
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
                    StrippedLicenceNumber = FormattingHelper.StripForComparison(naldLicenceRef, regionCode)!
                };

                filenames.Add(pdfFolderPath + destinationFileName, dmsFileData);
                mappingFile.Add(dmsFileData.StrippedLicenceNumber, dmsFileData);
            }
        }
    }

    return (
        filenames,
        mappingFile
    );
}

void Copy(string sourceDir, string targetDir)
{
    Directory.CreateDirectory(targetDir);

    foreach (var file in Directory.GetFiles(sourceDir))
    {
        File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
    }

    foreach (var directory in Directory.GetDirectories(sourceDir))
    {
        Copy(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
    }
}