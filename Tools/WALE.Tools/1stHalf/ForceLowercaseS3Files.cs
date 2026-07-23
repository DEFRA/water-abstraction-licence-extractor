using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Types;
using WALE.Tools.Config;

namespace WALE.Tools._1stHalf;

public static class ForceLowercaseS3Files
{
    public static async Task RunAsync()
    {
        var sourceHttpClient = HttpHelper.GetResilientHttpClient(
            KeyConfig.ApiBaseUrl,
            100,
            30);
        
        Console.WriteLine($"Started at {DateTime.Now}");
        Console.WriteLine($"Getting files from {sourceHttpClient.BaseAddress}");

        var sourceFileService = new ApiFileService(sourceHttpClient);
        var sourceFiles = await sourceFileService.GetAllFilesAsync();

        Console.WriteLine($"{sourceFiles.Count} files in s3");
        
        var uppercase = sourceFiles
            .Where(fileName => fileName.Any(char.IsUpper))
            .ToList();
        
        Console.WriteLine($"{uppercase.Count} uppercase files in S3");

        foreach (var uppercaseFile in uppercase)
        {
            var originalFilename = uppercaseFile;
            var newFileName = uppercaseFile.ToLower();
            
            await sourceFileService.RenameAsync(
                uppercaseFile,
                uppercaseFile.ToLower());
            
            Console.WriteLine($"{originalFilename} renamed to {newFileName}");
        }
        
        Console.WriteLine("Done");
    }
}