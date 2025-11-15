using System.Globalization;
using CsvHelper;
using Tesseract;
using WALE.ProcessFile.RuleEngine.Services;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.Tools.Helpers;

namespace WALE.Tools;

/// <summary>
/// Tool to identify file types from output folder files using rule engine
/// </summary>
public static class FileTypeIdentificationExtract
{
    private static readonly string OutputFolder = KeyConfig.OutputFolder;
    private static readonly string CacheFolder = KeyConfig.CacheFolder;
    private static readonly Dictionary<string, string> FileLicenceMapping = new() {{"", ""}};

    /// <summary>
    /// Identifies file types in the output folder and generates a CSV report
    /// </summary>
    public static async Task GenerateFileTypeIdentificationAsync()
    {
        Console.WriteLine("Starting file type identification...");

        var pdfDataExtractor = new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>
            {
                new TesseractOcrDataExtractorService(KeyConfig.TesseractPrefix, PageSegMode.Auto)
            },
            KeyConfig.PdfFolder);

        var fileTypeService = new FileTypeIdentifierService(pdfDataExtractor);

        // Process all files in the output folder
        var labels = LicenceReaderConfiguration.GetLabels();
        Console.WriteLine($"Retrieved {labels.Count} label groups from configuration");

        var configuration = new LookupConfiguration(
            labels,
            FileLicenceMapping,
            OutputFolder,
            CacheFolder);

        var results = await fileTypeService.ProcessDirectoryAsync(KeyConfig.PdfFolder, configuration);
        var csvData = new List<FileTypeIdentificationResult>();

        foreach (var result in results)
        {
            var filePath = result.Key;
            var fileTypeResult = result.Value;

            // Extract date of issue for PDF files
            string? dateOfIssue = null;
            try
            {
                var matchesResult = await pdfDataExtractor.GetMatchesAsync(filePath, configuration, new List<string>());
                dateOfIssue = SharedHelper.ExtractDateOfIssue(matchesResult);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting date from {filePath}: {ex.Message}");
            }

            csvData.Add(new FileTypeIdentificationResult
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                FileType = fileTypeResult?.FileType ?? "Unknown",
                Confidence = fileTypeResult?.Confidence ?? 0.0,
                IdentifiedByRule = fileTypeResult?.IdentifiedByRule ?? "N/A",
                MatchedTerms = fileTypeResult?.MatchedTerms != null ? string.Join("; ", fileTypeResult.MatchedTerms) : "",
                DateOfIssue = SharedHelper.DateFormatConsistent(dateOfIssue),
                FileSize = File.Exists(filePath) ? new FileInfo(filePath).Length : 0,
                LastModified = File.Exists(filePath) ? File.GetLastWriteTime(filePath) : DateTime.MinValue
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
}

/// <summary>
/// Data model for file type identification CSV export
/// </summary>
public class FileTypeIdentificationResult
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string IdentifiedByRule { get; set; } = string.Empty;
    public string MatchedTerms { get; set; } = string.Empty;
    
    public string? DateOfIssue { get; set; }
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
}
