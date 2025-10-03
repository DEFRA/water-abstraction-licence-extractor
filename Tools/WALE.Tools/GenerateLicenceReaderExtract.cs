using System.Collections;
using System.Globalization;
using CsvHelper;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
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
                new TesseractOcrDataExtractorService(KeyConfig.TesseractPrefix)
            },
            KeyConfig.PdfFolder);

        var data = await GetLicenceReaderDataAsync(pdfDataExtractor);

        var fileName = $"LicenceReader-{DateTime.Today:yyyyMMdd}.csv";
        var fullPath = Path.Combine(OutputFolder, fileName);
        await using var writer = new StreamWriter(fullPath);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csv.WriteRecordsAsync((IEnumerable)data);
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
//'/Users/sriyankamohapatra/RiderProjects/WALE/WaterPdfs/2-27-16-196 6967036.PDF'
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
                var licenceNumber = ExtractLicenceNumber(internalJson);
                var dateOfIssue = ExtractDateOfIssue(internalJson);

                Console.WriteLine($"Extracted - Licence: {licenceNumber}, Date: {dateOfIssue}");

                returnList.Add(new LicenceReaderCsvLine
                {
                    LicenceNumber = licenceNumber,
                    DateOfIssue = dateOfIssue
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
                    DateOfIssue = null
                });
            }
        }

        return returnList;
    }

    private static string? ExtractLicenceNumber(MatchesResult matchesResult)
    {
        var licenceNumberMatch = matchesResult.Matches?
            .FirstOrDefault(m => m.LabelGroupName == "LicenceNumber");

        if (licenceNumberMatch?.Text != null && licenceNumberMatch.Text.Count > 0)
        {
            return string.Join(" ", licenceNumberMatch.Text
                .SelectMany(line => line.Text)
                .Select(element => element))
                .Trim();
        }

        return null;
    }

    private static string? ExtractDateOfIssue(MatchesResult matchesResult)
    {
        var dateOfIssueMatch = matchesResult.Matches?
            .FirstOrDefault(m => m.LabelGroupName == "DateOfIssue");

        if (dateOfIssueMatch?.Text != null && dateOfIssueMatch.Text.Count > 0)
        {
            return string.Join(" ", dateOfIssueMatch.Text
                .SelectMany(line => line.Text)
                .Select(element => element))
                .Trim();
        }

        return null;
    }
}
