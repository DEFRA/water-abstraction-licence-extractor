using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Helpers;

namespace WRADI.Services.ProcessFile.Implementations;

public class FileProcessOrchestrateService(
    FileProcessAppSettings settings,
    ICacheService cacheService,
    IOutputService outputService,
    IFileService fileService,
    IOrchestratorService orchestratorService)
    : IOrchestrateFileProcess
{
    public async Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        ConsoleHelper.WriteLine("INFO - OrchestrateFileProcessService - Started");

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
            ConsoleHelper.WriteLine("INFO - OrchestrateFileProcessService - No DMS files to process");
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
                await orchestratorService.AddToFileProcessQueue(
                    new SingleFileProcessRequest
                    {
                        FilePath = filePath,
                        ProcessRunId = processRun.ProcessRunId
                    });
                
                ConsoleHelper.WriteLine($"INFO - {filePath} - sent to single process file queue");
            }
        }
        catch (Exception e)
        {
            ConsoleHelper.WriteLine($"ERROR - OrchestrateFileProcessService - Error during sending to single " +
                $"file processing queue: {e}");
            
            throw;
        }
        
        ConsoleHelper.WriteLine($"INFO - OrchestrateFileProcessService - Finished processing " +
            $"at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        
        return true;
    }
}