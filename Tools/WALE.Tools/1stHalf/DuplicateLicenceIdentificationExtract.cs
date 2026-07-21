using System.Security.Cryptography;
using ClosedXML.Excel;
using WALE.ProcessFile.Core.Helpers;
using WALE.Tools.Config;
using WALE.Tools.Models;

namespace WALE.Tools._1stHalf;

public static class DuplicateLicenceIdentificationExtract
{
    private static readonly string OutputFolder = KeyConfig.OutputFolder;

    public static async Task<int> GenerateDuplicateLicenceIdentificationExtractAsync(
        string excelFilePath,
        string pdfFolder,
        bool byFileName)
    {
        // Step 1: Get the input of processing by reading Excel file into a list ofLicenceDuplicateFinderInput
        var potentialDuplicates = ReadPotentialDuplicatesExcelFile(excelFilePath);
        
        // Step 2: Group by Permit Number and loop over each group to process each group and generate
        // a result list of LicenceDuplicateCsvLine objects
        var outputResults = new List<LicenceDuplicateOutputLine>();

        if (byFileName)
        {
            var groupedByPermit = potentialDuplicates
                .Where(line => !string.IsNullOrEmpty(line.PermitNumber))
                .GroupBy(line => line.PermitNumber); 
            
            foreach (var permitGroup in groupedByPermit)
            {
                // Step 3: Get the list of files for the current permit number
                var filesForPermit = permitGroup.ToList();
            
                // Step 4: Read the pdf files for the current permit number from KeyConfig.PdfFolderForDuplicates
                var pdfFilesData = ReadPdfFilesForPermit(filesForPermit, pdfFolder);
            
                // Step 5 - Identify the primary file in pdfFilesData which is the file that has a name the other
                // files are sub strings of for example 0-034 5665255.PDF is primary file in a group with that
                // and -034 5665255.PDF
                var primaryFile = IdentifyPrimaryFile(pdfFilesData);
            
                // Step 6 - Process the primary file and compare with other files for duplicate analysis
                if (primaryFile?.FileExists != true || pdfFilesData.Count <= 1)
                {
                    continue;
                }
                
                var duplicateResults = await ComparePdfContentAsync(
                    primaryFile.Value,
                    pdfFilesData,
                    permitGroup.Key!);
                    
                outputResults.AddRange(duplicateResults);
            }
        }
        else
        {
            var groupedByPermitAndSize = potentialDuplicates
                .Where(line => !string.IsNullOrEmpty(line.PermitNumber))
                .GroupBy(line => (line.PermitNumber, line.FileSize));
            
            foreach (var permitGroup in groupedByPermitAndSize)
            {
                try
                {
                    // Step 3: Get the list of files for the current permit number
                    var filesForPermit = permitGroup.ToList();

                    // Step 4: Read the pdf files for the current permit number from KeyConfig.PdfFolderForDuplicates
                    var primaryFile = filesForPermit
                        .FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.FileName));

                    if (primaryFile == null)
                    {
                        continue;
                    }
                    
                    var potentialDuplicateFiles = GetPotentialDuplicatesForPermitAndSize(
                        primaryFile,
                        filesForPermit);
                    
                    var duplicateCsvLine = await ComparePdfContentAsync(
                        primaryFile,
                        potentialDuplicateFiles,
                        permitGroup.Key.PermitNumber!,
                        pdfFolder);

                    outputResults.AddRange(duplicateCsvLine);
                }
                catch (Exception ex)
                {
                    // Log the error but continue processing other files
                    ConsoleHelper.WriteLine($"Error processing file {permitGroup.Key}: {ex.Message}");
                }
            }
        }
        
        var fileName = $"Duplicate-Licence-Extract-{DateTime.Today:yyyyMMdd}.xlsx";
        var fullPath = Path.Combine(OutputFolder, fileName);
        
        // Create an Excel file with the results
        CreateExcelFileFromList(outputResults, fullPath);

        return 1;
    }

    private static void CreateExcelFileFromList<T>(List<T> results, string filePath)
    {
        // 2. Create a new Excel workbook
        var workbook = new XLWorkbook();

        // 3. Add a new worksheet and insert the list data, including headers
        // The 'true' argument in LoadFromCollection indicates that the first row is for headers
        var worksheet = workbook.Worksheets.Add("All_Results");
        worksheet.Cell(1, 1).InsertTable(results);

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
    
    private static async Task<List<LicenceDuplicateOutputLine>> ComparePdfContentAsync(
        (string FileName, string FilePath, bool FileExists, string FileUrl) primaryFile,
        List<(string FileName, string FilePath, bool FileExists, string FileUrl)> allFiles,
        string permitNumber)
    {
        var outputResults = new List<LicenceDuplicateOutputLine>();

        ConsoleHelper.WriteLine($"Comparing files for permit {permitNumber}");
        ConsoleHelper.WriteLine($"Primary file: {primaryFile.FileName}");

        try
        {
            // Compare with other files
            var otherFiles = allFiles.Where(f =>
                f.FileName != primaryFile.FileName && f.FileExists).ToList();
            
            ConsoleHelper.WriteLine($"Comparing with {otherFiles.Count} other files");

            foreach (var otherFile in otherFiles)
            {
                try
                {
                    ConsoleHelper.WriteLine($"Comparing with: {otherFile.FileName}");

                    // Compare files using hash comparison
                    var isDuplicate = await CompareFileHashesAsync([primaryFile.FilePath], [otherFile.FilePath]);

                    if (isDuplicate)
                    {
                        outputResults.Add(new LicenceDuplicateOutputLine
                        {
                            PermitNumber = permitNumber,
                            FileName = primaryFile.FileName,
                            FileUrl = primaryFile.FileUrl,
                            DuplicateFileName = otherFile.FileName,
                            DuplicateFileUrl = otherFile.FileUrl
                        });

                        ConsoleHelper.WriteLine($"✓ Duplicate found: {primaryFile.FileName} == {otherFile.FileName}");
                        continue;
                    }
                    
                    outputResults.Add(new LicenceDuplicateOutputLine
                    {
                        PermitNumber = permitNumber,
                        FileName = primaryFile.FileName,
                        FileUrl = primaryFile.FileUrl,
                        DuplicateFileName = string.Empty,
                        DuplicateFileUrl = string.Empty,
                    });
                    
                    ConsoleHelper.WriteLine($"✗ Not duplicate: {primaryFile.FileName} != {otherFile.FileName}");
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLine($"Error comparing {otherFile.FileName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"Error processing primary file {primaryFile.FileName}: {ex.Message}");
        }

        return outputResults;
    }
    
    private static List<LicenceDuplicateFinderInput> ReadPotentialDuplicatesExcelFile(string excelFilePath)
    {
        var potentialDuplicates = new List<LicenceDuplicateFinderInput>();

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

        var permitNumberCol = headerMapping["PermitNumber"];
        var fileNameCol = headerMapping["DestinationFileName"];
        var fileUrlCol = headerMapping["FullPath"];
        var fileSizeCol = headerMapping["Size"];

        // Read data rows starting from row 2
        for (var row = 2; row <= usedRange.LastRow().RowNumber(); row++)
        {
            var permitNumber = worksheet.Cell(row, permitNumberCol).GetValue<string>();
            var fileName = worksheet.Cell(row, fileNameCol).GetValue<string>();
            var fileUrl = worksheet.Cell(row, fileUrlCol).GetValue<string>();
            var fileSize = worksheet.Cell(row, fileSizeCol).GetValue<string>();

            potentialDuplicates.Add(new LicenceDuplicateFinderInput
            {
                PermitNumber = permitNumber,
                FileName = fileName,
                FileUrl = fileUrl,
                FileSize = fileSize
            });
        }

        return potentialDuplicates;
    }

    private static List<(string FileName, string FilePath, bool FileExists, string FileUrl)> ReadPdfFilesForPermit(
        List<LicenceDuplicateFinderInput> filesForPermit,
        string pdfFolder)
    {
        var pdfFilesData = new List<(string FileName, string FilePath, bool FileExists, string FileUrl)>();

        foreach (var file in filesForPermit)
        {
            if (string.IsNullOrEmpty(file.FileName))
            {
                continue;
            }
            
            // Search for files that end with the expected filename after number__ prefix
            var matchingFiles = Directory.GetFiles(pdfFolder, $"*__{file.FileName}")
                .Where(f => Path.GetFileName(f).Contains("__"))
                .ToList();

            if (matchingFiles.Any())
            {
                foreach (var matchingFile in matchingFiles)
                {
                    var actualFileName = Path.GetFileName(matchingFile);
                    pdfFilesData.Add((actualFileName, matchingFile, true, file.FileUrl ?? ""));
                }
                
                continue;
            }
            
            // Fallback: try direct filename match
            var directPath = Path.Combine(pdfFolder, file.FileName);
            var directExists = File.Exists(directPath);

            pdfFilesData.Add((file.FileName, directPath, directExists, file.FileUrl ?? ""));

            if (!directExists)
            {
                ConsoleHelper.WriteLine($"Warning: PDF file not found with pattern *__{file.FileName} or direct match: {file.FileName}");
            }
        }

        return pdfFilesData;
    }
    
    private static List<LicenceDuplicateFinderInput> GetPotentialDuplicatesForPermitAndSize(
        LicenceDuplicateFinderInput file,
        List<LicenceDuplicateFinderInput> filesForPermitAndSize)
    {
        if (string.IsNullOrEmpty(file.FileName))
        {
            return [];
        }
        
        return filesForPermitAndSize
            .Where(
                f => f.FileSize?.Equals(file.FileSize, StringComparison.OrdinalIgnoreCase) == true
                    && f.FileUrl?.Equals(file.FileUrl, StringComparison.InvariantCultureIgnoreCase) != true)
            .ToList();
    }

    private static (string FileName, string FilePath, bool FileExists, string FileUrl)? IdentifyPrimaryFile(
        List<(string FileName, string FilePath, bool FileExists, string FileUrl)> pdfFilesData)
    {
        if (pdfFilesData.Count <= 1)
        {
            return pdfFilesData.FirstOrDefault();
        }

        // The primary file is simply the one with the maximum number of characters
        var primaryFile = pdfFilesData
            .Where(file => file.FileExists)
            .OrderByDescending(file => Path.GetFileNameWithoutExtension(file.FileName).Length)
            .FirstOrDefault();

        return primaryFile.FileName != null
            ? primaryFile
            : pdfFilesData.FirstOrDefault(f => f.FileExists);
    }

    private static async Task<List<LicenceDuplicateOutputLine>> ComparePdfContentAsync(
        LicenceDuplicateFinderInput primaryFile,
        List<LicenceDuplicateFinderInput> allFiles,
        string permitNumber,
        string pdfFolder)
    {
        var outputResults = new List<LicenceDuplicateOutputLine>();

        ConsoleHelper.WriteLine($"Comparing files for permit {permitNumber} - primary file: {primaryFile.FileName}");

        try
        {
            // Compare with other files
            var otherFiles = allFiles
                .Where(f => f.FileUrl != primaryFile.FileUrl)
                .ToList();
            
            ConsoleHelper.WriteLine($"Comparing with {otherFiles.Count} other files");

            foreach (var otherFile in otherFiles)
            {
                try
                {
                    ConsoleHelper.WriteLine($"Comparing with: {otherFile.FileName}");
                
                    // Compare files using hash comparison
                    var isDuplicate = await CompareFileHashesAsync(
                        [Path.Combine(pdfFolder, primaryFile.FileName!)],
                        [Path.Combine(pdfFolder, otherFile.FileName!)]);

                    if (isDuplicate)
                    {
                        outputResults.Add(new LicenceDuplicateOutputLine
                        {
                            PermitNumber = permitNumber,
                            FileName = primaryFile.FileName,
                            FileUrl = primaryFile.FileUrl,
                            DuplicateFileName = otherFile.FileName,
                            DuplicateFileUrl = otherFile.FileUrl
                        });

                        ConsoleHelper.WriteLine($"✓ Duplicate found: {primaryFile.FileName} == {otherFile.FileName}");
                    }
                    else
                    {
                        outputResults.Add(new LicenceDuplicateOutputLine
                        {
                            PermitNumber = permitNumber,
                            FileName = primaryFile.FileName,
                            FileUrl = primaryFile.FileUrl,
                            DuplicateFileName = string.Empty,
                            DuplicateFileUrl = string.Empty,
                        });
                        
                        ConsoleHelper.WriteLine($"✗ Not duplicate: {primaryFile.FileName} != {otherFile.FileName}");
                    }
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLine($"Error comparing {otherFile.FileName}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"Error processing primary file {primaryFile.FileName}: {ex.Message}");
        }

        return outputResults;
    }
    
    private static async Task<bool> CompareFileHashesAsync(List<string> primaryFiles, List<string> otherFiles)
    {
        if (primaryFiles.Count != otherFiles.Count)
        {
            return false;
        }

        // Calculate hashes for all images in both sets
        var primaryHashes = new List<string>();
        var otherHashes = new List<string>();

        foreach (var primaryFile in primaryFiles)
        {
            var hash = await CalculateFileHashAsync(primaryFile);

            if (!string.IsNullOrEmpty(hash))
            {
                primaryHashes.Add(hash);
            }
        }

        foreach (var otherFile in otherFiles)
        {
            var hash = await CalculateFileHashAsync(otherFile);

            if (!string.IsNullOrEmpty(hash))
            {
                otherHashes.Add(hash);
            }
        }

        if (primaryHashes.Count != otherHashes.Count)
        {
            return false;
        }

        // Sort both hash lists and compare
        primaryHashes.Sort();
        otherHashes.Sort();

        return primaryHashes.SequenceEqual(otherHashes);
    }

    private static async Task<string> CalculateFileHashAsync(string filePath)
    {
        try
        {
            ConsoleHelper.WriteLine($"Checking file path: {filePath}");

            // Try to resolve relative paths to absolute paths
            var resolvedPath = filePath;
            
            if (!Path.IsPathRooted(filePath))
            {
                resolvedPath = Path.GetFullPath(filePath);
                ConsoleHelper.WriteLine($"Resolved to absolute path: {resolvedPath}");
            }

            ConsoleHelper.WriteLine($"File exists check for: {resolvedPath} = {File.Exists(resolvedPath)}");
            
            if (!File.Exists(resolvedPath))
            {
                ConsoleHelper.WriteLine($"File not found at resolved path, trying original path: {filePath}");
                
                if (!File.Exists(filePath))
                {
                    ConsoleHelper.WriteLine($"File not found at original path either: {filePath}");
                    return string.Empty;
                }
                
                resolvedPath = filePath;
            }

            await using var stream = File.OpenRead(resolvedPath);
            using var sha256 = SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(stream);
            
            return Convert.ToHexString(hashBytes);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"Error calculating hash for file {filePath}: {ex.Message}");
            return string.Empty;
        }
    }
}
