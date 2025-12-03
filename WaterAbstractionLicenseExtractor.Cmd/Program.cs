using Tesseract;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Database.Services;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
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
    var fileMappingPath = services.FileMappingPath!;
    
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
    
    var licenceNumberMapping = GetLicenceNumberMapping(fileMappingPath);
    var impoundmentLicenceNumbers = GetImpoundmentLicenceNumbers();
    var deadLicenceNumbers = GetDeadLicenceNumbers();
    var liveLicenceNumbers = GetLiveLicenceNumbers();
    
    var pdfPaths = GetPdfPaths(services.PdfFolderPath!);
    
    var processRun = await outputService.SaveProcessRunAsync(new ProcessRun
    {
        Description = $"Run using {services.PdfFolderPath}",
        StartDateTimeUtc = DateTime.UtcNow,
        NumberOfFiles = pdfPaths.Count
    });
    
    var licenceSetGroups = new List<IReadOnlyList<LicenceSet>>();
    List<LicenceSet> allLicenceSets;
    
    try
    {
        var scrapingTasks = new List<Task<List<LicenceSet>>>();
        var processCount = 1;
        var minimumToFreeUp = maxConcurrentScrapers / 3;
        
        foreach (var pdfFilePath in pdfPaths)
        {
            scrapingTasks.Add(ScrapeDocumentAsync(
                pdfFilePath,
                processCount++,
                licenceNumberMapping,
                impoundmentLicenceNumbers,
                deadLicenceNumbers,
                liveLicenceNumbers,
                outputService,
                pdfDataExtractors,
                licenceNumberMapping,
                processRun));

            if (scrapingTasks.Count != maxConcurrentScrapers)
            {
                continue;
            }

            while (scrapingTasks.Count > maxConcurrentScrapers - minimumToFreeUp)
            {
                var licenceSetsTask = await Task.WhenAny(scrapingTasks);
                scrapingTasks.Remove(licenceSetsTask);

                allLicenceSets = await licenceSetsTask;
                licenceSetGroups.Add(allLicenceSets);   
            }
        }

        if (scrapingTasks.Any())
        {
            await Task.WhenAll(scrapingTasks);

            foreach (var scrapingTask in scrapingTasks)
            {
                allLicenceSets = await scrapingTask;
                licenceSetGroups.Add(allLicenceSets);
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
    
    allLicenceSets = SchemaConverter.AddAdditionalLicenceSets(
        licenceSetGroups,
        impoundmentLicenceNumbers,
        deadLicenceNumbers,
        liveLicenceNumbers);
    
    Console.WriteLine($"Converted into all licence sets at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    
    var outputLines = new List<IntermediateOutputLicence>();

    var fileNumber = 1;
    var completeNumber = 1;

    var savedLicenceNumbers = new Dictionary<string, int>();
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
                if (licenceLoop.LicenceNumber != null
                    && !savedLicenceNumbers.TryGetValue(licenceLoop.LicenceNumber, out _))
                {
                    var loopLicenceId =
                        await outputService.SaveLicenceAsync(licenceLoop, licenceLoop.Filename!,
                            processRun.ProcessRunId);

                    savedLicenceNumbers.Add(licenceLoop.LicenceNumber, loopLicenceId);
                    licenceLoop.NoneSchemaData.Add("licenceId", loopLicenceId);
                }
                
                var licenceSetsLoop = GetLicenceSetsForLicenceSetIds(
                    licenceLoop.LicenceSets,
                    allLicenceSets);

                var newLicenceSetsLoop = new Dictionary<string, LicenceSet>();
                
                foreach (var kvp in licenceSetsLoop)
                {
                    if (savedLicenceSetIds.Contains(kvp.Key))
                    {
                        continue;
                    }
                    
                    newLicenceSetsLoop.Add(kvp.Key, kvp.Value);
                    savedLicenceSetIds.Add(kvp.Key);
                }
                
                await outputService.SaveLicenceSetsAsync(
                    newLicenceSetsLoop,
                    licenceLoop.Filename!,
                    processRun.ProcessRunId);  
            }
        }

        var licence = licenceSetGroup.First().Licences.First();
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
    await outputService.FinishProcessRunAsync(processRun);
    
    Console.WriteLine($"Finished processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    Console.Write($"Finished all in {(processRun.EndDateTimeUtc.Value - processRun.StartDateTimeUtc!.Value).TotalSeconds} seconds - process run id {processRun.ProcessRunId}");
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
    var fileMappingPath = Environment.GetEnvironmentVariable("FileMappingPath")
        ?? throw new NullReferenceException("FileMappingPath");
    var outputFolder = Environment.GetEnvironmentVariable("OutputFolder")
        ?? throw new NullReferenceException("OutputFolder");
    var cacheFolder = Environment.GetEnvironmentVariable("CacheFolder")
        ?? throw new NullReferenceException("CacheFolder");
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
    var sqlConnectionString = Environment.GetEnvironmentVariable("SqlConnectionString")
        ?? throw new NullReferenceException("SqlConnectionString");
    
    var databaseReadService = new SqlSeverReadService(sqlConnectionString);
    var databaseAddService = new SqlSeverWriteService(sqlConnectionString);
    
    var cacheService = new DatabaseCacheService(databaseReadService, databaseAddService);
    var outputService = new DatabaseOutputService(databaseReadService, databaseAddService);
    
    var pdfDataExtractors = new List<IPdfDataExtractorService>();
    
    for (var idx = 0; idx < maxConcurrentScrapers; idx++)
    {
        var pdfPigNoOcr = new PdfPigNoOcrDataExtractorService();

        var tesseractOcrSparse = new TesseractOcrDataExtractorService(
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX")
            ?? throw new NullReferenceException("TESSDATA_PREFIX"),
            PageSegMode.SparseTextOsd,
            cacheService,
            outputService);
        
        var tesseractOcrDefault = new TesseractOcrDataExtractorService(
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX")
            ?? throw new NullReferenceException("TESSDATA_PREFIX"),
            PageSegMode.Auto,
            cacheService,
            outputService);

        var azureAiServices = new AzureAiVisionOcrDataExtractorService(
            Environment.GetEnvironmentVariable("AzureAIVisionEndpoint")
            ?? throw new NullReferenceException("AzureAIVisionEndpoint"),
            Environment.GetEnvironmentVariable("AzureAIVisionKey")
            ?? throw new NullReferenceException("AzureAIVisionKey"),
            cacheService,
            outputService);

        var pdfDataExtractor = (IPdfDataExtractorService)new PdfDataExtractorService(
            pdfPigNoOcr,
            [
                tesseractOcrSparse,
                tesseractOcrDefault,
                azureAiServices
            ],
            cacheService,
            outputService,
            pdfFolderPath);

        pdfDataExtractors.Add(pdfDataExtractor);
    }
    
    return new ConfiguredServices
    {
        CacheService = cacheService,
        OutputService = outputService,
        PdfDataExtractorServices = pdfDataExtractors,
        FileMappingPath = fileMappingPath,
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
        RefreshCache = refreshCache
    };
}

async Task<List<LicenceSet>> ScrapeDocumentAsync(
    string pdfFilePath,
    int fileNumber,
    Dictionary<string, string> licenceMapping,
    HashSet<string> impoundmentLicenceNumbers,
    HashSet<string> deadLicenceNumbers,
    HashSet<string> liveLicenceNumbers,
    IOutputService outputService,
    List<IPdfDataExtractorService> pdfDataExtractors,
    Dictionary<string, string> fileLicenceMapping,
    ProcessRun processRun)
{
    var fileName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);

    Console.WriteLine($"Attempting {fileNumber} {fileName}...");
    var pdfDataExtractor = pdfDataExtractors.First(x => !x.InUse);
    pdfDataExtractor.InUse = true;
    
    try
    {
        var previouslyParsedPaths = new List<string>
        {
            pdfFilePath
        };

        var pdfFolder = pdfFilePath[..(pdfFilePath.LastIndexOf('/') + 1)];

        var lookupConfig = new LookupConfiguration(
            LabelConfiguration.GetLabels(),
            licenceMapping);
        
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
            fileLicenceMapping,
            impoundmentLicenceNumbers,
            deadLicenceNumbers,
            liveLicenceNumbers,
            pdfDataExtractor,
            pdfFolder,
            processRun.ProcessRunId);
        
        return licenceSets;
    }
    catch (Exception ex)
    {
        // TODO log
        return [];
    }
    finally
    {
        pdfDataExtractor.InUse = false;
    }
}

