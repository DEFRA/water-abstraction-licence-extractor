using DocumentFormat.OpenXml.Office2010.ExcelAc;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;
using WALE.Tools.Helpers;
using WALE.Tools.Models;

namespace WALE.Tools._1stHalf;

public static class RemoveRedundantFilesFromS3
{
    public static async Task RunAsync()
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(KeyConfig.ApiBaseUrl);

        var fileService = new ApiFileService(httpClient);
        var files = await fileService.GetAllFilesWithMetadataAsync(string.Empty, int.MaxValue);
        
        // TODO may need to implement pagination above
        
        var notWantedFiles = new List<FileMetadata>();

        foreach (var file in files)
        {
            if (!file.Filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                notWantedFiles.Add(file);
                continue;
            }
            
            var permitNumber = ExtractPermitNumber(file.Filename);
            var fileId = ExtractFileId(file.Filename);

            if (string.IsNullOrWhiteSpace(permitNumber) || fileId == null)
            {
                notWantedFiles.Add(file);
            }
        }

        var duplicates = files
            .GroupBy(file => file.Filename.ToLower())
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Skip(1))
            .ToList();
        
        notWantedFiles.AddRange(duplicates);

        var loopIndex = 0;
        
        foreach (var notWantedFile in notWantedFiles)
        {
            await fileService.DeleteAsync(notWantedFile.Filename);
            
            if (loopIndex++ % 50 == 0)
            {
                ConsoleHelper.WriteLine($"{loopIndex} duplicates deleted so far");
            }
        }
        
        ConsoleHelper.WriteLine($"{loopIndex} duplicates deleted");
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