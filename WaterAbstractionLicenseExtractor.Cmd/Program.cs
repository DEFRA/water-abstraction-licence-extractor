using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tesseract;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Models.OutputSchema;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;

var pdfDataExtractors = new List<IPdfDataExtractorService>();

var maxConcurrentScrapers = int.Parse(Environment.GetEnvironmentVariable("ConcurrentCount")
    ?? throw new NullReferenceException("ConcurrentCount"));
var regenerateMappingJson = bool.Parse(Environment.GetEnvironmentVariable("REGENERATE_MAPPING_JSON")
    ?? throw new NullReferenceException("REGENERATE_MAPPING_JSON"));
var loadAiJs = bool.Parse(Environment.GetEnvironmentVariable("LOAD_AI_JS")
    ?? throw new NullReferenceException("LOAD_AI_JS"));
var pdfFolderPath = Environment.GetEnvironmentVariable("PdfFolderPath")
    ?? throw new NullReferenceException("PdfFolderPath");
var reportTemplatePath = Environment.GetEnvironmentVariable("ReportTemplatePath")
    ?? throw new NullReferenceException("ReportTemplatePath");
var outputFolder = Environment.GetEnvironmentVariable("OutputFolder")
    ?? throw new NullReferenceException("OutputFolder");
var cacheFolder = Environment.GetEnvironmentVariable("CacheFolder")
    ?? throw new NullReferenceException("CacheFolder");

await AllWork();
return;

