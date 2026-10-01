using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WRADI.Services.ProcessFile.AbstractionLicence.Implementations;

public class AbstractionLicenceFileProcessOrchestrationService(
    FileProcessAppSettings settings,
    ICacheService cacheService,
    IAbstractionLicenceCacheService abstractionLicenceCacheService,
    IOutputService outputService,
    IFileService fileService,
    IMessageQueueService messageQueueService)
    : IFileProcessOrchestrator
{
    public async Task<bool> RunAsync(FileProcessOrchestrationRequest request, CancellationToken cancellationToken)
    {
        ConsoleHelper.WriteLine($"INFO - {nameof(AbstractionLicenceFileProcessOrchestrationService)} - Started");

        if (settings.RefreshCache)
        {
            await cacheService.ClearCacheAsync();
        }
        
        await cacheService.SetupAsync();
        await outputService.SetupAsync();

        var (dmsFilesToProcess, _) =
            await DmsHelper.GetDmsAndNaldFilesAndMappingAsync(
                fileService,
                string.Empty,
                false,
                abstractionLicenceCacheService);
        
        if (request.RegionId != null)
        {
            dmsFilesToProcess = dmsFilesToProcess
                .Where(kvp => kvp.Value.Item2.RegionCode == request.RegionId.Value)
                .Take(request.MaxLicencesToTake)                
                .ToDictionary(
                    filePath => filePath.Key,
                    filePath => filePath.Value);
        }
        else
        {
            dmsFilesToProcess = dmsFilesToProcess
                .Take(request.MaxLicencesToTake)
                .ToDictionary(
                    filePath => filePath.Key,
                    filePath => filePath.Value);            
        }
        
        if (dmsFilesToProcess.Count == 0)
        {
            ConsoleHelper.WriteLine($"INFO - {nameof(AbstractionLicenceFileProcessOrchestrationService)} - No DMS files to process");
            return true;
        }
        
        var processRun = await outputService.StartProcessRunAsync(
            new ProcessRun
            {
                Description = $"Batch process request for single file process from: {fileService.IngressFolderPath}",
                StartDateTimeUtc = DateTime.UtcNow,
                NumberOfFiles = dmsFilesToProcess.Count,
                Status = "Batch",
                DocumentType = "AbstractionLicence"
            });

        try
        {
            foreach (var (filePath, (dmsFileData, naldLicence)) in dmsFilesToProcess)
            {
                await messageQueueService.AddToFileProcessQueue(
                    new FileProcessSingleRequest
                    {
                        FilePath = filePath,
                        DestinationFileName = dmsFileData.DestinationFileName,
                        DmsPath = dmsFileData.DmsPath,
                        FileId = dmsFileData.FileId,
                        PermitNumber = dmsFileData.PermitNumber,
                        RegionId = naldLicence.RegionCode,
                        ProcessRunId = processRun.ProcessRunId,
                        RequestedAt =  DateTime.UtcNow,
                        DocumentType = "AbstractionLicence"
                    });
                
                ConsoleHelper.WriteLine($"INFO - {nameof(AbstractionLicenceFileProcessOrchestrationService)} - {filePath} sent to single process file queue");
            }
        }
        catch (Exception exception)
        {
            ConsoleHelper.WriteLine($"ERROR - {nameof(AbstractionLicenceFileProcessOrchestrationService)} - Error during sending to single " +
                $"file processing queue: {exception}");
            
            throw;
        }
        
        ConsoleHelper.WriteLine($"INFO - {nameof(AbstractionLicenceFileProcessOrchestrationService)} - Finished processing " +
            $"at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        
        return true;
    }
}