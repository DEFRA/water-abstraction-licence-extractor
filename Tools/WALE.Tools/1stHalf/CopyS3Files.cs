using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;

namespace WALE.Tools._1stHalf;

public static class CopyS3Files
{
    public static async Task RunAsync()
    {
        var devHttpClient = new HttpClient();
        devHttpClient.BaseAddress = new Uri(KeyConfig.ApiBaseUrl); // DEV url
        var devFileService = new ApiFileService(devHttpClient);
        
        var tstHttpClient = new HttpClient();
        tstHttpClient.BaseAddress = new Uri(""); // Hardcoded TST url
        var tstFileService = new ApiFileService(tstHttpClient);

        var devFiles = await devFileService.GetAllFilesWithMetadataAsync(
            string.Empty,
            int.MaxValue);
        
        var copyingTasks = new List<Task>();
        const int maxConcurrent = 5;
        
        foreach (var filename in devFiles.Select(devFileMetadata => devFileMetadata.Filename))
        {
            copyingTasks.Add(CopyFileAsync(filename, devFileService, tstFileService));
            
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

    private static async Task CopyFileAsync(string filename, ApiFileService devFileService, ApiFileService tstFileService)
    {
        var devFileStream = await devFileService.GetFileAsStreamAsync(filename);

        if (devFileStream == null)
        {
            return;
        }
        
        const int chunkSize = 5 * 1024 * 1024; // 5MB
        
        if (devFileStream.Length > chunkSize)
        {
            var chunkIndex = 0;
            var totalChunks = Convert.ToInt32(Math.Ceiling(devFileStream.Length / (double)chunkSize));
                
            string? uploadId = null;
                
            while (chunkIndex < totalChunks)
            {
                var tempUploadId = await tstFileService.UploadFileChunkAsync(
                    filename,
                    devFileStream,
                    chunkIndex++,
                    totalChunks,
                    uploadId);

                uploadId ??= tempUploadId;
            }
        }
        else
        {
            await tstFileService.UploadFileAsStreamAsync(filename, devFileStream);
        }
    }
}