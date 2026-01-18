
using ClosedXML.Excel;
using Tesseract;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.RuleEngine.Services;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.Tools.Config;
using WALE.Tools.Models;

namespace WALE.Tools;

public static class TemplateIdentificationExtract
{
    private static readonly string OutputFolder = KeyConfig.OutputFolder;
    private static readonly object CsvLock = new object();

    private static string GetCsvFilePath()
    {
        return Path.Combine(KeyConfig.PdfFolder, $"Template_Finder_Results_{DateTime.Today:yyyyMMdd}.csv");
    }

    private static HashSet<string> ReadProcessedFiles()
    {
        var csvFilePath = GetCsvFilePath();
        var processedFiles = new HashSet<string>();

        if (!File.Exists(csvFilePath))
        {
            return processedFiles;
        }

        try
        {
            var lines = File.ReadAllLines(csvFilePath);
            // Skip header row
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length > 0)
                {
                    // Use PermitNumber as the unique identifier
                    var permitNumber = parts[0].Trim('"');
                    if (!string.IsNullOrEmpty(permitNumber))
                    {
                        processedFiles.Add(permitNumber);
                    }
                }
            }

            Console.WriteLine($"Found {processedFiles.Count} already processed files in CSV");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading processed files from CSV: {ex.Message}");
        }

        return processedFiles;
    }

    private static void AppendResultToCsv(TemplateFinderInput result)
    {
        var csvFilePath = GetCsvFilePath();

        lock (CsvLock)
        {
            try
            {
                var fileExists = File.Exists(csvFilePath);

                using var writer = new StreamWriter(csvFilePath, append: true);

                // Write header if file doesn't exist
                if (!fileExists)
                {
                    writer.WriteLine("PermitNumber,DateOfIssue,SignatureDate,FileUrl,FileName,NaldIssueNumber,Header,NumberOfPages,TemplateType,Template");
                }

                // Write the result row
                var line = $"\"{EscapeCsv(result.PermitNumber)}\",\"{EscapeCsv(result.DateOfIssue)}\",\"{EscapeCsv(result.SignatureDate)}\",\"{EscapeCsv(result.FileUrl)}\",\"{EscapeCsv(result.FileName)}\",\"{EscapeCsv(result.NaldIssueNumber)}\",\"{EscapeCsv(result.Header)}\",{result.NumberOfPages},\"{EscapeCsv(result.TemplateType)}\",\"{EscapeCsv(result.Template)}\"";
                writer.WriteLine(line);
                writer.Flush();

                Console.WriteLine($"Saved result to CSV: {result.PermitNumber}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to CSV: {ex.Message}");
            }
        }
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Escape quotes by doubling them
        return value.Replace("\"", "\"\"");
    }

    public static async Task GenerateTemplateFinderResult(string region)
    {
        var data = await GetTemplateFinderDataAsync(region);

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

    static async Task<List<TemplateFinderInput>> GetTemplateFinderDataAsync(
        string region)
    {
        var returnList = new List<TemplateFinderInput>();

        // Get all PDF files from the folder
        var allPdfFiles = Directory
            .GetFiles(KeyConfig.PdfFolder)
            .Where(fileName => fileName.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
            .Select(x => x.Split('/').Last())
            .OrderBy(x => x)
            .ToList();

        // Read already processed files
        var processedFiles = ReadProcessedFiles();

        // Create file entries from PDF files and filter out already processed ones
        var filesToProcess = allPdfFiles
            .Select(fileName =>
            {
                // Extract permit number from filename (format: PermitNumber - rest.pdf)
                var permitNumber = fileName.Split('-').FirstOrDefault()?.Trim() ?? fileName;
                return new { FileName = fileName, PermitNumber = permitNumber };
            })
            .Where(x => !processedFiles.Contains(x.PermitNumber))
            .Select(x => new TemplateFinderInput
            {
                PermitNumber = x.PermitNumber,
                FileName = x.FileName,
                DateOfIssue = string.Empty,
                SignatureDate = string.Empty,
                FileUrl = string.Empty,
                NaldIssueNumber = string.Empty
            })
            .ToList();

        Console.WriteLine($"Total PDF files: {allPdfFiles.Count}, Already processed: {processedFiles.Count}, Remaining: {filesToProcess.Count}");

        if (filesToProcess.Count == 0)
        {
            Console.WriteLine("All files have been processed!");
            return returnList;
        }

        // Group files into batches of 15
        const int batchSize = 15;
        var batches = filesToProcess
            .Select((templateFile, index) => new { templateFile, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.templateFile).ToList())
            .ToList();

        Console.WriteLine($"Processing {filesToProcess.Count} files in {batches.Count} batches of {batchSize}");

        var postgresDataSourceProvider = new NpgsqlDataSourceProvider(KeyConfig.PostgresConnectionString);
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        var databaseReadService = new PostgresReadService(postgresDataSourceProvider);
        var databaseAddService = new PostgresWriteService(postgresDataSourceProvider);
        var cacheService = new DatabaseCacheService(databaseReadService, databaseAddService, KeyConfig.PostgresConnectionString);
        var outputService = new DatabaseOutputService(databaseReadService, databaseAddService);
        var dotnetPath = KeyConfig.DotnetPath;
        var tesseractExeName = KeyConfig.TesseractExeName;
        var tesseractExeDirectory = KeyConfig.TesseractExeDirectory;

        foreach (var batch in batches)
        {
            Console.WriteLine($"Processing batch with {batch.Count} files...");

            // Create a separate pdfDataExtractor for each file in the batch
            var pdfDataExtractors = new List<PdfDataExtractorService>();
            var templateTypeServices = new List<TemplateTypeIdentifierService>();

            for (int i = 0; i < batch.Count; i++)
            {
                var extractor = new PdfDataExtractorService(
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
                    KeyConfig.PdfFolder);

                pdfDataExtractors.Add(extractor);
                templateTypeServices.Add(new TemplateTypeIdentifierService(extractor, region));
            }

            var batchTasks = batch.Select((templateFile, index) => Task.Run(async () =>
            {
                var templateTypeService = templateTypeServices[index];

                var pdfFileName = templateFile.FileName;
                if (string.IsNullOrEmpty(pdfFileName)) return null;

                try
                {
                    Console.WriteLine($"Processing file: {pdfFileName}");

                    // Check if file exists
                    var fullPath = Path.Combine(KeyConfig.PdfFolder, pdfFileName);
                    if (!File.Exists(fullPath))
                    {
                        throw new FileNotFoundException($"PDF file not found: {fullPath}");
                    }

                    Console.WriteLine($"File exists, attempting to identify template...");

                    // Use the TemplateTypeIdentifierService to identify the template
                    // The service will use configurations from RuleConfiguration folder internally
                    var templateResult = await templateTypeService.IdentifyTemplateTypeAsync(fullPath);

                    Console.WriteLine($"Template identification completed successfully for {pdfFileName}");

                    TemplateFinderInput result;
                    if (templateResult != null)
                    {
                        result = new TemplateFinderInput
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
                        result = new TemplateFinderInput
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

                    // Save result to CSV immediately
                    AppendResultToCsv(result);
                    return result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing file {pdfFileName}:");
                    Console.WriteLine($"  Exception Type: {ex.GetType().Name}");
                    Console.WriteLine($"  Message: {ex.Message}");
                    Console.WriteLine($"  Stack Trace: {ex.StackTrace}");

                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"  Inner Exception: {ex.InnerException.GetType().Name}");
                        Console.WriteLine($"  Inner Message: {ex.InnerException.Message}");
                    }

                    // Return a failed result for tracking
                    var errorResult = new TemplateFinderInput
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
                        Template = $"Error: {ex.Message}"
                    };

                    // Save error result to CSV immediately
                    AppendResultToCsv(errorResult);
                    return errorResult;
                }
            }));

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
