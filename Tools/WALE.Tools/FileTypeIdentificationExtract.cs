using System.Globalization;
using CsvHelper;
using Tesseract;
using WALE.ProcessFile.RuleEngine.Services;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;

namespace WALE.Tools;

/// <summary>
/// Tool to identify file types from output folder files using rule engine
/// </summary>
public static class FileTypeIdentificationExtract
{
    private static readonly string OutputFolder = KeyConfig.PdfFolder;

    /// <summary>
    /// Identifies file types in the output folder and generates a CSV report
    /// </summary>
    public static async Task GenerateFileTypeIdentificationAsync()
    {
        Console.WriteLine("Starting file type identification...");

        var fileTypeService = new FileTypeIdentifierService(new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>
            {
                new TesseractOcrDataExtractorService(KeyConfig.TesseractPrefix, PageSegMode.Auto)
            },
            KeyConfig.PdfFolder));

        // Process all files in the output folder
        var results = await fileTypeService.ProcessDirectoryAsync(OutputFolder);

        // Prepare data for CSV export
        var csvData = results.Select(kvp => new FileTypeIdentificationResult
        {
            FilePath = kvp.Key,
            FileName = Path.GetFileName(kvp.Key),
            FileType = kvp.Value?.FileType ?? "Unknown",
            Confidence = kvp.Value?.Confidence ?? 0.0,
            IdentifiedByRule = kvp.Value?.IdentifiedByRule ?? "N/A",
            MatchedTerms = kvp.Value?.MatchedTerms != null ? string.Join("; ", kvp.Value.MatchedTerms) : "",
            FileSize = File.Exists(kvp.Key) ? new FileInfo(kvp.Key).Length : 0,
            LastModified = File.Exists(kvp.Key) ? File.GetLastWriteTime(kvp.Key) : DateTime.MinValue
        }).OrderBy(x => x.FileType).ThenBy(x => x.FileName).ToList();

        // Generate CSV report
        var fileName = $"FileTypeIdentification-{DateTime.Today:yyyyMMdd}.csv";
        var fullPath = Path.Combine(OutputFolder, fileName);

        await using var writer = new StreamWriter(fullPath);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csv.WriteRecordsAsync(csvData);

        Console.WriteLine($"File type identification completed. Results written to: {fullPath}");
        Console.WriteLine($"Total files processed: {results.Count}");

        // Print summary
        var summary = csvData.GroupBy(x => x.FileType)
            .Select(g => new { FileType = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count);

        Console.WriteLine("\nFile Type Summary:");
        foreach (var item in summary)
        {
            Console.WriteLine($"  {item.FileType}: {item.Count} files");
        }
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
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
}
