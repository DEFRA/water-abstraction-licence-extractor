using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

Console.WriteLine("Started");

const bool useCachedResponse = false;

var concurrentCount = int.Parse(Environment.GetEnvironmentVariable("ConcurrentCount")
    ?? throw new NullReferenceException("ConcurrentCount"));

var regenerateMappingJson = bool.Parse(Environment.GetEnvironmentVariable("REGENERATE_MAPPING_JSON")
    ?? throw new NullReferenceException("REGENERATE_MAPPING_JSON"));

var pdfFolderPath = Environment.GetEnvironmentVariable("PdfFolderPath")
    ?? throw new NullReferenceException("PdfFolderPath");
var reportTemplatePath = Environment.GetEnvironmentVariable("ReportTemplatePath")
    ?? throw new NullReferenceException("ReportTemplatePath");
var fileMappingPath = Environment.GetEnvironmentVariable("FileMappingPath")
    ?? throw new NullReferenceException("FileMappingPath");
var outputFolder = Environment.GetEnvironmentVariable("OutputFolder")
    ?? throw new NullReferenceException("OutputFolder");

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

var reportTemplateContents = File.ReadAllText(reportTemplatePath);

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

try
{
    var processingTasks = new List<Task>();
    
    foreach (var pdfFilePath in GetPdfPaths())
    {
        processingTasks.Add(HandleFileAsync(pdfFilePath, processCount++, fileLicenceMapping, useCachedResponse));

        if (processingTasks.Count == concurrentCount)
        {
            var completedTask = await Task.WhenAny(processingTasks);
            processingTasks.Remove(completedTask);
        }
    }

    if (processingTasks.Count > 0)
    {
        await Task.WhenAll(processingTasks);
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

var resultFileStringBuilder = new StringBuilder(
    "LineNumber,StartNumber,Filename,Text,OCR,ServiceName,Certainty,MatchType,Duration,MatchedLabelText," +
    "MatchedLabelPosition,LicenceNumber,LimitsFound,LinkedLicenceNumbers,LinkedLicenceNumbersExistInDataset");

var mappingFileStringBuilder = new StringBuilder(
    "Filename,LicenceNumber");

var indexFileStringBuilder = new StringBuilder("<html><head>");
indexFileStringBuilder.AppendLine("<style>table thead tr td { font-weight: bold; } table tbody tr { max-height: 40px; vertical-align: top; } table td { padding: 5px; }");
indexFileStringBuilder.AppendLine("</style");
indexFileStringBuilder.AppendLine("</head>");

indexFileStringBuilder.AppendLine("<body>");
indexFileStringBuilder.AppendLine("<h1>All licences</h1>");

indexFileStringBuilder.AppendLine("<table>");
indexFileStringBuilder.AppendLine("<thead>");
indexFileStringBuilder.AppendLine("<tr>");
indexFileStringBuilder.AppendLine("<td style='width: 5%'>Preview</td>");
/*indexFileStringBuilder.AppendLine("<td style='width: 5%'>Index</td>");*/
indexFileStringBuilder.AppendLine("<td style='width: 15%'>Filename</td>");
indexFileStringBuilder.AppendLine("<td style='width: 10%'>Licence number</td>");
indexFileStringBuilder.AppendLine("<td style='width: 15%'>Licence holder</td>");
indexFileStringBuilder.AppendLine("<td style='width: 10%'>Scanned file</td>");
indexFileStringBuilder.AppendLine("<td style='width: 10%'>Purposes</td>");
indexFileStringBuilder.AppendLine("<td style='width: 10%'>Abstraction limits</td>");
indexFileStringBuilder.AppendLine("<td style='width: 10%'>Means of abstraction</td>");
indexFileStringBuilder.AppendLine("<td style='width: 10%'>Linked&nbsp;licences</td>");
indexFileStringBuilder.AppendLine("</tr>");
indexFileStringBuilder.AppendLine("</thead>");
indexFileStringBuilder.AppendLine("<tbody>");

var filenameToLicenceNumberMap = new Dictionary<string, string>();
var licenceNumberToFilenameMap = new Dictionary<string, string>();
var fileCount = 1;

var licenceNumberFoundCount = 0;
var licenceHolderFoundCount = 0;
var scannedCount = 0;
var purposesFoundCount = 0;
var limitsFoundCount = 0;
var linkedLicenceNumbersFoundCount = 0;
var meansFoundCount = 0;

var nodeIndex = 1;
var nodesDictionaries = new List<Dictionary<string, object>>();
var linksDictionaries = new List<Dictionary<string, object>>();

foreach (var outputLine in outputLines.OrderBy(x => x.Filename))
{
    var anyLinkedLicenceNumbers = outputLine.LinkedLicenceNumbers?
        .Split('|')
        .Any(lln => outputLines.Count(ol => ol.LicenceNumber == lln) > 0) ?? false;
            
    Log(
        $"\n{outputLine.LineNumber},{outputLine.StartNumber},{outputLine.Filename}," +
        $"\"{outputLine.LicenceHolder}\",{outputLine.Ocr},{outputLine.ServiceName},{outputLine.Certainty}," +
        $"{outputLine.MatchType},{outputLine.Duration},{outputLine.MatchedLabelText}," +
        $"{outputLine.MatchedLabelPosition},{outputLine.LicenceNumber},{outputLine.LimitsFound}," +
        $"{outputLine.LinkedLicenceNumbers},{anyLinkedLicenceNumbers}",
        resultFileStringBuilder);

    var color = fileCount % 2 == 0 ? "#F6F6F6" : "#FAFAFA";
    var backgroundCss = $"style='background-color: {color}'";
    var filename = DataHelpers.GetFilenameWithoutExtensions(outputLine.Filename!);
    var filenameForScreen = outputLine.Filename;

    if (filenameForScreen?.Length > 30)
    {
        filenameForScreen = filenameForScreen[..30] + "-<br>" + filenameForScreen[30..];
    }
    
    indexFileStringBuilder.AppendLine($"<tr {backgroundCss}>");
    indexFileStringBuilder.AppendLine($"<td style='text-align: center'><img src='{filename}/PdfPig/Images/page-1.png' style='height: 80px' alt='No image found' onerror='this.style.display=\"none\"' /></td>");
    /*indexFileStringBuilder.AppendLine($"<td>{fileCount}</td>");*/
    indexFileStringBuilder.AppendLine($"<td><a href='{filename}/report.html'>{filenameForScreen}</a></td>");
    indexFileStringBuilder.AppendLine($"<td>{outputLine.LicenceNumber}{ToPercent(outputLine.LicenceNumberOcrConfidence, outputLine.Ocr)}</td>");
    indexFileStringBuilder.AppendLine($"<td>{outputLine.LicenceHolder}{ToPercent(outputLine.LicenceHolderOcrConfidence, outputLine.Ocr)}</td>");
    indexFileStringBuilder.AppendLine($"<td>{outputLine.Ocr == "OCR"}</td>");
    indexFileStringBuilder.AppendLine($"<td>{outputLine.Purposes}</td>");
    indexFileStringBuilder.AppendLine($"<td>{outputLine.LimitsFound}</td>");
    indexFileStringBuilder.AppendLine($"<td>{outputLine.MeansFound}</td>");
    indexFileStringBuilder.AppendLine($"<td>{!string.IsNullOrEmpty(outputLine.LinkedLicenceNumbers) && outputLine.LinkedLicenceNumbers != "--"}</td>");
    indexFileStringBuilder.AppendLine("</tr>");

    if (!string.IsNullOrEmpty(outputLine.LicenceNumber)
        && outputLine.LicenceNumber != "--") licenceNumberFoundCount++;
    if (!string.IsNullOrEmpty(outputLine.LicenceHolder)
        && outputLine.LicenceHolder != "--") licenceHolderFoundCount++;
    if (outputLine.Ocr == "OCR") scannedCount++;
    if (!string.IsNullOrEmpty(outputLine.Purposes)
        && outputLine.Purposes != "--") purposesFoundCount++;
    if (outputLine.LimitsFound) limitsFoundCount++;
    if (outputLine.MeansFound) meansFoundCount++;
    if (!string.IsNullOrEmpty(outputLine.LinkedLicenceNumbers)
        && outputLine.LinkedLicenceNumbers != "--") linkedLicenceNumbersFoundCount++;
    
    if (outputLine.LicenceNumber != null)
    {
        filenameToLicenceNumberMap.TryAdd(outputLine.Filename!, outputLine.LicenceNumber);
        licenceNumberToFilenameMap.TryAdd(outputLine.LicenceNumber, outputLine.Filename!);
    }

    outputLine.NodeId = nodeIndex++;
    var nodeName = outputLine.Filename!;

    if (!string.IsNullOrEmpty(outputLine.LicenceNumber) && outputLine.LicenceNumber != "--")
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

indexFileStringBuilder.AppendLine("<tr style='font-weight: bold;'>");
indexFileStringBuilder.AppendLine("<td>Total</td>");
/*indexFileStringBuilder.AppendLine("<td></td>");*/
indexFileStringBuilder.AppendLine($"<td>{fileCount-1}</td>");
indexFileStringBuilder.AppendLine($"<td>{licenceNumberFoundCount}</td>");
indexFileStringBuilder.AppendLine($"<td>{licenceHolderFoundCount}</td>");
indexFileStringBuilder.AppendLine($"<td>{scannedCount}</td>");
indexFileStringBuilder.AppendLine($"<td>{purposesFoundCount}</td>");
indexFileStringBuilder.AppendLine($"<td>{limitsFoundCount}</td>");
indexFileStringBuilder.AppendLine($"<td>{meansFoundCount}</td>");
indexFileStringBuilder.AppendLine($"<td>{linkedLicenceNumbersFoundCount}</td>");
indexFileStringBuilder.AppendLine("</tr>");

indexFileStringBuilder.AppendLine("</tbody>");
indexFileStringBuilder.AppendLine("</table>");

var resultFile = $"{outputFolder}{DateTime.Today:yyyyMMdd}-result.csv";
File.WriteAllText(resultFile, resultFileStringBuilder.ToString());

var licenceFilenameMapFile = $"{outputFolder}licence-number-filename-map.csv";
File.WriteAllText(licenceFilenameMapFile, mappingFileStringBuilder.ToString());

#pragma warning disable CS0162 // Unreachable code detected
if (regenerateMappingJson)
{
    var licenceFilenameMapJsonFile = $"{outputFolder}licence-number-filename-map.jsonp";
    var licenceFilenameMapDictionary = new Dictionary<string, object>
    {
        {"filenameToLicenceNumber", filenameToLicenceNumberMap},
        {"licenceNumberToFilename", licenceNumberToFilenameMap}
    };

    File.WriteAllText(licenceFilenameMapJsonFile,
        $"var mapData = {JsonSerializer.Serialize(licenceFilenameMapDictionary, jsonOptions)};");
}
#pragma warning restore CS0162 // Unreachable code detected

indexFileStringBuilder.AppendLine("<ul>");
indexFileStringBuilder.AppendLine("</body></html>");

var indexFilePath = $"{outputFolder}index.html";
File.WriteAllText(indexFilePath, indexFileStringBuilder.ToString());

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

var nodeGraphDataFile = $"{outputFolder}node-graph-data.jsonp";
File.WriteAllText(nodeGraphDataFile,
    $"var data = {JsonSerializer.Serialize(nodeGraphData, jsonOptions)};");

return;

async Task HandleFileAsync(
    string pdfFilePath,
    int fileNumber,
    Dictionary<string, string> licenceMapping,
    bool useCache)
{
    var dtStart = DateTime.Now;
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
        
        var matches1 = await pdfDataExtractor.GetMatchesAsync(
            pdfFilePath,
            LabelConfiguration.GetLabels(),
            licenceMapping,
            previouslyParsedPaths,
            outputFolder,
            useCache);

        var matches = matches1.Matches!;
        
        var purposeMatch = matches.FirstOrDefault(result => result.LabelGroupName == "Purpose");
        var purposeText = purposeMatch?.Text?.FirstOrDefault()?.Text ?? "--";
        
        var companyNameMatch = matches.FirstOrDefault(result => result.LabelGroupName == "Company");
        var licenceHolder = companyNameMatch?.Text?.FirstOrDefault()?.Text ?? "--";
        var licenceHolderOcrConfidence = companyNameMatch?.Text?.FirstOrDefault()?.OcrConfidence;
        
        var certainty = (int) (companyNameMatch?.MatchType ?? MatchType.NotApplicable) / 100;
        var ocr = companyNameMatch?.IsOcr == true ? "OCR" : "NoOCR";

        var serviceName = companyNameMatch?.ServiceName ?? "PdfPig";

        var limitsResult = matches.FirstOrDefault(result => result.LabelGroupName == "AbstractionLimits");
        var linkedLicenceNumbersList = limitsResult?
            .SubResults?
            .SelectMany(x => x.SubResults!)
            .SelectMany(section => section.SubResults?.Where(sr => sr.MatchedLabel?.Name == "LinkedLicenceNumber")!)
            .SelectMany(x => x.Text!)
            .Where(x => !string.IsNullOrEmpty(x?.Text))
            .ToList();
        
        var linkedLicenceNumbers = linkedLicenceNumbersList != null ?
            string.Join("|", linkedLicenceNumbersList.Select(x => x?.Text).ToArray())
            : "--";

        var durationInMSeconds = (int) (DateTime.Now - dtStart).TotalMilliseconds;
        var matchedLabelText = companyNameMatch?.MatchedLabel?.Text?.FirstOrDefault() ?? "--";
        var matchedLabelPosition = companyNameMatch?.MatchedLabel?.Position.ToString();

        var licenceNumber = matches
            .FirstOrDefault(result => result.LabelGroupName == "LicenceNumber")?
            .Text?
            .FirstOrDefault()?
            .Text ?? "--";

        var licenceNumberOcrConfidence = matches
            .FirstOrDefault(result => result.LabelGroupName == "LicenceNumber")?
            .Text?
            .FirstOrDefault()?
            .OcrConfidence;

        var limitsFound = matches.Any(result => result.LabelGroupName == "AbstractionLimits");
        var meansFound = matches
            .FirstOrDefault(result => result.LabelGroupName == "MeansOfAbstraction")?
            .SubResults?.Count > 0;
        
        outputLines.Add(new OutputLine
        {
            LineNumber = completeNumber++,
            StartNumber = fileNumber,
            Filename = fileName,
            LicenceHolder = licenceHolder,
            LicenceHolderOcrConfidence = licenceHolderOcrConfidence,
            Ocr = ocr,
            Purposes = purposeText,
            ServiceName = serviceName,
            Certainty = certainty,
            MatchType = companyNameMatch?.MatchType.ToString() ?? "N/A",
            Duration = durationInMSeconds,
            MatchedLabelText = matchedLabelText,
            MatchedLabelPosition = matchedLabelPosition,
            LicenceNumber = licenceNumber,
            LicenceNumberOcrConfidence = licenceNumberOcrConfidence,
            LimitsFound = limitsFound,
            MeansFound = meansFound,
            LinkedLicenceNumbers = linkedLicenceNumbers
        });

        var json = SharedHelper.GetJson(matches1, pdfFilePath);
        
        var filenameOnlyNoExtension = DataHelpers.GetFilenameWithoutExtensions(pdfFilePath);
        Directory.CreateDirectory($"{outputFolder}/{filenameOnlyNoExtension}");
        
        File.WriteAllText(
            $"{outputFolder}/{filenameOnlyNoExtension}/data.json",
            json);

        File.WriteAllText(
            $"{outputFolder}/{filenameOnlyNoExtension}/data.jsonp",
            $"var data = {json}");
        
        File.WriteAllText(
            $"{outputFolder}/{filenameOnlyNoExtension}/report.html",
            reportTemplateContents.Replace("{Filename}", filenameOnlyNoExtension));
        
        Console.WriteLine($"Finished {fileNumber} {fileName}...");
    }
    catch (Exception exception)
    {
        var durationInSeconds = (int) (DateTime.Now - dtStart).TotalSeconds;

        outputLines.Add(new OutputLine
        {
            LineNumber = completeNumber++,
            StartNumber = fileNumber,
            Filename = fileName,
            LicenceHolder = $"[Error] {exception.Message}",
            Ocr = "0",
            ServiceName = "NA",
            Duration = durationInSeconds
        });
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

    var rnd = new Random();
    
    //pdfFilePaths = pdfFilePaths.Where(x => x.Contains("Application - Transfer -Application New Licence Issued 19_06_2019 00_00_00 10893476.pdf")).ToArray();
    //pdfFilePaths = pdfFilePaths.Where(x => x.Contains("Licence - Old 6078869.PDF")).ToArray();
    //pdfFilePaths = pdfFilePaths.Where(x => x.Contains("Application Minor Variation Issued Licence 11.12.2019 11149448.pdf")).ToArray();
    //pdfFilePaths = pdfFilePaths.Where(x => x.Contains("Non-Application Licence Document (08.06.1987).PDF")).ToArray();
    //pdfFilePaths = pdfFilePaths.Where(x => 
    //    x.Contains("original licence (12.03.1975).PDF")
    //||
    //    x.Contains("Issued Licence - 01081966")).ToArray();
    //pdfFilePaths = pdfFilePaths.Where(x => x.Contains("Licence - Old 6078947.PDF")).ToArray();
    //pdfFilePaths = pdfFilePaths.Where(x => x.Contains("Licence Original 5652046.pdf")).ToArray();
    //pdfFilePaths = pdfFilePaths.Where(x => x.Contains("permit_01_01_1998.pdf")).ToArray();
    //pdfFilePaths = pdfFilePaths.Where(x => x.Contains("Application - New - Issued Licence Dec 2015 9146886.pdf")).ToArray();
    pdfFilePaths = pdfFilePaths.OrderBy(x => x).Take(5).ToList();
    //pdfFilePaths = pdfFilePaths.Where(x => x.Contains("14460030852 licence effective 24.07.2005.PDF")).ToArray();
    //pdfFilePaths = pdfFilePaths.Where(x => x.Contains("08-37-31-S-0199 5835643.PDF")).ToArray();
    
    return pdfFilePaths;
}

void Log(string message, StringBuilder outputStringBuilder)
{
//    Console.WriteLine(message);
    outputStringBuilder.Append(message);
}

class OutputLine
{
    public int LineNumber;
    public int StartNumber;
    public string? Filename;
    public string? LicenceHolder;
    public double? LicenceHolderOcrConfidence;
    public string? Ocr;
    public string? Purposes;
    public string? ServiceName;
    public int Certainty;
    public string? MatchType;
    public int Duration;
    public string? MatchedLabelText;
    public string? MatchedLabelPosition;
    public string? LicenceNumber;
    public double? LicenceNumberOcrConfidence;
    public bool LimitsFound;
    public bool MeansFound;
    public string? LinkedLicenceNumbers;
    public int NodeId;
}