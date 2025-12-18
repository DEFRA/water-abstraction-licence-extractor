using System.Collections;
using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using Tesseract;
using WALE.ProcessFile.Database.Services;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.RuleEngine.Services;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.Tools.Helpers;
using WALE.Tools.Models;

namespace WALE.Tools;

public static class TemplateIdentificationExtract
{
    private static readonly string OutputFolder = KeyConfig.OutputFolder;
    private static readonly string CacheFolder = KeyConfig.CacheFolder;
    private static readonly Dictionary<string, string> FileLicenceMapping = new() {{"", ""}};

    public static async Task GenerateTemplateFinderResult()
    {
        var sqlConnectionString = KeyConfig.SqlConnectionString;
    
        var databaseReadService = new SqlSeverReadService(sqlConnectionString);
        var databaseAddService = new SqlSeverWriteService(sqlConnectionString);
    
        var cacheService = new DatabaseCacheService(databaseReadService, databaseAddService);
        var outputService = new DatabaseOutputService(databaseReadService, databaseAddService);
        var pdfDataExtractor = new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>
            {
                new TesseractOcrDataExtractorService(KeyConfig.TesseractPrefix, PageSegMode.SparseTextOsd, cacheService, outputService),
                new TesseractOcrDataExtractorService(KeyConfig.TesseractPrefix, PageSegMode.Auto, cacheService, outputService),
                new AzureAiVisionOcrDataExtractorService(
                    KeyConfig.AiVisionEndpoint,
                    KeyConfig.AiVisionKey,
                    cacheService,
                    outputService)
            },
            cacheService,
            outputService,
            KeyConfig.PdfFolder);

        var data = await GetTemplateFinderDataAsync(pdfDataExtractor);

