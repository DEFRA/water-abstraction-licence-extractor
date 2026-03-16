using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Services;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WALE.Tools.Config;
using WALE.Tools.Helpers;
using WALE.Tools.Models;

namespace WALE.Tools._1stHalf;

/// <summary>
/// Tool to identify file types from output folder files using rule engine
/// </summary>
public static class FileTypeIdentificationExtract
{
    private static readonly string OutputFolder = KeyConfig.OutputFolder;
    private static readonly Dictionary<string, DmsFileData> FileLicenceMapping = [];

    /// <summary>
    /// Identifies file types in the output folder and generates a CSV report
    /// </summary>
    public static async Task GenerateFileTypeIdentificationAsync()
    {
        ConsoleHelper.WriteLine("Starting file type identification...");
        
        var dotnetPath = KeyConfig.DotnetPath;
        var tesseractExeName = KeyConfig.TesseractExeName;
        var tesseractExeDirectory = KeyConfig.TesseractExeDirectory;
    
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(KeyConfig.ApiBaseUrl);
    
        var cacheService = new ApiCacheService(httpClient);
        var outputService = new ApiOutputService(httpClient);
        
        var pdfPigDocumentService = new PdfPigNoOcrPdfDocumentService();
        var docnetAlternativeDocumentService = new DocnetNoOcrAlternativePdfDocumentService();
        
        // Create 10 instances of PdfDataExtractorService for parallel processing
        var pdfDataExtractors = new List<IPdfDataExtractorService>();
        
        for (var i = 0; i < 10; i++)
        {
            var pdfDataExtractor = new PdfDataExtractorService(
                new PdfPigNoOcrDataExtractorService(),
                new List<IOcrDataExtractorService>
                {
                    new TesseractOcrDataExtractorService(
                        KeyConfig.TesseractPrefix, 
                        PageSegMode.SparseTextOsd, 
                        cacheService, 
                        outputService,
                        dotnetPath, 
                        tesseractExeName, 
                        tesseractExeDirectory,
                        i + 1),
                    new TesseractOcrDataExtractorService(
                        KeyConfig.TesseractPrefix, 
                        PageSegMode.Auto, 
                        cacheService, 
                        outputService,
                        dotnetPath, 
                        tesseractExeName, 
                        tesseractExeDirectory,
                        i + 1),
                    new AzureAiVisionOcrDataExtractorService(
                        KeyConfig.AiVisionEndpoint,
                        KeyConfig.AiVisionKey,
                        cacheService,
                        outputService)
                },
                cacheService, 
                outputService,
                pdfPigDocumentService,
                docnetAlternativeDocumentService);

            pdfDataExtractors.Add(pdfDataExtractor);
        }

        var fileTypeService = new FileTypeIdentifierService(pdfDataExtractors);

        // Process all files in the output folder
        var labels = LicenceReaderConfiguration.GetLabels();
        ConsoleHelper.WriteLine($"Retrieved {labels.Count} label groups from configuration");

        var configuration = new LookupConfiguration(
            labels,
            FileLicenceMapping,
            [],
            new LocalFileService(KeyConfig.PdfFolder),
            3);

        var results = await fileTypeService.ProcessDirectoryAsync(
            KeyConfig.PdfFolder,
            configuration,
            outputService,
            pdfPigDocumentService,
            docnetAlternativeDocumentService);
        
        var csvData = new List<FileTypeIdentificationResult>();

        foreach (var result in results)
        {
            var filePath = result.Key;
            var fileTypeResult = result.Value;

            csvData.Add(new FileTypeIdentificationResult
            {
                FilePath = filePath,
                OriginalFileName = Path.GetFileName(filePath), 
                FileName = GetFileNameAfterFirstUnderscore(Path.GetFileName(filePath)),
                FileType = fileTypeResult?.FileType ?? "Unknown",
                Confidence = fileTypeResult?.Confidence ?? 0.0,
                IdentifiedByRule = fileTypeResult?.IdentifiedByRule ?? "N/A",
                MatchedTerms = fileTypeResult?.MatchedTerms != null ? string.Join("; ", fileTypeResult.MatchedTerms) : "",
                DateOfIssue = fileTypeResult?.DateOfIssue,
                FileSize = File.Exists(filePath) ? new FileInfo(filePath).Length : 0,
                LicenceNumber = fileTypeResult?.LicenceNumber
            });
        }

        csvData = csvData.OrderBy(x => x.FileType).ThenBy(x => x.FileName).ToList();

        // Generate CSV report using ToolHelper
        await ToolHelper.GenerateCsvReportWithSummaryAsync(
            csvData,
            "FileTypeIdentification",
            OutputFolder,
            x => x.FileType,
            "files",
            "File Type Summary");
    }
    
    /// <summary>
    /// Extracts substring after the first occurrence of "__" in filename
    /// </summary>
    private static string GetFileNameAfterFirstUnderscore(string fileName)
    {
        var underscoreIndex = fileName.IndexOf("__", StringComparison.Ordinal);
        
        return underscoreIndex >= 0 && underscoreIndex < fileName.Length - 2 
            ? fileName[(underscoreIndex + 2)..] 
            : fileName;
    }
}