using System.Collections;
using System.Globalization;
using CsvHelper;
using Tesseract;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.Tools.Helpers;
using WALE.Tools.Models;

namespace WALE.Tools;

public static class GenerateLicenceReaderExtract
{
    private static readonly string OutputFolder = KeyConfig.OutputFolder;
    private static readonly string CacheFolder = KeyConfig.CacheFolder;
    private static readonly Dictionary<string, string> FileLicenceMapping = new() {{"", ""}};

    public static async Task GenerateLicenceReaderExtractAsync()
    {
        var pdfDataExtractor = new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>
            {
                new TesseractOcrDataExtractorService(KeyConfig.TesseractPrefix, PageSegMode.Auto)
            },
            KeyConfig.PdfFolder);

        var data = await GetLicenceReaderDataAsync(pdfDataExtractor);

        // Generate CSV report using ToolHelper
        await ToolHelper.GenerateCsvReportWithSummaryAsync(
            data,
            "LicenceReader",
            OutputFolder,
            x => x.LicenceNumber ?? "No Licence",
            "licence records",
            "Licence Processing Summary");
    }

    static async Task<MatchesResult> GetMatchesAsync(string fileName, PdfDataExtractorService pdfDataExtractor)
    {
        var pdfFolder = KeyConfig.PdfFolder;

        try
        {
            Console.WriteLine($"Creating lookup configuration for {fileName}...");
            var labels = LicenceReaderConfiguration.GetLabels();
            Console.WriteLine($"Retrieved {labels.Count} label groups from configuration");

            var configuration = new LookupConfiguration(
                labels,
                FileLicenceMapping,
                OutputFolder,
                CacheFolder);

            Console.WriteLine($"Configuration created, calling PDF extractor...");

            var result = await pdfDataExtractor.GetMatchesAsync(
                pdfFolder + fileName,
                configuration,
                [pdfFolder + fileName]);
            Console.WriteLine($"PDF extraction completed successfully for {fileName}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetMatchesAsync for {fileName}:");
            Console.WriteLine($"  Exception Type: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
            throw; // Re-throw to maintain the original error handling flow
        }
    }

    static async Task<List<LicenceReaderCsvLine>> GetLicenceReaderDataAsync(PdfDataExtractorService pdfDataExtractor)
    {
        var pdfFilePaths = Directory
            .GetFiles(KeyConfig.PdfFolder)
            .Where(fileName => fileName.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
            .Select(x => x.Split('/').Last())
            .OrderBy(x => x).ToList();

        var returnList = new List<LicenceReaderCsvLine>();

        foreach (var pdfFilePath in pdfFilePaths)
        {
            try
            {
                Console.WriteLine($"Processing file: {pdfFilePath}");

                // Check if file exists
                var fullPath = KeyConfig.PdfFolder + pdfFilePath;
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"PDF file not found: {fullPath}");
                }

                Console.WriteLine($"File exists, attempting to extract matches...");
                var internalJson = await GetMatchesAsync(pdfFilePath, pdfDataExtractor);

                Console.WriteLine($"Matches extracted successfully, processing licence data...");

                // Extract licence number and date of issue from matches
                var licenceNumber = SharedHelper.ExtractLicenceNumber(internalJson);
                var dateOfIssue = SharedHelper.ExtractDateOfIssue(internalJson);

                // Extract permit number from filename (everything before first underscore)
                var permitNumber = SharedHelper.ExtractPermitNumberFromFilename(pdfFilePath);

                Console.WriteLine($"Extracted - Licence: {licenceNumber}, Date: {dateOfIssue}, Permit: {permitNumber}");

                returnList.Add(new LicenceReaderCsvLine
                {
                    LicenceNumber = licenceNumber,
                    PermitNumber = permitNumber,
                    DateOfIssue = SharedHelper.DateFormatConsistent(dateOfIssue)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {pdfFilePath}:");
                Console.WriteLine($"  Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"  Message: {ex.Message}");
                Console.WriteLine($"  Stack Trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  Inner Exception: {ex.InnerException.GetType().Name}");
                    Console.WriteLine($"  Inner Message: {ex.InnerException.Message}");
                }

                // Add entry with filename but null values to track failed files
                returnList.Add(new LicenceReaderCsvLine
                {
                    LicenceNumber = null,
                    PermitNumber = SharedHelper.ExtractPermitNumberFromFilename(pdfFilePath),
                    DateOfIssue = null
                });
            }
        }

        return returnList;
    }

    
}
