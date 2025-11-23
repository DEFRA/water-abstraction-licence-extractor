using System.Globalization;
using System.Security.Cryptography;
using ClosedXML.Excel;
using CsvHelper;
using WALE.Tools.Models;

namespace WALE.Tools;

public static class DuplicateLicenceIdentificationExtract
{
    private static readonly string OutputFolder = KeyConfig.OutputFolder;

    public static async Task GenerateDuplicateLicenceIdentificationExtractAsync(bool byFileName = true)
    {
        // Step 1: Get the input of processing by reading DuplicateResults_Extract.xlsx from KeyConfig.PdfFolderForDuplicates into a list of LicenceDuplicateFinderInput objects
        var duplicateInputs = await ReadDuplicateResultsFromExcelAsync();
        
        // Step 2: Group by Permit Number and loop over each group to process each group and generate a result list of LicenceDuplicateCsvLine objects
        var csvResults = new List<LicenceDuplicateCsvLine>();

        var groupedByPermit = 
             duplicateInputs
            .Where(x => !string.IsNullOrEmpty(x.PermitNumber))
            .GroupBy(x => x.PermitNumber); 
        var groupedByPermitAndSize =
        duplicateInputs
            .Where(x => !string.IsNullOrEmpty(x.PermitNumber))
            .GroupBy(x => (x.PermitNumber, x.FileSize));

        if (byFileName)
        {
            foreach (var group in groupedByPermit)
            {
                // Step 3: Get the list of files for the current permit number
                var filesForPermit = group.ToList();
            
                // Step 4: Read the pdf files for the current permit number from KeyConfig.PdfFolderForDuplicates
                var pdfFilesData = ReadPdfFilesForPermit(filesForPermit);
            
                // Step 5 - Identify the main file in pdfFilesData which is the file that has a name the other files are sub strings of
                // for example 0-034 5665255.PDF is main file in a group with that and -034 5665255.PDF
                var mainFile = IdentifyMainFile(pdfFilesData);
            
                // Step 6 - Process the main file and compare with other files for duplicate analysis
                if (mainFile.HasValue && mainFile.Value.FileExists && pdfFilesData.Count > 1)
                {
                    var duplicateResults = await ComparePdfContentAsync(mainFile.Value, pdfFilesData, group.Key);
                    csvResults.AddRange(duplicateResults);
                }
            }
        }
        else
        {
            const int batchSize = 1;
            var batches = groupedByPermitAndSize
                .Select((file, index) => new { file, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.file).ToList())
                .ToList();

            foreach (var batch in batches)
            {
                var batchTasks = batch.Select(async group =>
                {
                    try
                    {
                        // Step 3: Get the list of files for the current permit number
                        var filesForPermit = group.ToList();

                        // Step 4: Read the pdf files for the current permit number from KeyConfig.PdfFolderForDuplicates
                        var firstFile = filesForPermit
                            ?.Where(f => !string.IsNullOrWhiteSpace(f.FileName))
                            ?.FirstOrDefault();
                        if (firstFile != null)
                        {
                            var pdfFilesData = ReadPdfFilesForPermitAndSize(firstFile, filesForPermit);

                            var duplicateResults = await ComparePdfContentAsync(firstFile, pdfFilesData, group.Key.PermitNumber);
                            csvResults.AddRange(duplicateResults);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but continue processing other files
                        Console.WriteLine($"Error processing file {group.Key}: {ex.Message}");
                    }
                });

                // Wait for all tasks in the current batch to complete
                await Task.WhenAll(batchTasks);
            }
        }
        

        var fileName = $"Duplicate--Licence--Extract-{DateTime.Today:yyyyMMdd}.csv";
        var fullPath = Path.Combine(OutputFolder, fileName);
        await using var writer = new StreamWriter(fullPath);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csv.WriteRecordsAsync(csvResults);
    }
    private static async Task<List<LicenceDuplicateCsvLine>> ComparePdfContentAsync(
        (string FileName, string FilePath, bool FileExists, string FileUrl) mainFile,
        List<(string FileName, string FilePath, bool FileExists, string FileUrl)> allFiles,
        string permitNumber)
    {
        var results = new List<LicenceDuplicateCsvLine>();

        Console.WriteLine($"Comparing files for permit {permitNumber}");
        Console.WriteLine($"Main file: {mainFile.FileName}");

        try
        {
            // Compare with other files
            var otherFiles = allFiles.Where(f => f.FileName != mainFile.FileName && f.FileExists).ToList();
            Console.WriteLine($"Comparing with {otherFiles.Count} other files");

            foreach (var otherFile in otherFiles)
            {
                try
                {
                    Console.WriteLine($"Comparing with: {otherFile.FileName}");

                    // Compare images using hash comparison
                    var isDuplicate = await CompareImagesAsync(new List<string>{mainFile.FilePath}, new List<string>{otherFile.FilePath});

                    if (isDuplicate)
                    {
                        results.Add(new LicenceDuplicateCsvLine
                        {
                            PermitNumber = permitNumber,
                            FileName = mainFile.FileName,
                            FileUrl = mainFile.FileUrl,
                            DuplicateFileName = otherFile.FileName,
                            DuplicateFileUrl = otherFile.FileUrl
                        });

                        Console.WriteLine($"✓ Duplicate found: {mainFile.FileName} == {otherFile.FileName}");
                    }
                    else
                    {
                        results.Add(new LicenceDuplicateCsvLine
                        {
                            PermitNumber = permitNumber,
                            FileName = mainFile.FileName,
                            FileUrl = mainFile.FileUrl,
                            DuplicateFileName = string.Empty,
                            DuplicateFileUrl = string.Empty,
                        });
                        Console.WriteLine($"✗ Not duplicate: {mainFile.FileName} != {otherFile.FileName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error comparing {otherFile.FileName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing main file {mainFile.FileName}: {ex.Message}");
        }

        return results;
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
        var fileNameCol = headerMapping["Downloaded file name"];
        var fileUrlCol = headerMapping["File URL"];
        var fileSizeCol = headerMapping["File Size"];

        // Read data rows starting from row 2
        for (int row = 2; row <= usedRange.LastRow().RowNumber(); row++)
        {
            var permitNumber = worksheet.Cell(row, permitNumberCol).GetValue<string>();
            var fileName = worksheet.Cell(row, fileNameCol).GetValue<string>();
            var fileUrl = worksheet.Cell(row, fileUrlCol).GetValue<string>();
            var fileSize = worksheet.Cell(row, fileSizeCol).GetValue<string>();

            duplicateInputs.Add(new LicenceDuplicateFinderInput
            {
                PermitNumber = permitNumber,
                FileName = fileName,
                FileUrl = fileUrl,
                FileSize = fileSize
            });
        }

        return duplicateInputs;
    }

    private static List<(string FileName, string FilePath, bool FileExists, string FileUrl)> ReadPdfFilesForPermit(
        List<LicenceDuplicateFinderInput> filesForPermit)
    {
        var pdfFilesData = new List<(string FileName, string FilePath, bool FileExists, string FileUrl)>();

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
                        pdfFilesData.Add((actualFileName, matchingFile, true, file.FileUrl ?? ""));
                    }
                }
                else
                {
                    // Fallback: try direct filename match
                    var directPath = Path.Combine(KeyConfig.PdfFolderForDuplicates, file.FileName);
                    var directExists = File.Exists(directPath);

                    pdfFilesData.Add((file.FileName, directPath, directExists, file.FileUrl ?? ""));

                    if (!directExists)
                    {
                        Console.WriteLine($"Warning: PDF file not found with pattern *__{file.FileName} or direct match: {file.FileName}");
                    }
                }
            }
        }

        return pdfFilesData;
    }
    
