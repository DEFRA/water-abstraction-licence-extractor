using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;

namespace WALE.Tools._1stHalf;

public static class CopyS3Files
{
    public static async Task RunAsync()
    {
        var sourceHttpClient = new HttpClient();
        sourceHttpClient.BaseAddress = new Uri(KeyConfig.ApiBaseUrl); // Source url
        var sourceFileService = new ApiFileService(sourceHttpClient);
        
        var destinationHttpClient = new HttpClient();
        destinationHttpClient.BaseAddress = new Uri("http://localhost:8080"); // Hardcoded destination url
        var destinationFileService = new ApiFileService(destinationHttpClient);

        var sourceFiles = await sourceFileService.GetAllFilesWithMetadataAsync(
            string.Empty,
            int.MaxValue);
        
        var copyingTasks = new List<Task>();
        const int maxConcurrent = 5;
        
        foreach (var filename in sourceFiles.Select(sourceFileMetadata => sourceFileMetadata.Filename))
        {
            copyingTasks.Add(CopyFileAsync(filename, sourceFileService, destinationFileService));
            
            if (copyingTasks.Count != maxConcurrent)
            {
                continue;
            }

            while (copyingTasks.Count >= maxConcurrent)
            {
                await Task.WhenAny(copyingTasks);
                
                var toRemoveList = copyingTasks
                    .Where(copyingTask => copyingTask.IsCompleted)
                    .ToList();

                foreach (var toRemoveItem in toRemoveList)
                {
                    copyingTasks.Remove(toRemoveItem);
                }
            }
            
            foreach (var copyingTask in copyingTasks)
            {
                await copyingTask;
            }
        }
    }

    private static async Task CopyFileAsync(
        string filename,
        ApiFileService sourceFileService,
        ApiFileService destinationFileService)
    {
        var sourceFileStream = await sourceFileService.GetFileAsStreamAsync(filename);

        if (sourceFileStream == null)
        {
            return;
        }
        
        const int chunkSize = 5 * 1024 * 1024; // 5MB
        
        if (sourceFileStream.Length > chunkSize)
        {
            var chunkIndex = 0;
            var totalChunks = Convert.ToInt32(Math.Ceiling(sourceFileStream.Length / (double)chunkSize));
                
            string? uploadId = null;
                
            while (chunkIndex < totalChunks)
            {
                var tempUploadId = await destinationFileService.UploadFileChunkAsync(
                    filename,
                    sourceFileStream,
                    chunkIndex++,
                    totalChunks,
                    uploadId);

                uploadId ??= tempUploadId;
            }
        }
        else
        {
            await destinationFileService.UploadFileAsStreamAsync(filename, sourceFileStream);
        }
    }
}