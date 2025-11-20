using System.Text.Json;
using SkiaSharp;
using UglyToad.PdfPig.Graphics.Colors;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Services;

public class FileSystemOutputService(string outputFolder) : IOutputService
{
    public Task SetupAsync()
    {
        Directory.CreateDirectory(outputFolder);
        return Task.CompletedTask;
    }

    public Task<string> GetPageScreenshotReferenceAsync(
        int pageNumber,
        string pdfServiceName,
        string pdfFilePath)
    {
        return Task.FromResult(GetPageScreenshotPath(pageNumber, pdfServiceName, pdfFilePath));
    }
    
    private string GetPageScreenshotPath(
        int pageNumber,
        string pdfServiceName,
        string pdfFilePath)
    {
        var folderName = (outputFolder + "/" + FileHelper.GetFilenameWithoutExtension(pdfFilePath)).Replace("//", "/");
        var imgOutputPath = $"{folderName}/{pdfServiceName}/Images/";

        Directory.CreateDirectory(imgOutputPath); // This checks if exists, and creates the whole path too
        
        return $"{imgOutputPath}page-{pageNumber}.jpg";
    }
    
    public Task<ProcessRun> SaveProcessRunAsync(ProcessRun processRun)
    {
        // This mode doesn't need to do anything here
        return Task.FromResult(processRun);
    }

    public Task SaveLicenceSetsAsync(
        Dictionary<string, LicenceSet> licenceSets,
        string pdfFilePath,
        int processRunId)
    {
        var licenceSetsJson = JsonHelper.GetAsString(licenceSets);
        var folderName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        
        return File.WriteAllTextAsync(
            $"{outputFolder}/{folderName}/licence-sets.jsonp",
            $"var licenceSets = {licenceSetsJson}");
    }

    public async Task<int> SaveLicenceAsync(Licence licence, string? pdfFilePath, int processRunId)
    {
        var folderName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        Directory.CreateDirectory($"{outputFolder}/{folderName}");
        
        var licenceJson = JsonHelper.GetAsString(licence);

        await File.WriteAllTextAsync(
            $"{outputFolder}/{folderName}/licence.jsonp",
            $"var data2 = {licenceJson}");

        return -1;
    }

    public Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data)
    {
        throw new NotImplementedException();
    }

    public async Task<int> SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath, int processRunId)
    {
        var folderName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        Directory.CreateDirectory($"{outputFolder}/{folderName}");

        var internalJson = JsonHelper.GetAsString(matchesResult);
        
        await File.WriteAllTextAsync(
            $"{outputFolder}/{folderName}/internal.jsonp",
            $"var data = {internalJson}");

        return -1;
    }

    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId)
    {
        var jsListFilePath = $"{outputFolder}list-data.js";

        return File.WriteAllTextAsync(jsListFilePath, "var data = " +
            JsonSerializer.Serialize(listData, JsonHelper.GetSerializerOptions()) + ";");
    }

    public async Task SavePageScreenshotIfDoesntExistAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        string pdfFilePath,
        int processRunId)
    {
        var imgOutputFilename = GetPageScreenshotPath(pageNumber, noOcrServiceName, pdfFilePath);
        
        if (File.Exists(imgOutputFilename))
        {
            return;
        }
        
        using var memoryStream = pdfDocument.GetPageAsSkBitmap(pageNumber, RGBColor.White);
        await SaveAsJpegAsync(memoryStream, imgOutputFilename);
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

    public async Task SaveAllPagesTextIfDoesntExistAsync(List<DocumentLine> documentLines, string pdfFilePath, string noOcrServiceName, int processRunId)
    {
        var folderName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        Directory.CreateDirectory($"{outputFolder}/{folderName}");
        
        var folder = $"{outputFolder}/{folderName}/Text";
        Directory.CreateDirectory(folder);
        
        var pageAllPath = $"{folder}/pages-all.txt";
        
        if (!File.Exists(pageAllPath))
        {
            await File.WriteAllTextAsync(
                pageAllPath,
                string.Join("\r\n", documentLines
                    .Select(line => $"{line.LineNumber} {line.Text}")
                    .ToArray()));
        }
        
        var pageAllJsPath = $"{folder}/pages-all.js";

        if (!File.Exists(pageAllJsPath))
        {
            var body = string.Join("\r\n", documentLines
                .Select(line => $"{line.LineNumber} {line.Text}")
                .ToArray());
            
            await File.WriteAllTextAsync(
                pageAllJsPath,
                "var textData = `" + body + "`;");
        }
    }
    
    public Task<byte[]?> GetPageScreenshotDataAsync(int pageNumber, string pdfServiceName, string pdfFilePath)
    {
        throw new NotImplementedException();
    }

    public Task FinishProcessRunAsync(ProcessRun processRun)
    {
        return Task.CompletedTask;
    }

    public Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<string, LicenceSet>> GetLicenceSetsAsync(int processRunId, List<Licence> licences)
    {
        throw new NotImplementedException();
    }

    public Task<List<LicenceSet>> GetLicenceSetsAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<List<LicenceSet>> GetLicenceSetsAsync(string filename)
    {
        throw new NotImplementedException();
    }

    public Task<Licence?> GetLicenceAsync(string filename)
    {
        throw new NotImplementedException();
    }

    public Task<MatchesResult?> GetMatchesResult(string filename)
    {
        throw new NotImplementedException();
    }
}