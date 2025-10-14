using System.Text.Json;
using SkiaSharp;
using UglyToad.PdfPig.Graphics.Colors;
using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Services;

public class DatabaseOutputService(
    IDatabaseReadService databaseReadService,
    IDatabaseAddService databaseAddService) : IOutputService
{
    public Task SetupAsync()
    {
        // Nothing to do in this case
        return Task.CompletedTask;
    }

    public Task<string> GetPageScreenshotReferenceAsync(int pageNumber, string pdfServiceName,
        string pdfFilePath)
    {
        var pdfFilename = pdfFilePath.Split('/').Last();
        return Task.FromResult($"Screenshot-{pdfFilename}-{pdfServiceName}-{pageNumber}");
    }
    
    public Task<ProcessRun> SaveProcessRunAsync(ProcessRun processRun)
    {
        return databaseAddService.AddProcessRunAsync(processRun);
    }

    public async Task SaveLicenceSetsAsync(IReadOnlyList<LicenceSet> licenceSets, string pdfFilePath)
    {
        foreach (var licenceSet in licenceSets)
        {
            var licenceSetStr = JsonSerializer.Serialize(licenceSet, JsonHelper.GetSerializerOptions());
            await databaseAddService.SaveLicenceSetAsync(licenceSetStr, licenceSet.LicenceSetId, licenceSet.ShortLicenceSetId);   
        }
    }

    public Task SaveLicenceAsync(Licence licence, string pdfFilePath)
    {
        var pdfFilename = pdfFilePath.Split('/').Last();
        var licenceStr = JsonSerializer.Serialize(licence, JsonHelper.GetSerializerOptions());
        
        return databaseAddService.SaveLicenceAsync(licenceStr, pdfFilename);
    }

    public Task SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath)
    {
        var pdfFilename = pdfFilePath.Split('/').Last();
        var matchesResultStr = JsonSerializer.Serialize(matchesResult, JsonHelper.GetSerializerOptions());
        
        return databaseAddService.SaveMatchResultAsync(matchesResultStr, pdfFilename);
    }
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData)
    {
        // Don't need to, as it can just get it on the fly
        return Task.CompletedTask;
    }

    public async Task SavePageScreenshotIfDoesntExistAsync(PdfDocument pdfDocument, int pageNumber, string noOcrServiceName,
        string pdfFilePath)
    {
        var pdfFilename = pdfFilePath.Split('/').Last();
        var screenshot = await databaseReadService.GetPageScreenshotAsync(pageNumber, pdfFilename, noOcrServiceName);

        if (screenshot != null)
        {
            return;
        }
        
        using var memoryStream = pdfDocument.GetPageAsSkBitmap(pageNumber, RGBColor.White);
        var bytes = await GetAsJpegAsync(memoryStream);
        
        await databaseAddService.SavePageScreenshotIfDoesntExistAsync(
            pageNumber,
            noOcrServiceName,
            pdfFilename,
            bytes);
    }

    public async Task SaveAllPagesTextIfDoesntExistAsync(List<DocumentLine> documentLines, string pdfFilePath, string noOcrServiceName)
    {
        var pdfFilename = pdfFilePath.Split('/').Last();
        var data = await databaseReadService.GetAllPagesTextAsync(pdfFilename, noOcrServiceName);

        if (data != null)
        {
            return;
        }
        
        var documentLinesStr = JsonSerializer.Serialize(documentLines, JsonHelper.GetSerializerOptions());
        await databaseAddService.SaveAllPagesTextIfDoesntExistAsync(documentLinesStr, pdfFilename, noOcrServiceName);
    }
    
    private static async Task<byte[]> GetAsJpegAsync(SKBitmap bitmap, int quality = 60)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        
        await using var stream = new MemoryStream();
        data.SaveTo(stream);
        
        await stream.FlushAsync();

        stream.Position = 0;
        var bytes = stream.ToArray();
        stream.Close();

        return bytes;
    }
}