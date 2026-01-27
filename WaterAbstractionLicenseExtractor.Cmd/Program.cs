using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using Tesseract;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WaterAbstractionLicenseExtractor.Cmd;

await ProgramAsync();
return;

async Task ProgramAsync()
{
    Console.WriteLine("Started");

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

    await MoveReportHtmlFilesAsync(
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

    // Filter to Yorks/North region (hard-coded for now - this will need reconsidering when we want to handle more than one region)
    var regionCode = 3;
    
    var naldLicenceStatusData = new NaldLicenceStatusData
    {
        LiveLicences = ExternalDataHelper.GetLiveLicenceNumbers(
            Environment.GetEnvironmentVariable("LiveLicencesPath"), regionCode),
        DeadLicences = ExternalDataHelper.GetDeadLicenceNumbers(
            Environment.GetEnvironmentVariable("DeadLicencesPath"), regionCode),
        ImpoundmentLicences = ExternalDataHelper.GetImpoundmentLicenceNumbers(
            Environment.GetEnvironmentVariable("ImpoundmentLicencesPath"), regionCode)
    };
    
    var (dmsFilesToProcess, allDmsData) = GetDmsFilesAndMapping(services, regionCode);
    
    var naldData = ExternalDataHelper.GetNaldAbstractionLicencesData(
        allDmsData,
        Environment.GetEnvironmentVariable("NaldAbsLicencesDataPath"),
        Environment.GetEnvironmentVariable("NaldAbsLicencePurposesDataPath"),
        Environment.GetEnvironmentVariable("NaldAbsLicencePointsDataPath"),
        Environment.GetEnvironmentVariable("NaldAbsLicenceVersionsDataPath"),
        Environment.GetEnvironmentVariable("NaldAbsLicenceQuantitiesDataPath"),
        regionCode);
    
    var naldLinkedLicenceRawData = await services.DatabaseReadService!.GetNaldLinkedLicenceRawDataAsync();

    // filter to Yorks/North region (hard-coded for now - this will need reconsidering when we want to handle more than one region)
    var yorkshireNaldData = naldLinkedLicenceRawData.Where(x => x.RegionCode == regionCode.ToString());
    var yorkshireNaldHelper = await NaldLinkedLicenceHelper.CreateAsync(yorkshireNaldData.ToList(), regionCode);
    
    var processRun = await outputService.SaveProcessRunAsync(new ProcessRun
    {
        Description = $"Run using {services.PdfFolderPath}",
        StartDateTimeUtc = DateTime.UtcNow,
        NumberOfFiles = dmsFilesToProcess.Count
    });
    
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
                    allDmsData,
                    naldLicenceStatusData,
                    naldData,
                    outputService,
                    pdfDataExtractors,
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
        Console.WriteLine(e);
        throw;
    }

    Console.WriteLine($"All scraped at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    
    var allLicenceSets = SchemaConverter.AddAdditionalLicenceSets(
        licenceSetGroups,
        naldLicenceStatusData,
        allDmsData,
        regionCode);
    
    Console.WriteLine($"Converted into all licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    
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
                var linkedLicences = yorkshireNaldHelper.GetLinkedLicences(licenceLoop.LicenceNumber, regionCode);
                if (linkedLicences.Any())
                {
                    licenceLoop.NoneSchemaData["NaldLinkedLicences"] = linkedLicences;
                }

                var filename = licenceLoop.Filename;
                
                if (licenceLoop.LicenceNumber != null
                    && (!savedLicenceNumbers.TryGetValue(licenceLoop.LicenceNumber, out _)
                        || (licenceLoop.Status == LicenceStatus.Ok && notFoundSavedLicenceNumbers.TryGetValue(licenceLoop.LicenceNumber, out _))))
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
        var licenceSets = GetLicenceSetsForLicenceSetIds(licence.LicenceSets, allLicenceSets);

        var outputLine = JsOutputHelper.ToOutputLine(
            licence,
            DateTime.Now,
            completeNumber++,
            fileNumber++,
            licenceSets);

        outputLines.Add(outputLine);
    }
    
    Console.WriteLine($"Saved licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    
    await JsOutputHelper.SaveListDataAsync(
        outputLines,
        outputFolder,
        outputService,
        services.RegenerateMappingJson,
        processRun,
        true);
    
    Console.WriteLine($"Saved list at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    
    processRun.EndDateTimeUtc = DateTime.UtcNow;
    await outputService.FinishProcessRunAsync(processRun, regionCode);
    
    Console.WriteLine($"Finished processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"Finished all in {(processRun.EndDateTimeUtc.Value - processRun.StartDateTimeUtc!.Value).TotalSeconds} seconds - process run id {processRun.ProcessRunId}");
    
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
    var postgresPassword = Environment.GetEnvironmentVariable("POSTGRESQL_PASSSWORD")
        ?? throw new NullReferenceException("POSTGRESQL_PASSSWORD");    
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

    LicenceNumber.Instance = new LicenceNumber(databaseReadService);
    
    var cacheService = new DatabaseCacheService(
        databaseReadService,
        databaseAddService,
        postgresHost,
        postgresPort,
        postgresDatabaseName,
        postgresUsername,
        postgresPassword);
    
    var outputService = new DatabaseOutputService(databaseReadService, databaseAddService);
    
    var pdfDataExtractors = new List<IPdfDataExtractorService>();
    
    for (var idx = 0; idx < maxConcurrentScrapers; idx++)
    {
        var id = idx + 1;
        var pdfPigNoOcr = new PdfPigNoOcrDataExtractorService();

        var tesseractOcrSparse = new TesseractOcrDataExtractorService(
            tessDataPrefix,
            PageSegMode.SparseTextOsd,
            cacheService,
            outputService,
            dotnetPath,
            tesseractExeName,
            tesseractExeDirectory,
            id);
        
        var tesseractOcrDefault = new TesseractOcrDataExtractorService(
            tessDataPrefix,
            PageSegMode.Auto,
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
    Dictionary<string, DmsFileData> licenceMapping,
    NaldLicenceStatusData naldLicenceStatusData,
    Dictionary<string, List<NaldData>> naldData,
    IOutputService outputService,
    List<IPdfDataExtractorService> pdfDataExtractors,
    ProcessRun processRun,
    Lock extractorLock)
{
    var fileName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);

    Console.WriteLine($"Attempting {fileNumber} {fileName}...");

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

        if (matchesFull.Matches != null)
        {
            foreach (var match in matchesFull.Matches)
            {
                await outputService.SaveMatchAsync(
                    matchResultId,
                    match.MatchedLabel?.Name,
                    match.LabelGroupName,
                    match);
            }
        }

        Console.WriteLine($"Finished {fileNumber} {fileName}...");

        var licenceSets = await SchemaConverter.ToLicenceSetsAsync(
            matchesFull,
            licenceMapping,
            naldLicenceStatusData,
            naldData,
            pdfDataExtractor,
            pdfFolder,
            processRun.ProcessRunId);

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

(Dictionary<string, DmsFileData> FilepathsWithLicenceNumbers, Dictionary<string, DmsFileData> LicenceNumbersWithFilenames)
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
//        .Take(100)
//        .Where(x => x.Key.Contains("NE0270022023__Application type unknown Licence Issued - 29092011"))
        .Take(5)
        .ToDictionary(filePath => filePath.Key, filePath => filePath.Value);
    
    return filesAndMapping;
}

(Dictionary<string, DmsFileData> FilepathsWithLicenceNumbers, Dictionary<string, DmsFileData> LicenceNumbersWithFilenames)
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
                var destinationFileName = (string)row["DestinationFileName"];
                var permitNumberField = row["PermitNumber"];
                string permitNumber;
                
                if (permitNumberField is string permitNumberValue)
                {
                    permitNumber = permitNumberValue;
                }
                else
                {
                    permitNumber = ((double)permitNumberField).ToString(CultureInfo.InvariantCulture);
                }

                if (!filesInFolder.Contains(destinationFileName))
                {
                    continue;
                }

                var naldLicenceRef = (string)row["NALD Licence Ref"];
                
                var dmsFileData = new DmsFileData
                {
                    DestinationFileName = destinationFileName,
                    NaldLicenceRef = naldLicenceRef,
                    PermitNumber = permitNumber,
                    DmsPath = (string)row["FullPath"],
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

(Dictionary<string, string> FilepathsWithLicenceNumbers, Dictionary<string, string> LicenceNumbersWithFilenames)
    GetFilesAndMappingFromFolders(string pdfFolderPath, int regionCode)
{
    var filenames = GetPdfPathsWithLicenceNumbersFromFolders(pdfFolderPath);
    var missingMapping = filenames.Where(f => string.IsNullOrEmpty(f.Value)).ToList();

    var licenceNumberMapping = ExternalDataHelper
        .GetLicenceNumberMappingFromFilenames(pdfFolderPath, regionCode);
    
    if (missingMapping.Count == 0)
    {
        return (
            filenames.ToDictionary(k => k.Key, k => k.Value!),
            licenceNumberMapping
        );
    }
    
    var inverseLicenceNumberMapping = licenceNumberMapping
        .ToDictionary(i => i.Value, i => i.Key);

    foreach (var filename in missingMapping)
    {
        var filenameOnly = filename.Key.Split('/').Last();

        if (!inverseLicenceNumberMapping.TryGetValue(filenameOnly, out var value))
        {
            continue;
        }
        
        filenames[filename.Key] = value;
    }

    missingMapping = filenames.Where(f => string.IsNullOrEmpty(f.Value)).ToList();

    if (missingMapping.Count > 0)
    {
        throw new Exception($"Missing licence number mapping: {string.Join(", ", missingMapping.Select(mm => mm.Key))}");
    }
    
    return (
        filenames.ToDictionary(k => k.Key, k => k.Value!),
        licenceNumberMapping
    );
}

Dictionary<string, string?> GetPdfPathsWithLicenceNumbersFromFolders(string pdfFolderPath)
{
    var pdfFilePaths = FileHelper.GetRelevantFilesInFolder(pdfFolderPath);
    
    //var yorkshire = Yorkshire200Files();

    // YORKSHIRE 200 - From new files
    
    /*pdfFilePaths = pdfFilePaths.Where(filePath =>
    {
        var filename = filePath.Split('/').Last();
        
        return yorkshire.Contains(filename, StringComparer.InvariantCultureIgnoreCase);
    }).OrderBy(filename => filename).Skip(0).Take(10).ToList();*/
    
    // YORKSHIRE 6 - From original files

    /*pdfFilePaths = pdfFilePaths.Where(filePath =>
        filePath.Contains("2-26-32-126 6937559.PDF")
        || filePath.Contains("2-27-29-012 7003124.PDF")
        || filePath.Contains("Application - New - Licence Issued 30092021.pdf")
        || filePath.Contains("Application Formal Variation Issued Licence 07032023 (1).pdf")
        || filePath.Contains("Application Formal Variation Issued Licence 07032023.pdf")
        || filePath.Contains("Application Minor Variation Issued Licence 03.10.24.pdf")
    ).ToArray();*/
    
    // Any additional filtering
    
    /*pdfFilePaths = pdfFilePaths.Where(x =>
        // Orig 3
        x.Contains("11497061")
        || x.Contains("11149535")
        || x.Contains("11149440")
        
        // Some more
        || x.Contains("16022023")
        || x.Contains("08072024")
        || x.Contains("19122022")
        || x.Contains("11761845")
        ).ToArray();*/

    /*pdfFilePaths = pdfFilePaths.Where(x => 
        //x.Contains("12303008")
            
        x.Contains("12100004")
        ||x.Contains("12100052")
        ||x.Contains("12100065")
        ||x.Contains("12201014")
        ||x.Contains("12201021")
        ||x.Contains("12201023")
        ||x.Contains("12201078")
        ||x.Contains("12202043")
        ||x.Contains("12203007")
        ||x.Contains("12203045")
        ||x.Contains("12203120")
        ||x.Contains("12205021")
        ||x.Contains("12205044")
        
        ||x.Contains("12206039") // Pdf pages come through as pretty much blank
        ||x.Contains("12301067")
        ||x.Contains("12302006")
        ||x.Contains("12302044")
        ||x.Contains("12302207")
        ||x.Contains("12303008") // Not found
        ||x.Contains("12303075")
        
    ).ToList();*/

    return pdfFilePaths;
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

List<string> Yorkshire200Files()
{
    return
    [
        "22713185__Non-Application Licence Documents (20.12.1996).pdf",
        "22714090r01__Application Transfer Issued Licence 12 6 24 12 6 24.pdf",
        "22718033__Application - Minor Variation - Issued Licence - 16022023.pdf",
        "22718045__Application - Reduction -Application New Licence Issued 24_06_2019 00_00_00 10897641.pdf",
        "22718125R01__Application - NA Formal Variation - Issued Licence 31.03.21 11764153.pdf",
        "22718131r01__Application -New   licence - Issued Licence  - PDR- 15.12.2022.pdf",
        "22724197__Application - NA Formal Variation - Issued Licence 02112022.pdf",
        "NE0270012011__Application - New - Issued Licence 02.12.2013 8110044.pdf",
        "NE0270012049__Application – New Full   – Issued Licence 23122022.pdf",
        "ne0270018009__Application – Formal Variation – Issued Licence 19122022.pdf",
        "ne0270018020__Application - Minor Variation - Issued Licence - 16022023.pdf",
        "ne0270018023__Application - Minor Variation -Issued Licence - 08.11.2022.pdf",
        "ne0270018033__Application – Formal Variation – Issued Licence 1512022.pdf",
        "NE0270018041__Application NA New Issued Licence 26 03 2021 11761845.pdf",
        "22725124__Non-Application Licence Document (09.10.2008).pdf",
        "22727116__Application Formal Variation Issued Licence - 26092023.pdf",
        "22727278__Non-Application Licence Document (26.01.2009).pdf",
        "22727279__Non-Application Licence Document (26.01.2009).pdf",
        "ne0270025032__Application New Issued Licence 16.05.23.pdf",
        "NE0270025037__Application Formal Variation Issued Licence 16.05.23.pdf",
        "NE0270026005R01__Application Renewal Licence Issued - (25092024).pdf",
        "ne0270027009__Application Formal Variation Issued Licence 03.05.23.pdf",
        "ne0270028073__Application – NA New – Issued Licence 27092022.pdf",
        "NE0270028081__Application New License - License Issued - 18102024.pdf",
        "22704027r01__Application Formal Variation Issued Licence - [issued date] - (07062024).pdf",
        "22707004__Application - Transfer - Issued Licence 28.04.2017 9774748.pdf",
        "22708092__Application – NA Formal Variation – Issued Licence-10082022.pdf",
        "22709099__Application Minor Variation Licence issued 21.12.2018 10629856.pdf",
        "22709196r01__Application New Licence Issued - [22.03.2024] - (22.03.2024).pdf",
        "NE0270005031__Application New Issued Licence 17.04.23.pdf",
        "NE0270029007R01__Application Renewal Licence Issued - [issued date] - (11042024).pdf",
        "22631093__Application - Issued Licence [23-10-1978] 6075944.pdf",
        "22631097__Non-Application Licence Document (09.03.1988).pdf",
        "22631114__Application Formal Variation Issued Licence - [issued date] - (29082024).pdf",
        "22631168R01__Application Renewal Licence Issued - [issued date] - (09052024).pdf",
        "22632004__Application Minor Variation Issued Licence - 06122023.pdf",
        "22632235__Application Renewal - Licence Issued - 11112024.pdf",
        "22632344__Application - NA Formal Variation - Issued Licence 27102022.pdf",
        "22634031__Application - NA Formal Variation - Issued Licence 27102022.pdf",
        "22724007__Application minor variation issued Licence 22724007 11600563.pdf",
        "NE0260030016R01__Application Renewal - Licence Issued - 20112024.pdf",
        "NE0260031035__Application New Issued Licence 28.04.2023.pdf",
        "ne0260032055__Application - NA New - Issued Licence 15112022.pdf",
        "NE0260032058__Application NA New Licence Issued (Public Register) - 02122022 .pdf",
        "NE0260032074__Application  new  -licence issued  (08072024).pdf",
        "NE0260033011__Application - New -Application New Licence Issued 24_03_2020 00_00_00 11292824.pdf",
        "NE0260033017__Application Formal Variation - Licence Issued - (23052024).pdf",
        "NE0260034006__Application - Formal Variation -Application New Licence Issued 08_08_2019 00_00_00 10974057.pdf",
        "NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf",
        "NE0260034052__Application Apportionment Issued Licence 11.12.2019 11149440.pdf",
        "NE0260034056__Application New Issued Licence 10.09.2020 11497061.pdf",
        "NE0270024021R02__Application Renewal Licence Issued - 20062024.pdf",
        "22721238__Non-Application Licence Document (25.07.1977).pdf",
        "22721348r01__Application – NA Formal Variation – Issued Licence 13.07.2022.pdf",
        "22721356R01__Application Formal Variation Issued Licence 13.9.18 10487468.pdf",
        "22722128__Non-Application Licence Document (15.08.1988).pdf",
        "22722323__Non-Application Licence Document - Issued Licence - 22101998.pdf",
        "22722395A__Non-Application Licence Document (22.10.2001).pdf",
        "22722452__Non-Application Licence Document [Issued Licence] (26.2.01).pdf",
        "22722460__Application New Licence Issued [17.1.1992] (26.7.2010).pdf",
        "22722580r01__Application Transfer - Issued Licence 24092021.pdf",
        "22723556__Application - Formal Variation -Application New Licence Issued 12_04_2019 00_00_00 10797059.pdf",
        "ne0270021016__Application - Minor Variation -Application New Licence Issued 12_03_2021 00_00_00 11736007.pdf",
        "NE0270022058__Application New Issued Licence 18.05.23.pdf",
        "NE0270023043__Application New Licence Issued 18.12.2018 10623801.pdf",
        "NE0270023047__Application - New -Application New Licence Issued 06_04_2020 00_00_00 11303354.pdf",
        "22719149__Application Formal Variation - Issued Licence [04-09-2018] 10474343.pdf",
        "22719156__Application Formal Variation Licence Issued - 12102023.pdf",
        "22720093__Non-Application Licence Document (02.02.1998).pdf",
        "22720211__Non-Application Licence Document (01.12.1990).pdf",
        "22724371r01__Application NA Formal Variation Issued Licence 21122021.pdf",
        "NE0270020038__Application - New Licence Issued - Licence Issued - PDF - 28.10.2022.pdf",
        "NE0270020044__Application New Licence Issued - 20112024.pdf"
    ];
}