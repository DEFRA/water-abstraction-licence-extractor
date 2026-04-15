using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.RuleEngine.Services;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WALE.Tools.Config;
using WALE.Tools.Models;

namespace WALE.Tools._1stHalf;

public static class TemplateIdentificationExtract
{
    private static readonly string OutputFolder = KeyConfig.OutputFolder;
    private static readonly Lock CsvLock = new();

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
            for (var i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length <= 0) continue;
                
                // Use PermitNumber as the unique identifier
                var permitNumber = parts[0].Trim('"');
                if (!string.IsNullOrEmpty(permitNumber))
                {
                    processedFiles.Add(permitNumber);
                }
            }

            ConsoleHelper.WriteLine($"Found {processedFiles.Count} already processed files in CSV");
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"Error reading processed files from CSV: {ex.Message}");
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

                ConsoleHelper.WriteLine($"Saved result to CSV: {result.PermitNumber}");
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"Error writing to CSV: {ex.Message}");
            }
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Escape quotes by doubling them
        return value.Replace("\"", "\"\"");
    }

    private static string? ExtractPermitNumber(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }

        var underscoreIndex = fileName.IndexOf("__", StringComparison.Ordinal);
        
        return underscoreIndex >= 0 
            ? fileName[..underscoreIndex].Trim() 
            : null;
    }
    
    private static string? ExtractFileId(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }

        var filenameParts = fileName.Split("__");
        var fileId = filenameParts.Length >= 3 && Guid.TryParse(filenameParts[1], out var _fileId)
            ? _fileId.ToString()
            : null;

        return fileId;
    }

    public static async Task GenerateTemplateFinderResult(string region)
    {
        var data = await GetTemplateFinderDataAsync(region);

        var fileName = $"Template_Finder-{DateTime.Today:yyyyMMdd}.xlsx";
        var fullPath = Path.Combine(OutputFolder, fileName);
        
        CreateExcelFileFromList(data, fullPath);
    }

    public static void GenerateWaterPdfsFolderInventory(string username)
    {
        ConsoleHelper.WriteLine("Starting WaterPdfs folder inventory generation...");

        try
        {
            var waterPdfFoldersStr = $"/Users/{username}/Downloads/2025_11_11_parts;" +
                $"/Users/{username}/Downloads/2025_12_12_parts;" +
                $"/Users/{username}/Downloads/2026_02_13_parts;" +
                $"/Users/{username}/Downloads/20260218-NW_dup;" +
                $"/Users/{username}/Downloads/Anglian_2026_01_12;" +
                $"/Users/{username}/Downloads/Anglian_20260225;" +
                $"/Users/{username}/Downloads/Anglian_overrides_2026_02_22;" +
                $"/Users/{username}/Downloads/DOI_Regression;" +
                $"/Users/{username}/Downloads/Fw_ Yorkshire 1.1 Licences;" +
                $"/Users/{username}/Downloads/NE_Oct_2025;" +
                $"/Users/{username}/Downloads/NE_WC_only;" +
                $"/Users/{username}/Downloads/WaterPdfs6000;" +
                $"/Users/{username}/Documents/GitHub/WaterPdfs";
        
            var waterPdfFolderPaths = waterPdfFoldersStr
                .Split(';')
                .ToList();
            
            if (waterPdfFolderPaths.Count == 0)
            {
                ConsoleHelper.WriteLine("No folders specified");
                return;
            }

            ConsoleHelper.WriteLine($"Found {waterPdfFolderPaths.Count} folder(s) to look at");

            // Collect all file information
            var filesMetadata = new List<(
                string FolderName,
                string? PermitNumber,
                string? FileId,
                string FileName,
                long FileSize,
                DateTime ModifiedTime)>();

            foreach (var folderPath in waterPdfFolderPaths)
            {
                ConsoleHelper.WriteLine($"Processing folder: {folderPath}");
                var folder = new DirectoryInfo(folderPath);
                
                try
                {
                    var filesInFolder = folder.GetFiles("*.pdf", SearchOption.AllDirectories);
                    ConsoleHelper.WriteLine($"  Found {filesInFolder.Length} file(s) in {folder.Name}");

                    foreach (var file in filesInFolder)
                    {
                        var permitNumber = ExtractPermitNumber(file.Name);
                        var fileId = ExtractFileId(file.Name);

                        if (!string.IsNullOrEmpty(fileId))
                        {
                            
                        }
                        
                        filesMetadata.Add((
                            FolderName: folder.Name,
                            PermitNumber: permitNumber,
                            FileId: fileId,
                            FileName: file.Name,
                            FileSize: file.Length,
                            ModifiedTime: file.LastWriteTime
                        ));
                    }
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLine($"  Error processing folder {folder.Name}: {ex.Message}");
                    throw;
                }
            }

            var fileMetadataOrderedByFolderNameAndPermitNumber = filesMetadata
                .OrderBy(tuple => tuple.FolderName)
                .ThenBy(tuple => tuple.PermitNumber)
                .ToList();
            
            // Generate CSV file
            var csvFileName = $"WaterPdfs_Inventory_{DateTime.Today:yyyyMMdd}.csv";
            var csvFilePath = Path.Combine(OutputFolder, csvFileName);

            using (var writer = new StreamWriter(csvFilePath))
            {
                // Write header
                writer.WriteLine("FolderName,PermitNumber,FileId,FileName,FileSizeBytes,ModifiedTime");
                
                // Write data rows
                foreach (var fileMetadata in fileMetadataOrderedByFolderNameAndPermitNumber)
                {
                    var line = $"\"{EscapeCsv(fileMetadata.FolderName)}\",\"{EscapeCsv(fileMetadata.PermitNumber)}\"" +
                        $",\"{EscapeCsv(fileMetadata.FileId)}\",\"{EscapeCsv(fileMetadata.FileName)}\"" +
                        $",{fileMetadata.FileSize},\"{fileMetadata.ModifiedTime:yyyy-MM-dd HH:mm:ss}\"";
                    
                    writer.WriteLine(line);
                }
            }

            ConsoleHelper.WriteLine($"Inventory CSV created successfully: {csvFilePath}");
            ConsoleHelper.WriteLine($"Total files processed: {filesMetadata.Count}");

            // Print summary by folder
            var summary = filesMetadata
                .GroupBy(tuple => tuple.FolderName)
                .Select(grp => new
                {
                    FolderName = grp.Key,
                    FileCount = grp.Count(),
                    TotalSize = grp.Sum(f => f.FileSize)
                })
                .ToList();

            ConsoleHelper.WriteLine("\nSummary by folder:");
            
            foreach (var summaryItem in summary)
            {
                ConsoleHelper.WriteLine($"  {summaryItem.FolderName}: {summaryItem.FileCount} files, {summaryItem.TotalSize:N0} bytes");
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"Error generating WaterPdfs folder inventory: {ex.Message}");
            ConsoleHelper.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static void CreateExcelFileFromList<T>(List<T> employees, string filePath)
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
            ConsoleHelper.WriteLine($"Excel file successfully created at: {filePath}");
        }
        catch (IOException ex)
        {
            ConsoleHelper.WriteLine($"Error saving file: {ex.Message}");
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

        ConsoleHelper.WriteLine($"Total PDF files: {allPdfFiles.Count}, Already processed: {processedFiles.Count}, Remaining: {filesToProcess.Count}");

        if (filesToProcess.Count == 0)
        {
            ConsoleHelper.WriteLine("All files have been processed!");
            return returnList;
        }

        // Group files into batches of 15
        const int batchSize = 15;
        var batches = filesToProcess
            .Select((templateFile, index) => new { templateFile, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.templateFile).ToList())
            .ToList();

        ConsoleHelper.WriteLine($"Processing {filesToProcess.Count} files in {batches.Count} batches of {batchSize}");
        
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(KeyConfig.ApiBaseUrl);
    
        var cacheService = new ApiCacheService(httpClient);
        var outputService = new ApiOutputService(httpClient);
        
        var dotnetPath = KeyConfig.DotnetPath;
        var tesseractExeName = KeyConfig.TesseractExeName;
        var tesseractExeDirectory = KeyConfig.TesseractExeDirectory;

        var pdfPigDocumentService = new PdfPigNoOcrPdfDocumentService();
        var docnetAlternativeDocumentService = new DocnetNoOcrAlternativePdfDocumentService();
        
        foreach (var batch in batches)
        {
            ConsoleHelper.WriteLine($"Processing batch with {batch.Count} files...");

            var templateTypeServices = new List<TemplateTypeIdentifierService>();

            // Create a separate pdfDataExtractor for each file in the batch
            //var pdfDataExtractors = new List<PdfDataExtractorService>();
            
            for (var i = 0; i < batch.Count; i++)
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
                    pdfPigDocumentService,
                    docnetAlternativeDocumentService);

                //pdfDataExtractors.Add(extractor);
                templateTypeServices.Add(new TemplateTypeIdentifierService(extractor, region));
            }

            var batchTasks = batch
                .Select((templateFile, index) => Task.Run(async () =>
                {
                    var templateTypeService = templateTypeServices[index];

                    var pdfFileName = templateFile.FileName;
                    if (string.IsNullOrEmpty(pdfFileName)) return null;

                    try
                    {
                        ConsoleHelper.WriteLine($"Processing file: {pdfFileName}");

                        // Check if file exists
                        var fullPath = Path.Combine(KeyConfig.PdfFolder, pdfFileName);
                        if (!File.Exists(fullPath))
                        {
                            throw new FileNotFoundException($"PDF file not found: {fullPath}");
                        }

                        ConsoleHelper.WriteLine($"File exists, attempting to identify template...");

                        // Use the TemplateTypeIdentifierService to identify the template
                        // The service will use configurations from RuleConfiguration folder internally
                        var templateResult = await templateTypeService.IdentifyTemplateTypeAsync(fullPath);

                        ConsoleHelper.WriteLine($"Template identification completed successfully for {pdfFileName}");

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
                        ConsoleHelper.WriteLine($"Error processing file {pdfFileName}:");
                        ConsoleHelper.WriteLine($"  Exception Type: {ex.GetType().Name}");
                        ConsoleHelper.WriteLine($"  Message: {ex.Message}");
                        ConsoleHelper.WriteLine($"  Stack Trace: {ex.StackTrace}");

                        if (ex.InnerException != null)
                        {
                            ConsoleHelper.WriteLine($"  Inner Exception: {ex.InnerException.GetType().Name}");
                            ConsoleHelper.WriteLine($"  Inner Message: {ex.InnerException.Message}");
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
            
            var validResults = batchResults
                .Where(result => result != null)
                .ToList();

            returnList.AddRange(validResults!);
            ConsoleHelper.WriteLine($"Batch completed. Processed {validResults.Count} files successfully.");
        }

        return returnList;
    }

    private static Task<List<TemplateFinderInput>> ReadTemplateReaderInput()
    {
        var excelFilePath = Path.Combine(KeyConfig.PdfFolder, "TemplateIdentificationResults.xlsx");
        var inputResults = new List<TemplateFinderInput>();

        using var workbook = new XLWorkbook(excelFilePath);
        var worksheet = workbook.Worksheet(1);
        var usedRange = worksheet.RangeUsed()!;

        // Read header row to create column mapping
        var headerMapping = new Dictionary<string, int>(); 
        for (var col = 1; col <= usedRange.LastColumn().ColumnNumber(); col++)
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
        for (var row = 2; row <= usedRange.LastRow().RowNumber(); row++)
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

        return Task.FromResult(inputResults);
    }
}