async Task AllWork()
{
    Console.WriteLine("Started");

    const bool refreshCache = false;

    if (refreshCache)
    {
        // TODO clear out the Cache directory
    }

    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    Directory.CreateDirectory(outputFolder);
    Directory.CreateDirectory(cacheFolder);

    for (var idx = 0; idx < maxConcurrentScrapers; idx++)
    {
        var pdfPigNoOcr = new PdfPigNoOcrDataExtractorService();

        var tesseractOcrSparse = new TesseractOcrDataExtractorService(
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX")
                ?? throw new NullReferenceException("TESSDATA_PREFIX"),
            PageSegMode.SparseTextOsd);
        
        var tesseractOcrDefault = new TesseractOcrDataExtractorService(
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX")
                ?? throw new NullReferenceException("TESSDATA_PREFIX"),
            PageSegMode.Auto);

        var azureAiServices = new AzureAiVisionOcrDataExtractorService(
            Environment.GetEnvironmentVariable("AzureAIVisionEndpoint")
                ?? throw new NullReferenceException("AzureAIVisionEndpoint"),
            Environment.GetEnvironmentVariable("AzureAIVisionKey")
                ?? throw new NullReferenceException("AzureAIVisionKey"));

        var pdfDataExtractor = (IPdfDataExtractorService)new PdfDataExtractorService(
            pdfPigNoOcr,
            [
                tesseractOcrSparse,
                tesseractOcrDefault,
                azureAiServices
            ],
            pdfFolderPath);

        pdfDataExtractors.Add(pdfDataExtractor);
    }

    var outputLines = new List<IntermediateOutputLicence>();
    var processCount = 1;
    var completeNumber = 1;

    Copy(reportTemplatePath, outputFolder);

    var indexPath = $"{outputFolder}index.html";
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

    File.Move($"{outputFolder}report-template.html", $"{outputFolder}report.html", true);
    File.Move($"{outputFolder}licence-set-report-template.html", $"{outputFolder}licencesetreport.html", true);
    File.Move($"{outputFolder}list-template.html", indexPath, true);

    var indexHtml = await File.ReadAllTextAsync(indexPath);
    indexHtml = indexHtml.Replace("[LOAD_AI_JS]", loadAiJs.ToString().ToLower());

    await File.WriteAllTextAsync(indexPath, indexHtml);

    var licenceNumberMapping = GetLicenceNumberMapping();
    var impoundmentLicenceNumbers = GetImpoundmentLicenceNumbers();
    var deadLicenceNumbers = GetDeadLicenceNumbers();
    var liveLicenceNumbers = GetLiveLicenceNumbers();
    
    var licenceSetGroups = new List<IReadOnlyList<LicenceSet>>();
    List<LicenceSet> licenceSets;

    try
    {
        var scrapingTasks = new List<Task<List<LicenceSet>>>();

        foreach (var pdfFilePath in GetPdfPaths())
        {
            var scrapingTask = ScrapeDocumentAsync(
                pdfFilePath,
                processCount++,
                licenceNumberMapping,
                impoundmentLicenceNumbers,
                deadLicenceNumbers,
                liveLicenceNumbers);
            
            scrapingTasks.Add(scrapingTask);

            if (scrapingTasks.Count != maxConcurrentScrapers)
            {
                continue;
            }
            
            var licenceSetsTask = await Task.WhenAny(scrapingTasks);
            scrapingTasks.Remove(licenceSetsTask);

            licenceSets = await licenceSetsTask;
            licenceSetGroups.Add(licenceSets);
        }

        if (scrapingTasks.Any())
        {
            await Task.WhenAll(scrapingTasks);

            foreach (var scrapingTask in scrapingTasks)
            {
                licenceSets = await scrapingTask;
                licenceSetGroups.Add(licenceSets);
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

    licenceSets = SchemaConverter.AddGroupLicenceSetDetails(
        licenceSetGroups,
        impoundmentLicenceNumbers,
        deadLicenceNumbers,
        liveLicenceNumbers);
    
    var fileNumber = 1;
    
    foreach (var licenceSetGroup in licenceSetGroups)
    {
        if (licenceSetGroup.Count == 0)
        {
            // TODO log this - it shouldnt happen
            continue;
        }
        
        var licenceSet = licenceSetGroup.First();
        var licence = licenceSet.Licences.First();
        var folderName = FileHelper.GetFilenameWithoutExtensions(licence.Filename!);
        Directory.CreateDirectory($"{outputFolder}/{folderName}");

        var licenceJson = JsonHelper.GetAsString(licence);

        File.WriteAllText(
            $"{outputFolder}/{folderName}/licence.jsonp",
            $"var data2 = {licenceJson}");

        var licenceSetsFull = GetLicenceSetsForLicenceSetIds(licence.LicenceSets, licenceSets);
        var licenceSetsJson = JsonHelper.GetAsString(licenceSetsFull);

        File.WriteAllText(
            $"{outputFolder}/{folderName}/licence-sets.jsonp",
            $"var licenceSets = {licenceSetsJson}");

        var outputLine = ToOutputLine(
            licence,
            DateTime.Now,
            completeNumber++,
            fileNumber++,
            licenceSets);

        outputLines.Add(outputLine);
    }

    var resultFileStringBuilder = new StringBuilder(
        "LineNumber,StartNumber,Filename,Text,OCR,ServiceName,Certainty,MatchType,Duration,MatchedLabelText," +
        "MatchedLabelPosition,LicenceNumber,LimitsFound,LinkedLicenceNumbers,LinkedLicenceNumbersExistInDataset");

    var mappingFileStringBuilder = new StringBuilder(
        "Filename,LicenceNumber");

    var filenameToLicenceNumberMap = new Dictionary<string, string>();
    var licenceNumberToFilenameMap = new Dictionary<string, string>();

    var nodeIndex = 1;
    var nodesDictionaries = new List<Dictionary<string, object>>();
    var linksDictionaries = new List<Dictionary<string, object>>();

    var listJs = new List<OutputListDataItem>();

    foreach (var outputLine in outputLines.OrderBy(x => x.Filename))
    {
        var anyLinkedLicenceNumbers = outputLine.LinkedLicences?
            .Any(lln => outputLines.Count(ol => ol.LicenceNumber == lln.LicenceNumber) > 0);

        Log(
            $"\n{outputLine.LineNumber},{outputLine.StartNumber},{outputLine.Filename}," +
            $"\"{outputLine.LicenceHolder}\",{outputLine.Ocr},{outputLine.ServiceName},{outputLine.Certainty}," +
            $"{outputLine.MatchType},{outputLine.Duration},{outputLine.MatchedLabelText}," +
            $"{outputLine.MatchedLabelPosition},{outputLine.LicenceNumber},{outputLine.LimitsCount}," +
            $"{outputLine.LinkedLicences},{anyLinkedLicenceNumbers}",
            resultFileStringBuilder);

        var filename = FileHelper.GetFilenameWithoutExtensions(outputLine.Filename!);

        var listRow = new OutputListDataItem
        {
            imagePath = $"{filename}/PdfPig/Images/page-1.jpg",
            filename = filename,
            licenceNumber =
                $"{outputLine.LicenceNumber}{ToPercent(outputLine.LicenceNumberOcrConfidence, outputLine.Ocr)}<br><span style=\"color: silver\">{outputLine.NaldLicenceNumber}</span>",
            licenceHolder =
                $"{outputLine.LicenceHolder?.Replace("\"", "\\\"")}{ToPercent(outputLine.LicenceHolderOcrConfidence, outputLine.Ocr)}",
            purposes = outputLine.Purposes,
            points = outputLine.Points,
            limitsCount = outputLine.LimitsCount,
            aggregatesCount = outputLine.AggregatesCount,
            ocr = outputLine.Ocr == "OCR",
            issueDate = outputLine.IssueDate,
            issuer = outputLine.Issuer,
            meansFound = outputLine.MeansFound,
            status = outputLine.Status,
            linkedLicences = outputLine.LinkedLicences?.OrderBy(x => x.LicenceNumber).ToArray() ?? [],
            licenceSets = outputLine.LicenceSetReferences?.Select(lsr =>
            {
                var ls = outputLine.LicenceSets!.First(ls1 => ls1.LicenceSetId == lsr.LicenceSetId);
                var licenceSetType = lsr.LicenceSetType;

                return new OutputListDataItemLicenceSet
                {
                    LicenceSetId = ls.LicenceSetId,
                    ShortLicenceSetId = ls.ShortLicenceSetId,
                    LicenceSetTypes = ls.LicenceSetTypes,
                    LicenceSetType = licenceSetType

                };
            }).ToArray() ?? []
        };

        listJs.Add(listRow);

        if (outputLine.LicenceNumber != null)
        {
            filenameToLicenceNumberMap.TryAdd(outputLine.Filename!, outputLine.LicenceNumber);
            licenceNumberToFilenameMap.TryAdd(outputLine.LicenceNumber, outputLine.Filename!);
        }

        outputLine.NodeId = nodeIndex++;
        var nodeName = outputLine.Filename!;

        if (!string.IsNullOrEmpty(outputLine.LicenceNumber) && outputLine.LicenceNumber != string.Empty)
        {
            nodeName = outputLine.LicenceNumber;
        }

        nodesDictionaries.Add(new Dictionary<string, object>
        {
            { "id", outputLine.NodeId },
            { "name", nodeName }
        });

        Log($"\n{outputLine.Filename},{outputLine.LicenceNumber}", mappingFileStringBuilder);
    }

    foreach (var outputLine in outputLines)
    {
        if (outputLine.LinkedLicences != null)
        {
            foreach (var linkedLicence in outputLine.LinkedLicences)
            {
                var linkedOutputLine = outputLines.FirstOrDefault(x => x.LicenceNumber == linkedLicence.LicenceNumber);

                if (linkedOutputLine != null)
                {
                    linksDictionaries.Add(new Dictionary<string, object>
                    {
                        { "source", outputLine.NodeId },
                        { "target", linkedOutputLine.NodeId }
                    });
                }
            }
        }
    }

    Directory.CreateDirectory($"{outputFolder}Additional");

    var resultFile = $"{outputFolder}Additional/{DateTime.Today:yyyyMMdd}-result.csv";
    File.WriteAllText(resultFile, resultFileStringBuilder.ToString());

#pragma warning disable CS0162 // Unreachable code detected
    if (regenerateMappingJson)
    {
        var licenceFilenameMapFile = $"{outputFolder}Additional/licence-number-filename-map.csv";
        File.WriteAllText(licenceFilenameMapFile, mappingFileStringBuilder.ToString());

        var licenceFilenameMapJsonFile = $"{outputFolder}Additional/licence-number-filename-map.jsonp";
        var licenceFilenameMapDictionary = new Dictionary<string, object>
        {
            { "filenameToLicenceNumber", filenameToLicenceNumberMap },
            { "licenceNumberToFilename", licenceNumberToFilenameMap }
        };

        File.WriteAllText(licenceFilenameMapJsonFile,
            $"var mapData = {JsonSerializer.Serialize(licenceFilenameMapDictionary, jsonOptions)};");
    }
#pragma warning restore CS0162 // Unreachable code detected

    var jsListFilePath = $"{outputFolder}list-data.js";
    File.WriteAllText(jsListFilePath, "var data = " + JsonSerializer.Serialize(listJs, jsonOptions) + ";");

    var nodeGraphData = new Dictionary<string, List<Dictionary<string, object>>>
    {
        {
            "nodes",
            nodesDictionaries
        },
        {
            "links",
            linksDictionaries
        }
    };

    var nodeGraphDataFile = $"{outputFolder}Additional/node-graph-data.jsonp";
    File.WriteAllText(nodeGraphDataFile,
        $"var data = {JsonSerializer.Serialize(nodeGraphData, jsonOptions)};");
}

List<LicenceSet> GetLicenceSetsForLicenceSetIds(IReadOnlyList<LicenceSetReference> licenceSetIds, IReadOnlyList<LicenceSet> licenceSets)
{
    var returnList = new List<LicenceSet>();

    foreach (var licenceSet in licenceSets)
    {
        if (licenceSetIds.All(lsi => lsi.LicenceSetId != licenceSet.LicenceSetId))
        {
            continue;
        }
        
        returnList.Add(licenceSet);
    }
    
    return returnList;
}

Dictionary<string, string> GetLicenceNumberMapping()
{
    var fileMappingPath = Environment.GetEnvironmentVariable("FileMappingPath")
        ?? throw new NullReferenceException("FileMappingPath");
    
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

async Task<List<LicenceSet>> ScrapeDocumentAsync(
    string pdfFilePath,
    int fileNumber,
    Dictionary<string, string> licenceNumberMapping,
    HashSet<string> impoundmentLicenceNumbers,
    HashSet<string> deadLicenceNumbers,
    HashSet<string> liveLicenceNumbers)
{
    var fileName = pdfFilePath.Split('/').Last();

    Console.WriteLine($"Attempting {fileNumber} {fileName}...");
    var pdfDataExtractor = pdfDataExtractors.First(x => !x.InUse);
    pdfDataExtractor.InUse = true;
    
    try
    {
        var previouslyParsedPaths = new List<string>
        {
            pdfFilePath
        };

        var pdfFolder = pdfFilePath.Substring(0, pdfFilePath.LastIndexOf('/') + 1);

        var lookupConfig = new LookupConfiguration(
            LabelConfiguration.GetLabels(),
            licenceNumberMapping,
            outputFolder,
            cacheFolder);
        
        var matchesFull = await pdfDataExtractor.GetMatchesAsync(
            pdfFilePath,
            lookupConfig,
            previouslyParsedPaths);
        
        var licenceSets = await SchemaConverter.ToLicenceSetsAsync(
            matchesFull,
            licenceNumberMapping,
            impoundmentLicenceNumbers,
            deadLicenceNumbers,
            liveLicenceNumbers,
            pdfDataExtractor,
            outputFolder,
            cacheFolder,
            pdfFolder);

        var internalJson = JsonHelper.GetAsString(matchesFull);
    
        var folderName = FileHelper.GetFilenameWithoutExtensions(pdfFilePath);
        Directory.CreateDirectory($"{outputFolder}/{folderName}");
    
        File.WriteAllText(
            $"{outputFolder}/{folderName}/internal.jsonp",
            $"var data = {internalJson}");
    
        Console.WriteLine($"Finished {fileNumber} {fileName}...");
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

static IntermediateOutputLicence ToOutputLine(Licence licence, DateTime dtStart, int completeNumber, int fileNumber, List<LicenceSet> allLicenceSets)
{
    var licenceHolder = licence.NoneSchemaData.TryGetValue("issuedTo", out var value4)
        ? (string)value4 : null;
    var licenceHolderOcrConfidence = licence.NoneSchemaData.TryGetValue("issuedToConfidence", out var value5)
        ? (double)value5 : (double?)null;
    var ocr = licence.NoneSchemaData.TryGetValue("ocr", out var value6)
        ? (string)value6 : "--";
    var serviceName = licence.NoneSchemaData.TryGetValue("servicesUsed", out var value7)
        ? ((string[])value7).FirstOrDefault() : "PdfPig";

    var durationInMSeconds = (int) (DateTime.Now - dtStart).TotalMilliseconds;

    var licenceNumber = licence.LicenceNumber;
    var licenceNumberOcrConfidence = licence.NoneSchemaData.TryGetValue("licenceNumberConfidence", out var value8)
        ? (double)value8 : (double?)null;

    var meansFound = licence.MeansOfAbstraction.Length > 0;
    var status = "Not Found";

    if (licence.LicenceFoundInList)
    {
        status = "Found";
    }
    
    if (licence.IsLiveLicence == true)
    {
        status = "Live";
    }

    if (licence.IsDeadLicence == true)
    {
        status = "Dead";
    }

    if (licence.IsImpoundmentLicence == true)
    {
        status = "Impound";
    }
    
    var issueDate = licence.LicenceVersion.IssueDate?.ToString("yyyy-MM-dd");
    var issuer = licence.LicenceVersion.Issuer;

    var licenceSets = licence.LicenceSets
        .Select(lsi =>
        {
            return allLicenceSets.FirstOrDefault(ls => ls.LicenceSetId == lsi.LicenceSetId);
        })
        .Where(ls => ls != null)
        .Select(ls => ls!)
        .ToList();
    
    return new IntermediateOutputLicence
    {
        LineNumber = completeNumber,
        StartNumber = fileNumber,
        Filename = licence.Filename,
        LicenceHolder = licenceHolder,
        LicenceHolderOcrConfidence = licenceHolderOcrConfidence,
        Ocr = ocr,
        Purposes = licence.Purposes.Select(p => p.Description).ToArray(),
        Points = licence.Points.Select(p => p.Description).ToArray(),
        ServiceName = serviceName,
        Certainty = licence.NoneSchemaData.TryGetValue("issuedToCertainty", out var value) ? (int)value : -1,
        MatchType = licence.NoneSchemaData.TryGetValue("issuedToMatchType", out var value1) ? (string)value1 : "N/A",
        Duration = durationInMSeconds,
        MatchedLabelText = licence.NoneSchemaData.TryGetValue("issuedToMatchedLabelText", out var value2) ? (string)value2 : null,
        MatchedLabelPosition = licence.NoneSchemaData.TryGetValue("issuedToMatchLabelPosition", out var value3) ? (string)value3 : null,
        LicenceNumber = licenceNumber,
        NaldLicenceNumber = licence.NaldLicenceNumber,
        LicenceNumberOcrConfidence = licenceNumberOcrConfidence,
        LimitsCount = licence.AbstractionLimits.Individual?.Sum(x => x.Limits.Count) ?? 0,
        AggregatesCount = licence.AbstractionLimits.Aggregates?.Sum(x => x.Limits.Count) ?? 0,
        IssueDate = issueDate,
        Issuer = !string.IsNullOrEmpty(issuer) ? issuer : string.Empty,
        MeansFound = meansFound,
        Status = status,
        LinkedLicences = licence.LinkedLicences,
        LicenceSets = licenceSets,
        LicenceSetReferences = licence.LicenceSets
    };
}

static string ToPercent(double? value, string? ocr)
{
    if (ocr != "OCR")
    {
        return string.Empty;
    }
    
    if (value == null)
    {
        return string.Empty;
    }

    return " (" + Math.Round(value.Value, 1) + "%)";
}

IEnumerable<string> GetPdfPaths()
{
    var pdfFilePaths = Directory
        .GetFiles(pdfFolderPath)
        .Where(fileName => fileName.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase));

    var yorkshire = Yorkshire200Files();

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
        x.Contains("11497061")
        || x.Contains("11149535")
        || x.Contains("11149440")
        
        || x.Contains("16022023")
        || x.Contains("08072024")
        || x.Contains("19122022")
        || x.Contains("11761845")
        ).ToArray();*/

    //pdfFilePaths = pdfFilePaths.Where(x => x.Contains("22634066A")).ToList();
    pdfFilePaths = pdfFilePaths.OrderBy(x => x).Skip(0).Take(10).ToList();
    
    return pdfFilePaths;
}

void Log(string message, StringBuilder outputStringBuilder)
{
    //Console.WriteLine(message);
    outputStringBuilder.Append(message);
}

void Copy(string sourceDir, string targetDir)
{
    Directory.CreateDirectory(targetDir);

    foreach(var file in Directory.GetFiles(sourceDir))
        File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);

    foreach(var directory in Directory.GetDirectories(sourceDir))
        Copy(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
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