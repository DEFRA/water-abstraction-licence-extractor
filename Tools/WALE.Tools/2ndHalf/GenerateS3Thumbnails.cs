using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.Output;
using WALE.Tools.Config;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Services.Output.AbstractionLicence;

namespace WALE.Tools._2ndHalf;

public static class GenerateS3Thumbnails
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri(KeyConfig.ApiBaseUrl)
    };
    
    private static readonly IAbstractionLicenceOutputService AbsLicenceOutputService =
        new ApiAbstractionLicenceOutputService(HttpClient);
    
    private static readonly IOutputService OutputService =
        new ApiOutputService(HttpClient);

    public static async Task RunAsync(int processRunId)
    {
        ConsoleHelper.WriteLine("Started generating thumbnails");
        var fileIds = await GetFileIdsAsync(processRunId);

        ConsoleHelper.WriteLine($"{fileIds.Count} need setting");
        var idx = 1;
        
        foreach (var fileId in fileIds)
        {
            ConsoleHelper.WriteLine($"Generating thumbnail {idx++} of {fileIds.Count}");
            
            try
            {
                await OutputService.SaveThumbnailAsync(fileId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
    
    private static async Task<List<Guid>> GetFileIdsAsync(int processRunId)
    {
        var fileIds = new List<Guid>();
        var loopLicences = new List<Licence>();
        
        const int licencesToTake = 10;
        var first = true;
        var loopIdx = 0;
        
        while (first || loopLicences.Count == licencesToTake)
        {
            first = false;
            var startAt = loopIdx++ * licencesToTake;
            
            loopLicences = await AbsLicenceOutputService.GetLicencesAsync(processRunId, startAt, licencesToTake);
            var loopFileIds = loopLicences
                .Select(l => l.DmsFileId)
                .Where(fid => fid != null)
                .Select(fid => fid!.Value);
            
            fileIds.AddRange(loopFileIds);
        }
        
        var uniqueFileIds = fileIds
            .Distinct()
            .ToList();

        return uniqueFileIds;
    }
}