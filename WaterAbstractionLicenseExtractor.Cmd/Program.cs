using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Models.OutputSchema;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

Console.WriteLine("Started");

var concurrentCount = int.Parse(Environment.GetEnvironmentVariable("ConcurrentCount")
    ?? throw new NullReferenceException("ConcurrentCount"));

var regenerateMappingJson = bool.Parse(Environment.GetEnvironmentVariable("REGENERATE_MAPPING_JSON")
    ?? throw new NullReferenceException("REGENERATE_MAPPING_JSON"));

var loadAiJs = bool.Parse(Environment.GetEnvironmentVariable("LOAD_AI_JS")
    ?? throw new NullReferenceException("LOAD_AI_JS"));

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

var pdfDataExtractors = new List<IPdfDataExtractorService>();

for (var idx = 0; idx < concurrentCount; idx++)
{
    var pdfPigNoOcr = new PdfPigNoOcrDataExtractorService();

    var tesseractOcr = new TesseractOcrDataExtractorService(
        Environment.GetEnvironmentVariable("TESSDATA_PREFIX")
            ?? throw new NullReferenceException("TESSDATA_PREFIX"));

    var azureAiServices = new AzureAiVisionOcrDataExtractorService(
        Environment.GetEnvironmentVariable("AzureAIVisionEndpoint")
            ?? throw new NullReferenceException("AzureAIVisionEndpoint"),
        Environment.GetEnvironmentVariable("AzureAIVisionKey")
            ?? throw new NullReferenceException("AzureAIVisionKey"));

    var pdfDataExtractor = (IPdfDataExtractorService)new PdfDataExtractorService(
        pdfPigNoOcr,
        [
            tesseractOcr,
            azureAiServices
        ],
        pdfFolderPath);

    pdfDataExtractors.Add(pdfDataExtractor);
}

var outputLines = new List<OutputLine>();
var processCount = 1;
var completeNumber = 1;

var fileLicenceMapping = new Dictionary<string, string>();

var fileMappingContents = File.Exists(fileMappingPath)
    ? File.ReadAllText(fileMappingPath)
        .Replace("\r", string.Empty)
        .Split('\n')
    : [];

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
File.Move($"{outputFolder}list-template.html", indexPath, true);

var indexHtml = await File.ReadAllTextAsync(indexPath);
indexHtml = indexHtml.Replace("[LOAD_AI_JS]", loadAiJs.ToString().ToLower());

await File.WriteAllTextAsync(indexPath, indexHtml);

var count = 0;
foreach (var line in fileMappingContents)
{
    if (count++ == 0)
    {
        continue;
    }
    
    var parts = line.Split(',');
    var licenceNumber = parts[1];
    var filename = parts[0].Split('/').Last();

    if (!fileLicenceMapping.TryAdd(licenceNumber, filename))
    {
        fileLicenceMapping[licenceNumber] = filename;
    }
}

var allLicences = new List<Licence>();

