using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using Tesseract;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.Tools.Models;

namespace WALE.Tools;

public static class DuplicateLicenceIdentificationExtract
{
    private static readonly string OutputFolder = KeyConfig.OutputFolder;
    private static readonly string CacheFolder = KeyConfig.CacheFolder;
    private static readonly Dictionary<string, string> FileLicenceMapping = new() {{"", ""}};

    public static async Task GenerateDuplicateLicenceIdentificationExtractAsync()
    {
        // Step 1: Get the input of processing by reading DuplicateResults_Extract.xlsx from KeyConfig.PdfFolderForDuplicates into a list of LicenceDuplicateFinderInput objects
        var duplicateInputs = await ReadDuplicateResultsFromExcelAsync();
        
        // Step 2: Group by Permit Number and loop over each group to process each group and generate a result list of LicenceDuplicateCsvLine objects
        var csvResults = new List<LicenceDuplicateCsvLine>();

        var groupedByPermit = duplicateInputs
            .Where(x => !string.IsNullOrEmpty(x.PermitNumber))
            .GroupBy(x => x.PermitNumber);

        foreach (var group in groupedByPermit)
        {
            // Step 3: Get the list of files for the current permit number
            var filesForPermit = group.ToList();

            // Step 4: Read the pdf files for the current permit number from KeyConfig.PdfFolderForDuplicates
            var pdfFilesData = ReadPdfFilesForPermit(filesForPermit);

            // Step 5 - Identify the main file in pdfFilesData which is the file that has a name the other files are sub strings of
            // for example 0-034 5665255.PDF is main file in a group with that and -034 5665255.PDF
            var mainFile = IdentifyMainFile(pdfFilesData);
            if(mainFile.HasValue && mainFile.Value.FileExists)
                Console.WriteLine($"Main file: {mainFile.Value.FileName}");

            // Step 6 - Process the main file and compare with other files for duplicate analysis
        }
        
        var pdfDataExtractor = new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>
            {
                new TesseractOcrDataExtractorService(KeyConfig.TesseractPrefix, PageSegMode.Auto)
            },
            KeyConfig.PdfFolderForDuplicates);

        var fileName = $"Duplicate--Licence--Extract-{DateTime.Today:yyyyMMdd}.csv";
        var fullPath = Path.Combine(OutputFolder, fileName);
        await using var writer = new StreamWriter(fullPath);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csv.WriteRecordsAsync(csvResults);
    }
    
    private static async Task<List<LicenceDuplicateFinderInput>> ReadDuplicateResultsFromExcelAsync()
    {
        var excelFilePath = Path.Combine(KeyConfig.PdfFolderForDuplicates, "DuplicateResults_Extract.xlsx");
        var duplicateInputs = new List<LicenceDuplicateFinderInput>();

        using var workbook = new XLWorkbook(excelFilePath);
        var worksheet = workbook.Worksheet(1);
        var usedRange = worksheet.RangeUsed();

        // Read header row to create column mapping
        var headerMapping = new Dictionary<string, int>();
        for (int col = 1; col <= usedRange.LastColumn().ColumnNumber(); col++)
        {
            var headerValue = worksheet.Cell(1, col).GetValue<string>()?.Trim();
            if (!string.IsNullOrEmpty(headerValue))
            {
                headerMapping[headerValue] = col;
            }
        }

        var permitNumberCol = headerMapping["Permit Number"];
        var fileNameCol = headerMapping["File Name"];
        var fileUrlCol = headerMapping["File URL"];

        // Read data rows starting from row 2
        for (int row = 2; row <= usedRange.LastRow().RowNumber(); row++)
        {
            var permitNumber = worksheet.Cell(row, permitNumberCol).GetValue<string>();
            var fileName = worksheet.Cell(row, fileNameCol).GetValue<string>();
            var fileUrl = worksheet.Cell(row, fileUrlCol).GetValue<string>();

            duplicateInputs.Add(new LicenceDuplicateFinderInput
            {
                PermitNumber = permitNumber,
                FileName = fileName,
                FileUrl = fileUrl
            });
        }

        return duplicateInputs;
    }

    private static List<(string FileName, string FilePath, bool FileExists)> ReadPdfFilesForPermit(
        List<LicenceDuplicateFinderInput> filesForPermit)
    {
        var pdfFilesData = new List<(string FileName, string FilePath, bool FileExists)>();

        foreach (var file in filesForPermit)
        {
            if (!string.IsNullOrEmpty(file.FileName))
            {
                // Search for files that end with the expected filename after number__ prefix
                var matchingFiles = Directory.GetFiles(KeyConfig.PdfFolderForDuplicates, $"*__{file.FileName}")
                    .Where(f => Path.GetFileName(f).Contains("__"))
                    .ToList();

                if (matchingFiles.Any())
                {
                    foreach (var matchingFile in matchingFiles)
                    {
                        var actualFileName = Path.GetFileName(matchingFile);
                        pdfFilesData.Add((actualFileName, matchingFile, true));
                    }
                }
                else
                {
                    // Fallback: try direct filename match
                    var directPath = Path.Combine(KeyConfig.PdfFolderForDuplicates, file.FileName);
                    var directExists = File.Exists(directPath);

                    pdfFilesData.Add((file.FileName, directPath, directExists));

                    if (!directExists)
                    {
                        Console.WriteLine($"Warning: PDF file not found with pattern *__{file.FileName} or direct match: {file.FileName}");
                    }
                }
            }
        }

        return pdfFilesData;
    }

    private static (string FileName, string FilePath, bool FileExists)? IdentifyMainFile(
        List<(string FileName, string FilePath, bool FileExists)> pdfFilesData)
    {
        if (pdfFilesData.Count <= 1)
            return pdfFilesData.FirstOrDefault();

        // The main file is simply the one with the maximum number of characters
        var mainFile = pdfFilesData
            .Where(file => file.FileExists)
            .OrderByDescending(file => Path.GetFileNameWithoutExtension(file.FileName).Length)
            .FirstOrDefault();

        return mainFile.FileName != null ? mainFile : pdfFilesData.FirstOrDefault(f => f.FileExists);
    }
}
