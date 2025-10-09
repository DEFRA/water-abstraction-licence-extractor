using System.Text.Json;
using SkiaSharp;
using UglyToad.PdfPig.Graphics.Colors;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services.PdfPig;

namespace WALE.ProcessFile.Services.Services;

public class FileSystemOutputService(string outputFolder) : IOutputService
{
    public Task SetupAsync()
    {
        Directory.CreateDirectory(outputFolder);
        return Task.CompletedTask;
    }
    
    public Task<ProcessRun> RecordProcessRunStartAsync(ProcessRun processRun)
    {
        // This mode doesn't need to do anything here
        return Task.FromResult(processRun);
    }

    public Task SaveLicenceSetsAsync(
        IReadOnlyList<LicenceSet> licenceSets,
        string pdfFilePath)
    {
        var licenceSetsJson = JsonHelper.GetAsString(licenceSets);
        var folderName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        
        return File.WriteAllTextAsync(
            $"{outputFolder}/{folderName}/licence-sets.jsonp",
            $"var licenceSets = {licenceSetsJson}");
    }

    public Task SaveLicenceAsync(Licence licence, string pdfFilePath)
    {
        var folderName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        Directory.CreateDirectory($"{outputFolder}/{folderName}");
        
        var licenceJson = JsonHelper.GetAsString(licence);

        return File.WriteAllTextAsync(
            $"{outputFolder}/{folderName}/licence.jsonp",
            $"var data2 = {licenceJson}");
    }
    
    public Task SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath)
    {
        var folderName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        Directory.CreateDirectory($"{outputFolder}/{folderName}");

        var internalJson = JsonHelper.GetAsString(matchesResult);
        
        return File.WriteAllTextAsync(
            $"{outputFolder}/{folderName}/internal.jsonp",
            $"var data = {internalJson}");
    }

    public Task SaveListDataAsync(List<OutputListDataItem> listData)
    {
        var jsListFilePath = $"{outputFolder}list-data.js";

        return File.WriteAllTextAsync(jsListFilePath, "var data = " +
            JsonSerializer.Serialize(listData, JsonHelper.GetSerializerOptions()) + ";");
    }

    public string GetImageFilepath()
    {
        return $"{outputFolder}/{new PdfPage().GetImageFilepath(new PdfPigNoOcrDataExtractorService().Name)}";
    }

    public (string imgFolder, string imgOutputFilename) GetPageScreenshotPath(
        int pageNumber,
        string pdfServiceName)
    {
        var imgFolder = outputFolder.Replace("//", "/");
        var imgOutputPath = $"/{pdfServiceName}/Images/";

        Directory.CreateDirectory($"{imgFolder}{imgOutputPath}"); // This checks if exists, and creates the whole path too
        
        return (imgFolder, $"{imgOutputPath}page-{pageNumber}.jpg");
    }
    
    public async Task SavePageScreenshotAsync(PdfDocument pdfDocument, int pageNumber, string pdfServiceName)
    {
        var (imgFolder, imgOutputFilename) = GetPageScreenshotPath(pageNumber, pdfServiceName);

        using var memoryStream = pdfDocument.GetPageAsSkBitmap(pageNumber, RGBColor.White);
        await SaveAsJpegAsync(memoryStream, $"{imgFolder}{imgOutputFilename}");
    }
    
    private static async Task SaveAsJpegAsync(SKBitmap bitmap, string filePath, int quality = 60)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        await using var stream = new FileStream(
            filePath,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.ReadWrite);
        
        data.SaveTo(stream);
        
        await stream.FlushAsync();
        stream.Close();
    }
}