Dictionary<string, string> GetLicenceNumberMapping(string fileMappingPath)
{
    var returnMapping = new Dictionary<string, string>();

    var fileContents = File.Exists(fileMappingPath)
        ? File.ReadAllText(fileMappingPath)
            .Replace("\r", string.Empty)
            .Split('\n')
        : [];

    var count = 0;
    foreach (var line in fileContents)
    {
        if (count++ == 0)
        {
            continue;
        }

        var parts = line.Split(',');
        var licenceNumber = parts[1];
        var filename = parts[0].Split('/').Last();

        if (!returnMapping.TryAdd(licenceNumber, filename))
        {
            returnMapping[licenceNumber] = filename;
        }
    }

    return returnMapping;
}

HashSet<string> GetLiveLicenceNumbers()
{
    var liveLicencesPath = Environment.GetEnvironmentVariable("LiveLicencesPath")
        ?? throw new NullReferenceException("LiveLicencesPath");

    var returnList = new HashSet<string>();

    var fileContents = File.Exists(liveLicencesPath)
        ? File.ReadAllText(liveLicencesPath)
            .Replace("\r", string.Empty)
            .Split('\n')
        : [];

    var count = 0;
    foreach (var line in fileContents)
    {
        if (count++ == 0)
        {
            continue;
        }

        var parts = line.Split(',');

        if (parts.Length < 3)
        {
            continue;
        }

        var licenceNumber = parts[2];
        returnList.Add(licenceNumber);
    }

    return returnList;
}

