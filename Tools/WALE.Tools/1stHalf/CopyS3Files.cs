using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;

namespace WALE.Tools._1stHalf;

public static class CopyS3Files
{
    public static async Task RunAsync()
    {
        var sourceHttpClient = HttpHelper.GetResilientHttpClient(
            KeyConfig.ApiBaseUrl,
            100,
            30);
        
        var destinationHttpClient = HttpHelper.GetResilientHttpClient(
            "https://wli-api-tst.aws-int.defra.cloud",
            100,
            30);
        
        var sourceFileService = new ApiFileService(sourceHttpClient);
        var destinationFileService = new ApiFileService(destinationHttpClient);

        var sourceFiles = await sourceFileService.GetAllFilesAsync();
        var existingDestinationFiles = await destinationFileService.GetAllFilesAsync();

        // For debugging / limiting file uploads
        sourceFiles = sourceFiles
            .Where(x => x.StartsWith("032864"))
            .Take(10)
            .ToList();
        
        var missingFiles = sourceFiles
            .Except(existingDestinationFiles)
            .ToList();
        
        var copyingTasks = new List<Task>();
        const int maxConcurrent = 5;
        var loopIdx = 1;
        
        foreach (var sourceFilename in missingFiles)
        {
            copyingTasks.Add(
                CopyFileAsync(
                    sourceFilename,
                    sourceFileService,
                    destinationFileService,
                    loopIdx++,
                    missingFiles.Count));
            
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
        }
        
        foreach (var copyingTask in copyingTasks)
        {
            await copyingTask;
        }
    }

    private static async Task CopyFileAsync(
        string filename,
        ApiFileService sourceFileService,
        ApiFileService destinationFileService,
        int loopIdx,
        int totalFiles)
    {
        try
        {
            var sourceFileStream = await sourceFileService.GetFileAsStreamAsync(filename);

            if (sourceFileStream == null)
            {
                return;
            }

            const int chunkSize = 5 * 1024 * 1024; // 5MB
            var isChunked = sourceFileStream.Length > chunkSize;

            if (isChunked)
            {
                var chunkIndex = 0;
                var totalChunks = Convert.ToInt32(Math.Ceiling(sourceFileStream.Length / (double)chunkSize));

                string? uploadId = null;
                var fullByteArray = GetByteArray(sourceFileStream);

                while (chunkIndex < totalChunks)
                {
                    var chunkOfByteArray = fullByteArray
                        .Skip(chunkIndex * chunkSize)
                        .Take(chunkSize)
                        .ToArray();

                    var streamChunk = new MemoryStream(chunkOfByteArray);

                    var tempUploadId = await destinationFileService.UploadFileChunkAsync(
                        filename,
                        streamChunk,
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

            var additionalText = isChunked ? " (chunked)" : string.Empty;
            Console.WriteLine($"Uploaded file {loopIdx} of {totalFiles} - {filename}{additionalText}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR - Downloading or uploading file {filename} - {ex.Message} ({loopIdx} of {totalFiles} - {filename})");
        }
    }
    
    private static byte[] GetByteArray(Stream stream)
    {
        if (stream is MemoryStream memStream)
        {
            return memStream.ToArray();
        }

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        
        var bytes = memoryStream.ToArray();
        return bytes;
    }
}