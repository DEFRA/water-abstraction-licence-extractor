using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Helpers;

namespace WRADI.Services.ProcessFile.Implementations;

public class FileProcessOrchestrationService(
    FileProcessAppSettings settings,
    ICacheService cacheService,
    IOutputService outputService,
    IFileService fileService,
    IMessageQueueService messageQueueService)
    : IFileProcessOrchestrator
{
    public async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - Started");

        if (settings.RefreshCache)
        {
            await cacheService.ClearCacheAsync();
        }
        
        await cacheService.SetupAsync();
        await outputService.SetupAsync();

        var (dmsFilesToProcess, allDmsData) =
            await DmsHelper.GetDmsFilesAndMappingAsync(
                fileService,
                string.Empty,
                false,
                cacheService);

        if (dmsFilesToProcess.Count == 0)
        {
            ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - No DMS files to process");
            return true;
        }
        
        var processRun = await outputService.StartProcessRunAsync(new ProcessRun
        {
            Description = $"Batch process request for single file process from: {fileService.FolderPath}",
            StartDateTimeUtc = DateTime.UtcNow,
            NumberOfFiles = dmsFilesToProcess.Count,
            Status = "Batch"
        });

        try
        {
            foreach (var (filePath, _) in dmsFilesToProcess)
            {
                await messageQueueService.AddToFileProcessQueue(
                    new FileProcessSingleRequest
                    {
                        FilePath = filePath,
                        ProcessRunId = processRun.ProcessRunId
                    });
                
                ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - {filePath} sent to single process file queue");
            }
        }
        catch (Exception exception)
        {
            ConsoleHelper.WriteLine($"ERROR - {nameof(FileProcessOrchestrationService)} - Error during sending to single " +
                $"file processing queue: {exception}");
            
            throw;
        }
        
        ConsoleHelper.WriteLine($"INFO - {nameof(FileProcessOrchestrationService)} - Finished processing " +
            $"at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        
        return true;
    }
}