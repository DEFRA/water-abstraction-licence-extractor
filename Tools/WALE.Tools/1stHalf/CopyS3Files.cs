using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Types;
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

        Console.WriteLine($"Started at {DateTime.Now}");
        Console.WriteLine($"Copying files from {sourceHttpClient.BaseAddress} to {destinationHttpClient.BaseAddress}");

        var sourceFileService = new ApiFileService(sourceHttpClient);
        var destinationFileService = new ApiFileService(destinationHttpClient);

        var sourceFilesTask = sourceFileService.GetAllFilesAsync();
        var existingDestinationFiles = await destinationFileService.GetAllFilesAsync();

        Console.WriteLine($"{existingDestinationFiles.Count} existing destination files");
        
        // For debugging / limiting file uploads
        /*sourceFiles = sourceFiles
            .Where(x => x.StartsWith("0328640105__462b0c9c-682e-4dc0-bc76-85786c80baf7.pdf"))
            .Take(10)
            .ToList();*/
        
        var sourceFiles = await sourceFilesTask;
        Console.WriteLine($"{sourceFiles.Count} source files");
        
        var excludeIncorrectlyNamedFiles = sourceFiles
            .Where(filename => !filename.Any(char.IsUpper))
            .ToList();

        Console.WriteLine($"{excludeIncorrectlyNamedFiles.Count} correctly named files");
        
        var missingFiles = excludeIncorrectlyNamedFiles
            .Except(existingDestinationFiles)
            .ToList();
        
        var copyingTasks = new List<Task>();
        const int maxConcurrent = 20; // Might be a touch to high
        var loopIdx = 1;
        
        Console.WriteLine($"{missingFiles.Count} missing files");
        
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

        Console.WriteLine("Done");
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
            var dtStart = DateTime.UtcNow;
            
            var sourceFileStream = await sourceFileService.GetFileAsStreamAsync(filename);

            if (sourceFileStream == null)
            {
                return;
            }

            const int chunkSize = 5 * 1024 * 1024; // 5MB
            var fileLength = sourceFileStream.Length;
            
            var isChunked = fileLength > chunkSize;

            if (isChunked)
            {
                var chunkIndex = 0;
                var totalChunks = Convert.ToInt32(Math.Ceiling(fileLength / (double)chunkSize));

                string? uploadId = null;
                var fullByteArray = GetByteArray(sourceFileStream);

                while (chunkIndex < totalChunks)
                {
                    var chunkOfByteArray = fullByteArray
                        .Skip(chunkIndex * chunkSize)
                        .Take(chunkSize);

                    var streamChunk = new ByteStream(chunkOfByteArray, 0); // Length doesn't matter here

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

            var tsDuration = (DateTime.UtcNow - dtStart).TotalSeconds;
            
            var additionalText = isChunked ? " (chunked)" : string.Empty;
            Console.WriteLine($"Uploaded file {loopIdx} of {totalFiles} - {filename}{additionalText} - " +
                $"{fileLength / 1024.0 / 1024.0}mb in {tsDuration} seconds");
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