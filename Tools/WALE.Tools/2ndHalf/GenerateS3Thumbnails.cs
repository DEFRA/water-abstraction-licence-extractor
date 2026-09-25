using System.Net.Http.Headers;
using WALE.ProcessFile.Core.Helpers;
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
    
    public static async Task RunAsync(int processRunId)
    {
        ConsoleHelper.WriteLine("Started generating thumbnails");
        var fileIds = await GetFileIdsWithoutThumbnailAsync(processRunId);

        ConsoleHelper.WriteLine($"{fileIds.Count} need setting");
        var idx = 1;
        
        foreach (var fileId in fileIds)
        {
            var thumbnailGeneratorUrl = $"{KeyConfig.ApiBaseUrl}/BFF/Images/Thumbnail?fileId={fileId}&pageNumber=1&serviceName=PdfPig";
            
            var bytes = await HttpClient.GetByteArrayAsync(thumbnailGeneratorUrl);
            var s3Filename = $"thumbnail_{fileId.ToString().ToLower()}.jpg";

            var uploadUrl = new Uri($"{KeyConfig.ApiBaseUrl}/Extractor/Images/Upload");

            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(new MemoryStream(bytes));
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            form.Add(fileContent, "file", s3Filename);
            
            var response = await HttpClient.PutAsync(uploadUrl, form);
            
            var presignedUrl = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            if (string.IsNullOrEmpty(presignedUrl))
            {
                ConsoleHelper.WriteLine("WARNING - No url returned");
                continue;
            }
            
            await AbsLicenceOutputService.UpdateThumbnailPathAsync(fileId, presignedUrl);
            ConsoleHelper.WriteLine($"{idx++} of {fileIds.Count} done");
        }
    }

    private static async Task<List<Guid>> GetFileIdsWithoutThumbnailAsync(int processRunId)
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
                .Where(l => string.IsNullOrEmpty(l.ThumbnailUrl))
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