HashSet<string> GetDeadLicenceNumbers()
{
    var deadLicencesPath = Environment.GetEnvironmentVariable("DeadLicencesPath")
        ?? throw new NullReferenceException("DeadLicencesPath");

    var returnList = new HashSet<string>();

    var fileContents = File.Exists(deadLicencesPath)
        ? File.ReadAllText(deadLicencesPath)
            .Replace("\r", string.Empty)
            .Split('\n')
        : [];

    var count = 0;
    foreach (var line in fileContents)
    {
        if (count++ == 0)
        {
            continue;
        }

        var parts = line.Split(',');

        if (parts.Length < 6)
        {
            continue;
        }

        var licenceNumber = parts[5];
        returnList.Add(licenceNumber);
    }

    return returnList;
}

HashSet<string> GetImpoundmentLicenceNumbers()
{
    var impoundmentLicencesPath = Environment.GetEnvironmentVariable("ImpoundmentLicencesPath")
        ?? throw new NullReferenceException("ImpoundmentLicencesPath");

    var returnList = new HashSet<string>();

    var fileContents = File.Exists(impoundmentLicencesPath)
        ? File.ReadAllText(impoundmentLicencesPath)
            .Replace("\r", string.Empty)
            .Split('\n')
        : [];

    var count = 0;
    foreach (var line in fileContents)
    {
        if (count++ == 0)
        {
            continue;
        }

        var parts = line.Split(',');
        var licenceNumber = parts[0];

        returnList.Add(licenceNumber);
    }

    return returnList;
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

IReadOnlyList<string> GetPdfPaths(string pdfFolderPath)
{
    var pdfFilePaths = FileHelper.GetFiles(pdfFolderPath);
    
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
    pdfFilePaths = pdfFilePaths.Where(x => x.Contains("12504008__Application - Minor Variation - Issued Licence PDF Copy 9211405")).ToList();
    pdfFilePaths = pdfFilePaths.OrderBy(x => x).Skip(0).Take(500).ToList();
    
    return pdfFilePaths.ToList();
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