    private static List<LicenceDuplicateFinderInput> ReadPdfFilesForPermitAndSize(
        LicenceDuplicateFinderInput file, List<LicenceDuplicateFinderInput> filesForPermitAndSize)
    {
        if (!string.IsNullOrEmpty(file.FileName))
        {
            return filesForPermitAndSize
                .Where(f => f.FileSize.Equals(file.FileSize, StringComparison.InvariantCultureIgnoreCase) &&
                            !f.FileUrl.Equals(file.FileUrl, StringComparison.InvariantCultureIgnoreCase)).ToList();
        }

        return new List<LicenceDuplicateFinderInput>();
    }

    private static (string FileName, string FilePath, bool FileExists, string FileUrl)? IdentifyMainFile(
        List<(string FileName, string FilePath, bool FileExists, string FileUrl)> pdfFilesData)
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

    private static async Task<List<LicenceDuplicateCsvLine>> ComparePdfContentAsync(
        LicenceDuplicateFinderInput mainFile,
        List<LicenceDuplicateFinderInput> allFiles,
        string permitNumber)
    {
        var results = new List<LicenceDuplicateCsvLine>();

        Console.WriteLine($"Comparing files for permit {permitNumber}");
        Console.WriteLine($"Main file: {mainFile.FileName}");

        try
        {
            // Compare with other files
            var otherFiles = allFiles.Where(f => f.FileName != mainFile.FileName).ToList();
            Console.WriteLine($"Comparing with {otherFiles.Count} other files");

            foreach (var otherFile in otherFiles)
            {
                try
                {
                    Console.WriteLine($"Comparing with: {otherFile.FileName}");
                
                    // Compare images using hash comparison
                    var isDuplicate = await CompareImagesAsync(new List<string>{Path.Combine(KeyConfig.PdfFolderForDuplicates,mainFile.FileName)}, new List<string>{Path.Combine(KeyConfig.PdfFolderForDuplicates,otherFile.FileName)});

                    if (isDuplicate)
                    {
                        results.Add(new LicenceDuplicateCsvLine
                        {
                            PermitNumber = permitNumber,
                            FileName = mainFile.FileName,
                            FileUrl = mainFile.FileUrl,
                            DuplicateFileName = otherFile.FileName,
                            DuplicateFileUrl = otherFile.FileUrl
                        });

                        Console.WriteLine($"✓ Duplicate found: {mainFile.FileName} == {otherFile.FileName}");
                    }
                    else
                    {
                        results.Add(new LicenceDuplicateCsvLine
                        {
                            PermitNumber = permitNumber,
                            FileName = mainFile.FileName,
                            FileUrl = mainFile.FileUrl,
                            DuplicateFileName = string.Empty,
                            DuplicateFileUrl = string.Empty,
                        });
                        Console.WriteLine($"✗ Not duplicate: {mainFile.FileName} != {otherFile.FileName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error comparing {otherFile.FileName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing main file {mainFile.FileName}: {ex.Message}");
        }

        return results;
    }
    private static async Task<bool> CompareImagesAsync(List<string> mainFileImages, List<string> otherFileImages)
    {
        if (mainFileImages.Count != otherFileImages.Count)
            return false;

        // Calculate hashes for all images in both sets
        var mainHashes = new List<string>();
        var otherHashes = new List<string>();

        foreach (var imagePath in mainFileImages)
        {
            var hash = await CalculateImageHashAsync(imagePath);
            if (!string.IsNullOrEmpty(hash))
                mainHashes.Add(hash);
        }

        foreach (var imagePath in otherFileImages)
        {
            var hash = await CalculateImageHashAsync(imagePath);
            if (!string.IsNullOrEmpty(hash))
                otherHashes.Add(hash);
        }

        if (mainHashes.Count != otherHashes.Count)
            return false;

        // Sort both hash lists and compare
        mainHashes.Sort();
        otherHashes.Sort();

        return mainHashes.SequenceEqual(otherHashes);
    }

    private static async Task<string> CalculateImageHashAsync(string imagePath)
    {
        try
        {
            Console.WriteLine($"Checking image path: {imagePath}");

            // Try to resolve relative paths to absolute paths
            var resolvedPath = imagePath;
            if (!Path.IsPathRooted(imagePath))
            {
                resolvedPath = Path.GetFullPath(imagePath);
                Console.WriteLine($"Resolved to absolute path: {resolvedPath}");
            }

            Console.WriteLine($"File exists check for: {resolvedPath} = {File.Exists(resolvedPath)}");
            if (!File.Exists(resolvedPath))
            {
                Console.WriteLine($"File not found at resolved path, trying original path: {imagePath}");
                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"File not found at original path either: {imagePath}");
                    return string.Empty;
                }
                resolvedPath = imagePath;
            }

            using var stream = File.OpenRead(resolvedPath);
            using var sha256 = SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            return Convert.ToHexString(hashBytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calculating hash for image {imagePath}: {ex.Message}");
            return string.Empty;
        }
    }
}