        var fileName = $"Template_Finder-{DateTime.Today:yyyyMMdd}.xlsx";
        var fullPath = Path.Combine(OutputFolder, fileName);
        CreateExcelFileFromList(data, fullPath);
    }
    public static void CreateExcelFileFromList<T>(List<T> employees, string filePath)
    {
        // 2. Create a new Excel workbook
        var workbook = new XLWorkbook();

        // 3. Add a new worksheet and insert the list data, including headers
        // The 'true' argument in LoadFromCollection indicates that the first row is for headers
        var worksheet = workbook.Worksheets.Add("All_Results");
        worksheet.Cell(1, 1).InsertTable(employees);

        // Optional: Adjust column widths to fit the contents
        worksheet.Columns().AdjustToContents();

        // 4. Save the workbook
        try
        {
            workbook.SaveAs(filePath);
            Console.WriteLine($"Excel file successfully created at: {filePath}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error saving file: {ex.Message}");
        }
    }

    static async Task<List<TemplateFinderInput>> GetTemplateFinderDataAsync(PdfDataExtractorService pdfDataExtractor)
    {
        var pdfFilePaths = Directory
            .GetFiles(KeyConfig.PdfFolder)
            .Where(fileName => fileName.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
            .Select(x => x.Split('/').Last())
            .OrderBy(x => x).ToList();

        var returnList = new List<TemplateFinderInput>();
        var templateFinderInput = (await ReadTemplateReaderInput());//.Take(100);
           // .Where(x => x.PermitNumber.Equals("12100010")).ToList();

        // Create the TemplateTypeIdentifierService
        var templateTypeService = new TemplateTypeIdentifierService(pdfDataExtractor);

        // Group files into batches of 15
        const int batchSize = 15;
        var batches = templateFinderInput
            .Select((templateFile, index) => new { templateFile, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.templateFile).ToList())
            .ToList();

        Console.WriteLine($"Processing {templateFinderInput.Count()} files in {batches.Count} batches of {batchSize}");

        foreach (var batch in batches)
        {
            Console.WriteLine($"Processing batch with {batch.Count} files...");

            var batchTasks = batch.Select(async templateFile =>
            {
                var pdfFilePath = pdfFilePaths.FirstOrDefault(p => p.StartsWith(templateFile.PermitNumber)
                && p.Contains(templateFile.FileName));
                if (pdfFilePath == null) return null;

                try
                {
                    Console.WriteLine($"Processing file: {pdfFilePath}");

                    // Check if file exists
                    var fullPath = KeyConfig.PdfFolder + pdfFilePath;
                    if (!File.Exists(fullPath))
                    {
                        throw new FileNotFoundException($"PDF file not found: {fullPath}");
                    }

                    Console.WriteLine($"File exists, attempting to identify template...");

                    // Use the TemplateTypeIdentifierService to identify the template
                    // The service will use configurations from RuleConfiguration folder internally
                    var templateResult = await templateTypeService.IdentifyTemplateTypeAsync(fullPath);

                    Console.WriteLine($"Template identification completed successfully for {pdfFilePath}");

                    if (templateResult != null)
                    {
                        return new TemplateFinderInput
                        {
                            PermitNumber = templateFile.PermitNumber,
                            DateOfIssue = templateFile.DateOfIssue,
                            SignatureDate = templateFile.SignatureDate,
                            FileUrl = templateFile.FileUrl,
                            FileName = templateFile.FileName,
                            NaldIssueNumber = templateFile.NaldIssueNumber,
                            Header = templateResult.Header,
                            NumberOfPages = templateResult.NumberOfPages,
                            TemplateType = templateResult.TemplateType,
                            Template = templateResult.Template
                        };
                    }
                    else
                    {
                        // Fallback for unidentified templates
                        return new TemplateFinderInput
                        {
                            PermitNumber = templateFile.PermitNumber,
                            DateOfIssue = templateFile.DateOfIssue,
                            SignatureDate = templateFile.SignatureDate,
                            FileUrl = templateFile.FileUrl,
                            FileName = templateFile.FileName,
                            NaldIssueNumber = templateFile.NaldIssueNumber,
                            Header = "Unknown",
                            NumberOfPages = 0,
                            TemplateType = "Unknown",
                            Template = "Unknown"
                        };
                    }
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

                    // Return a failed result for tracking
                    return new TemplateFinderInput
                    {
                        PermitNumber = templateFile.PermitNumber,
                        DateOfIssue = templateFile.DateOfIssue,
                        SignatureDate = templateFile.SignatureDate,
                        FileUrl = templateFile.FileUrl,
                        FileName = templateFile.FileName,
                        NaldIssueNumber = templateFile.NaldIssueNumber,
                        Header = "Error",
                        NumberOfPages = 0,
                        TemplateType = "Error",
                        Template = "Error"
                    };
                }
            });

            // Process batch concurrently and collect results
            var batchResults = await Task.WhenAll(batchTasks);
            var validResults = batchResults.Where(result => result != null).ToList();

            returnList.AddRange(validResults);
            Console.WriteLine($"Batch completed. Processed {validResults.Count} files successfully.");
        }

        return returnList;
    }

    private static async Task<List<TemplateFinderInput>> ReadTemplateReaderInput()
    {
        var excelFilePath = Path.Combine(KeyConfig.PdfFolder, "TemplateIdentificationResults.xlsx");
        var inputResults = new List<TemplateFinderInput>();

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
        var naldIssueNumber = headerMapping["NaldIssueNumber"];
        var signatureDateCol = headerMapping["SignatureDate"];
        var fileUrlCol = headerMapping["File URL"];
        var dateOfIssueCol = headerMapping["DateOfIssue"];
        var fileNameCol = headerMapping["FileName"];

        // Read data rows starting from row 2
        for (int row = 2; row <= usedRange.LastRow().RowNumber(); row++)
        {
            var permitNumber = worksheet.Cell(row, permitNumberCol).GetValue<string>();
            var fileName = worksheet.Cell(row, fileNameCol).GetValue<string>();
            var fileUrl = worksheet.Cell(row, fileUrlCol).GetValue<string>();
            var naldIssue = worksheet.Cell(row, naldIssueNumber).GetValue<string>();
            var dateOfIssue = worksheet.Cell(row, dateOfIssueCol).GetValue<string>();
            var signatureDate = worksheet.Cell(row, signatureDateCol).GetValue<string>();

            inputResults.Add(new TemplateFinderInput
            {
                PermitNumber = permitNumber,
                FileName = fileName,
                FileUrl = fileUrl,
                NaldIssueNumber = naldIssue,
                SignatureDate = signatureDate,
                DateOfIssue = dateOfIssue
            });
        }

        return inputResults;
    }
}
