using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;
using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WALE.Tools._1stHalf;

public static class InventoryFileGenerator
{
    private static readonly string OutputFolder = KeyConfig.OutputFolder;

    public static async Task GenerateWaterPdfsFolderInventoryAsync(string folderPathUsername)
    {
        ConsoleHelper.WriteLine("Starting WaterPdfs folder inventory generation...");

        List<(
            string FolderName,
            string? PermitNumber,
            string? FileId,
            string FileName,
            long FileSize,
            DateTime ModifiedTime)> filesMetadata;

        var fileServiceType = "api";
        
        if (fileServiceType == "local")
        {
            filesMetadata = GenerateWaterPdfsFolderInventory_FileSystem(folderPathUsername);
        }
        else
        {
            filesMetadata = await GenerateWaterPdfsFolderInventory_ApiS3Async();
        }

        var fileMetadataOrderedByFolderNameAndPermitNumber = filesMetadata
            .OrderBy(tuple => tuple.FolderName)
            .ThenBy(tuple => tuple.PermitNumber)
            .ToList();
            
        // Generate CSV file
        var csvFileName = $"WaterPdfs_Inventory_{DateTime.Today:yyyyMMdd}.csv";
        var csvFilePath = Path.Combine(OutputFolder, csvFileName);

        await using (var writer = new StreamWriter(csvFilePath))
        {
            // Write header
            await writer.WriteLineAsync("FolderName,PermitNumber,FileId,FileName,FileSizeBytes,ModifiedTime");
            
            // Write data rows
            foreach (var fileMetadata in fileMetadataOrderedByFolderNameAndPermitNumber)
            {
                var line = $"\"{EscapeCsv(fileMetadata.FolderName)}\",\"{EscapeCsv(fileMetadata.PermitNumber)}\"" +
                    $",\"{EscapeCsv(fileMetadata.FileId)}\",\"{EscapeCsv(fileMetadata.FileName)}\"" +
                    $",{fileMetadata.FileSize},\"{fileMetadata.ModifiedTime:yyyy-MM-dd HH:mm:ss}\"";
                
                await writer.WriteLineAsync(line);
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

    private static async Task<List<(
        string FolderName,
        string? PermitNumber,
        string? FileId,
        string FileName,
        long FileSize,
        DateTime ModifiedTime)>> GenerateWaterPdfsFolderInventory_ApiS3Async()
    {
        // Collect all file information
        var filesMetadata = new List<(
            string FolderName,
            string? PermitNumber,
            string? FileId,
            string FileName,
            long FileSize,
            DateTime ModifiedTime)>();

        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(KeyConfig.ApiBaseUrl);

        var fileService = new ApiFileService(httpClient);

        var allPdfFileNames = (await fileService.GetAllFilesWithMetadataAsync(string.Empty, int.MaxValue))
            .OrderBy(fm => fm.Filename)
            .ToList();

        // TODO - Need to implement pagination above
        
        foreach (var fileMetadata in allPdfFileNames)
        {
            var filenameParts = fileMetadata.Filename.Split("__");

            if (filenameParts.Length != 2)
            {
                continue;
            }
            
            var fileIdPart = filenameParts[1].Split('.')[0];

            if (!Guid.TryParse(fileIdPart, out var fileId))
            {
                continue;
            }

            if (fileId == Guid.Empty)
            {
                continue;
            }

            var permitNumber = SharedHelper.ExtractPermitNumberFromFilename(fileMetadata.Filename);
            
            filesMetadata.Add((
                "S3",
                permitNumber,
                fileId.ToString(),
                fileMetadata.Filename,
                fileMetadata.Filesize,
                fileMetadata.ModifiedTime));
        }
        
        return filesMetadata;
    }

    private static List<(
        string FolderName,
        string? PermitNumber,
        string? FileId,
        string FileName,
        long FileSize,
        DateTime ModifiedTime)> GenerateWaterPdfsFolderInventory_FileSystem(string username)
    {
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
                throw new Exception("No folders specified");
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
                        
                        filesMetadata.Add((
                            FolderName: folder.Name,
                            PermitNumber: permitNumber,
                            FileId: fileId.ToString(),
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

            return filesMetadata;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"Error generating WaterPdfs folder inventory: {ex.Message}");
            ConsoleHelper.WriteLine($"Stack trace: {ex.StackTrace}");

            throw;
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
    
    private static Guid? ExtractFileId(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        var filenameParts = fileName.Split("__");
        var fileIdWithExtension = filenameParts.LastOrDefault()?.Trim();
        
        var fileIdString = fileIdWithExtension!.Split('.')[0];
        
        return Guid.TryParse(fileIdString, out var fileIdOut)
            ? fileIdOut
            : null;
    }
}