try
{
    var processingTasks = new List<Task<Licence?>>();
    
    foreach (var pdfFilePath in GetPdfPaths())
    {
        processingTasks.Add(HandleFileAsync(pdfFilePath, processCount++, fileLicenceMapping));

        if (processingTasks.Count == concurrentCount)
        {
            var licenceTask = await Task.WhenAny(processingTasks);
            processingTasks.Remove(licenceTask);

            var licence = await licenceTask;

            if (licence != null)
            {
                allLicences.Add(licence);
            }
        }
    }

    if (processingTasks.Count > 0)
    {
        await Task.WhenAll(processingTasks);

        foreach (var processingTask in processingTasks)
        {
            var licence = await processingTask;

            if (licence != null)
            {
                allLicences.Add(licence);
            }
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

SchemaConverter.AddMissingBackLinks(allLicences);

var resultFileStringBuilder = new StringBuilder(
    "LineNumber,StartNumber,Filename,Text,OCR,ServiceName,Certainty,MatchType,Duration,MatchedLabelText," +
    "MatchedLabelPosition,LicenceNumber,LimitsFound,LinkedLicenceNumbers,LinkedLicenceNumbersExistInDataset");

var mappingFileStringBuilder = new StringBuilder(
    "Filename,LicenceNumber");

var filenameToLicenceNumberMap = new Dictionary<string, string>();
var licenceNumberToFilenameMap = new Dictionary<string, string>();
var fileCount = 1;

var licenceNumberFoundCount = 0;
var licenceHolderFoundCount = 0;
var scannedCount = 0;
var purposesFoundCount = 0;
var pointsFoundCount = 0;
var limitsFoundCount = 0;
var aggregatesFoundCount = 0;
var issueDateFoundCount = 0;
var issuerFoundCount = 0;
var linkedLicenceNumbersFoundCount = 0;
var meansFoundCount = 0;

var nodeIndex = 1;
var nodesDictionaries = new List<Dictionary<string, object>>();
var linksDictionaries = new List<Dictionary<string, object>>();

var listJsStringBuilder = new StringBuilder("var data = [\n");
var first = true;

foreach (var outputLine in outputLines.OrderBy(x => x.Filename))
{
    var anyLinkedLicenceNumbers = outputLine.LinkedLicenceNumbers?
        .Split('|')
        .Any(lln => outputLines.Count(ol => ol.LicenceNumber == lln) > 0) ?? false;
            
    Log(
        $"\n{outputLine.LineNumber},{outputLine.StartNumber},{outputLine.Filename}," +
        $"\"{outputLine.LicenceHolder}\",{outputLine.Ocr},{outputLine.ServiceName},{outputLine.Certainty}," +
        $"{outputLine.MatchType},{outputLine.Duration},{outputLine.MatchedLabelText}," +
        $"{outputLine.MatchedLabelPosition},{outputLine.LicenceNumber},{outputLine.LimitsCount}," +
        $"{outputLine.LinkedLicenceNumbers},{anyLinkedLicenceNumbers}",
        resultFileStringBuilder);
    
    var filename = FileHelper.GetFilenameWithoutExtensions(outputLine.Filename!);
    var filenameForScreen = outputLine.Filename;

    if (filenameForScreen?.Length > 30)
    {
        filenameForScreen = filenameForScreen[..30] + "-<br>" + filenameForScreen[30..];
    }

    if (first)
    {
        first = false;
    }
    else
    {
        listJsStringBuilder.AppendLine(",");
    }

    var linkedLicencesSb = new StringBuilder();
    linkedLicencesSb.Append("[");

    if (!string.IsNullOrEmpty(outputLine.LinkedLicenceNumbers) && outputLine.LinkedLicenceNumbers != string.Empty)
    {
        first = true;
        
        foreach (var linkedLicenceNumber in outputLine.LinkedLicenceNumbers.Split('|'))
        {
            if (first)
            {
                first = false;
            }
            else
            {
                linkedLicencesSb.Append(",");
            }
            
            linkedLicencesSb.Append($"\"{linkedLicenceNumber}\"");
        }
    }
    
    linkedLicencesSb.Append("]");
    
    listJsStringBuilder.AppendLine("\t{");
    listJsStringBuilder.AppendLine($"\t\t\"imagePath\": \"{filename}/PdfPig/Images/page-1.jpg\",");
    listJsStringBuilder.AppendLine($"\t\t\"filename\": \"{filename}\",");
    listJsStringBuilder.AppendLine($"\t\t\"licenceNumber\": \"{outputLine.LicenceNumber}{ToPercent(outputLine.LicenceNumberOcrConfidence, outputLine.Ocr)}\",");
    listJsStringBuilder.AppendLine($"\t\t\"licenceHolder\": \"{outputLine.LicenceHolder}{ToPercent(outputLine.LicenceHolderOcrConfidence, outputLine.Ocr)}\",");
    listJsStringBuilder.AppendLine($"\t\t\"purposes\": {outputLine.Purposes},");
    listJsStringBuilder.AppendLine($"\t\t\"points\": {outputLine.Points},");
    listJsStringBuilder.AppendLine($"\t\t\"limitsCount\": {outputLine.LimitsCount},");
    listJsStringBuilder.AppendLine($"\t\t\"aggregatesCount\": {outputLine.AggregatesCount},");
    listJsStringBuilder.AppendLine($"\t\t\"ocr\": {(outputLine.Ocr == "OCR").ToString().ToLower()},");
    listJsStringBuilder.AppendLine($"\t\t\"issueDate\": \"{outputLine.IssueDate}\",");
    listJsStringBuilder.AppendLine($"\t\t\"issuer\": \"{outputLine.Issuer}\",");
    listJsStringBuilder.AppendLine($"\t\t\"meansFound\": {outputLine.MeansFound.ToString().ToLower()},"); 
    listJsStringBuilder.AppendLine($"\t\t\"linkedLicences\": {linkedLicencesSb.ToString()},");
    listJsStringBuilder.Append("\t}");
    
    if (!string.IsNullOrEmpty(outputLine.LicenceNumber)
        && outputLine.LicenceNumber != string.Empty) licenceNumberFoundCount++;
    if (!string.IsNullOrEmpty(outputLine.LicenceHolder)
        && outputLine.LicenceHolder != string.Empty) licenceHolderFoundCount++;
    if (outputLine.Ocr == "OCR") scannedCount++;
    if (!string.IsNullOrEmpty(outputLine.Purposes)
        && outputLine.Purposes != string.Empty) purposesFoundCount++;
    if (!string.IsNullOrEmpty(outputLine.Points)
        && outputLine.Points != string.Empty) pointsFoundCount++;
    if (outputLine.LimitsCount > 0) limitsFoundCount++;
    if (outputLine.AggregatesCount > 0) aggregatesFoundCount++;
    if (!string.IsNullOrEmpty(outputLine.IssueDate)) issueDateFoundCount++;
    if (!string.IsNullOrEmpty(outputLine.Issuer)) issuerFoundCount++;
    if (outputLine.MeansFound) meansFoundCount++;
    if (!string.IsNullOrEmpty(outputLine.LinkedLicenceNumbers)
        && outputLine.LinkedLicenceNumbers != string.Empty) linkedLicenceNumbersFoundCount++;
    
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
        { "id", outputLine.NodeId},
        { "name", nodeName}
    });
    
    Log($"\n{outputLine.Filename},{outputLine.LicenceNumber}", mappingFileStringBuilder);
    fileCount += 1;
}

listJsStringBuilder.Append("\n];");

foreach (var outputLine in outputLines)
{
    if (!string.IsNullOrEmpty(outputLine.LinkedLicenceNumbers))
    {
        foreach (var linkedLicenceNumber in outputLine.LinkedLicenceNumbers.Split('|'))
        {
            var linkedOutputLine = outputLines.FirstOrDefault(x => x.LicenceNumber == linkedLicenceNumber);

            if (linkedOutputLine != null)
            {
                linksDictionaries.Add(new Dictionary<string, object>
                {
                    {"source", outputLine.NodeId},
                    {"target", linkedOutputLine.NodeId}
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
        {"filenameToLicenceNumber", filenameToLicenceNumberMap},
        {"licenceNumberToFilename", licenceNumberToFilenameMap}
    };

    File.WriteAllText(licenceFilenameMapJsonFile,
        $"var mapData = {JsonSerializer.Serialize(licenceFilenameMapDictionary, jsonOptions)};");
}
#pragma warning restore CS0162 // Unreachable code detected

var jsListFilePath = $"{outputFolder}list-data.js";
File.WriteAllText(jsListFilePath, listJsStringBuilder.ToString());

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

return;

async Task<Licence?> HandleFileAsync(
    string pdfFilePath,
    int fileNumber,
    Dictionary<string, string> licenceMapping)
{
    var dtStart = DateTime.Now;
    var fileName = pdfFilePath.Split('/').Last().Replace("–", "-");

    Console.WriteLine($"Attempting {fileNumber} {fileName}...");
    var pdfDataExtractor = pdfDataExtractors.First(x => !x.InUse);
    pdfDataExtractor.InUse = true;
    
    try
    {
        var previouslyParsedPaths = new List<string>
        {
            pdfFilePath
        };

        var lookupConfig = new LookupConfiguration(
            LabelConfiguration.GetLabels(),
            licenceMapping,
            outputFolder,
            cacheFolder);
        
        var matchesFull = await pdfDataExtractor.GetMatchesAsync(
            pdfFilePath,
            lookupConfig,
            previouslyParsedPaths);
        
        var agreedSchemaGroup = await SchemaConverter.ToLicenceGroupAsync(
            matchesFull,
            fileLicenceMapping,
            pdfDataExtractor,
            outputFolder,
            cacheFolder);
        
        var agreedSchema = agreedSchemaGroup.Licences.First();
        
        var purposeSb = new StringBuilder();
        first = true;
        
        foreach (var purpose in agreedSchema.Purposes)
        {
            if (purpose.Description == null)
            {
                continue;
            }
            
            if (first)
            {
                first = false;
            }
            else
            {
                purposeSb.Append(",");
            }
                
            var t = purpose.Description.Replace("\n", "\\n").Replace("\"", "\\\"");
            purposeSb.AppendLine("\"" + t + "\"");
        }
        
        var purposesJs = "[" + purposeSb + "]";
        
        var pointsSb = new StringBuilder();
        first = true;
        
        foreach (var point in agreedSchema.Points)
        {
            if (point.Description == null)
            {
                continue;
            }
            
            if (first)
            {
                first = false;
            }
            else
            {
                pointsSb.Append(",");
            }
                
            var t = point.Description.Replace("\n", "\\n").Replace("\"", "\\\"");
            pointsSb.AppendLine("\"" + t + "\"");
        }

        var pointsJs = "[" + pointsSb + "]";

        var licenceHolder = agreedSchema.NoneSchemaData.TryGetValue("issuedTo", out var value4)
            ? (string)value4 : null;
        var licenceHolderOcrConfidence = agreedSchema.NoneSchemaData.TryGetValue("issuedToConfidence", out var value5)
            ? (double)value5 : (double?)null;
        var ocr = agreedSchema.NoneSchemaData.TryGetValue("ocr", out var value6)
            ? (string)value6 : "--";
        var serviceName = agreedSchema.NoneSchemaData.TryGetValue("servicesUsed", out var value7)
            ? ((string[])value7!).FirstOrDefault() : "PdfPig";

        var durationInMSeconds = (int) (DateTime.Now - dtStart).TotalMilliseconds;

        var licenceNumber = agreedSchema.LicenceNumber;
        var licenceNumberOcrConfidence = agreedSchema.NoneSchemaData.TryGetValue("licenceNumberConfidence", out var value8)
            ? (double)value8 : (double?)null;

        var meansFound = agreedSchema.MeansOfAbstraction.Length > 0;
        
        var issueDate = agreedSchema.LicenceVersion.IssueDate?.ToString("yyyy-MM-dd");
        var issuer = agreedSchema.LicenceVersion.Issuer;

        var linkedLicenceNumbers = string.Join(
            '|',
            agreedSchema.LinkedLicences
                .Select(x => x.FromSection?.FirstOrDefault() == "ImplicitBackLink" ? $"({x.LicenceNumber})" : x.LicenceNumber));
        
        outputLines.Add(new OutputLine
        {
            LineNumber = completeNumber++,
            StartNumber = fileNumber,
            Filename = fileName.Replace("–", "-"),
            LicenceHolder = licenceHolder,
            LicenceHolderOcrConfidence = licenceHolderOcrConfidence,
            Ocr = ocr,
            Purposes = purposesJs,
            Points = pointsJs,
            ServiceName = serviceName,
            Certainty = agreedSchema.NoneSchemaData.TryGetValue("issuedToCertainty", out var value) ? (int)value : -1,
            MatchType = agreedSchema.NoneSchemaData.TryGetValue("issuedToMatchType", out var value1) ? (string)value1 : "N/A",
            Duration = durationInMSeconds,
            MatchedLabelText = agreedSchema.NoneSchemaData.TryGetValue("issuedToMatchedLabelText", out var value2) ? (string)value2 : null,
            MatchedLabelPosition = agreedSchema.NoneSchemaData.TryGetValue("issuedToMatchLabelPosition", out var value3) ? (string)value3 : null,
            LicenceNumber = licenceNumber,
            LicenceNumberOcrConfidence = licenceNumberOcrConfidence,
            LimitsCount = agreedSchema.AbstractionLimits.Individual.Sum(x => x.Limits.Count),
            AggregatesCount = agreedSchema.AbstractionLimits.Aggregates.Sum(x => x.Limits.Count),
            IssueDate = issueDate,
            Issuer = !string.IsNullOrEmpty(issuer) ? issuer : string.Empty,
            MeansFound = meansFound,
            LinkedLicenceNumbers = linkedLicenceNumbers
        });

        var json = JsonHelper.GetAsString(matchesFull);
        
        var filenameOnlyNoExtension = FileHelper.GetFilenameWithoutExtensions(pdfFilePath);
        Directory.CreateDirectory($"{outputFolder}/{filenameOnlyNoExtension}");
        
        /*File.WriteAllText(
            $"{outputFolder}/{filenameOnlyNoExtension}/data.json",
            json);*/

        File.WriteAllText(
            $"{outputFolder}/{filenameOnlyNoExtension}/data.jsonp",
            $"var data = {json}");

        var agreedSchemaJson = JsonHelper.GetAsString(agreedSchema);
        
        /*File.WriteAllText(
            $"{outputFolder}/{filenameOnlyNoExtension}/data2.json",
            agreedSchemaJson);*/

        File.WriteAllText(
            $"{outputFolder}/{filenameOnlyNoExtension}/data2.jsonp",
            $"var data2 = {agreedSchemaJson}");
        
        var agreedSchemaSetJson = JsonHelper.GetAsString(agreedSchemaGroup);
        
        File.WriteAllText(
            $"{outputFolder}/{filenameOnlyNoExtension}/licence-set.jsonp",
            $"var licenceSet = {agreedSchemaSetJson}");
        
        Console.WriteLine($"Finished {fileNumber} {fileName}...");

        return agreedSchema;
    }
    catch (Exception exception)
    {
        var durationInSeconds = (int) (DateTime.Now - dtStart).TotalSeconds;

        outputLines.Add(new OutputLine
        {
            LineNumber = completeNumber++,
            StartNumber = fileNumber,
            Filename = fileName,
            LicenceHolder = $"[Error] {exception.Message} {exception.StackTrace}",
            Ocr = "0",
            ServiceName = "NA",
            Duration = durationInSeconds
        });

        return null;
    }
    finally
    {
        pdfDataExtractor.InUse = false;
    }
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
    
    pdfFilePaths = pdfFilePaths.Where(filePath =>
    {
        var filename = filePath.Split('/').Last();
        
        return yorkshire.Contains(filename, StringComparer.InvariantCultureIgnoreCase);
    }).OrderBy(filename => filename).Skip(0).Take(200).ToList();
    
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
    
    pdfFilePaths = pdfFilePaths.Where(x => x.Contains("11149535") || x.Contains("11149440") || x.Contains("1497061")).ToArray();
    //pdfFilePaths = pdfFilePaths.OrderBy(x => x).Skip(0).Take(200).ToList();
    
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