using System.Text.Json;
using SkiaSharp;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Output;

public class FileSystemOutputService(string outputFolder) : IOutputService
{
    public string? OutputFolder { get; set; } = outputFolder.StartsWith('/') ? outputFolder : Path.GetFullPath(outputFolder);
    
    public Task SetupAsync()
    {
        Directory.CreateDirectory(outputFolder);
        return Task.CompletedTask;
    }

    public List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(
        int pageNumber,
        string pdfServiceName,
        string pdfFilePath)
    {
        return GetPageScreenshotPaths(pageNumber, pdfServiceName, pdfFilePath);
    }

    private List<(string ProviderName, string? ImageReference)> GetPageScreenshotPaths(
        int pageNumber,
        string pdfServiceName,
        string pdfFilePath)
    {
        var folderName = (outputFolder + "/" + FileHelper.GetFilenameWithoutExtension(pdfFilePath)).Replace("//", "/");
        var imgOutputPath = $"{folderName}/{pdfServiceName}/Images/";

        Directory.CreateDirectory(imgOutputPath); // This checks if exists, and creates the whole path too

        var result1 = $"{imgOutputPath}page-{pageNumber}.jpg";
        
        imgOutputPath = $"{folderName}/{GeneralConstants.DocnetExtractorServiceName}/Images/";
        Directory.CreateDirectory(imgOutputPath); // This checks if exists, and creates the whole path too
        var result2 = $"{imgOutputPath}page-{pageNumber}.jpg";
        
        return
        [
            (pdfServiceName, result1),
            (GeneralConstants.DocnetExtractorServiceName, result2),
        ];
    }
    
    public Task<ProcessRun> StartProcessRunAsync(ProcessRun processRun)
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

    public Task UpdateLicenceAsync(Licence licence, int licenceId, string? pdfFilePath, int processRunId)
    {
        throw new NotImplementedException();
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

    public async Task SavePageScreenshotAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        string pdfFilePath,
        int processRunId)
    {
        var imagePaths = GetPageScreenshotPaths(
            pageNumber,
            noOcrServiceName,
            pdfFilePath);
        
        var exists1 = File.Exists(imagePaths[0].ImageReference);
        var exists2 = imagePaths.Count >= 2 && File.Exists(imagePaths[1].ImageReference);

        if (exists1 && exists2)
        {
            return;
        }
        
        var images = pdfDocument.GetPageAsSkBitmap(pageNumber, noOcrServiceName);

        foreach (var (provider, bitmap) in images)
        {
            var imgOutputFilename = imagePaths
                .First(x => x.ProviderName == provider)
                .ImageReference!;
            
            await SaveAsJpegAsync(bitmap, imgOutputFilename);
        }
    }
    
    private static async Task SaveAsJpegAsync(SKBitmap bitmap, string filePath, int quality = 80)
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

    public async Task SaveAllPagesTextAsync(List<DocumentLine> documentLines, string pdfFilePath, string noOcrServiceName, int processRunId)
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
    
    public async Task<List<byte[]>> GetPageScreenshotDataAsync(int pageNumber, string pdfServiceName, string pdfFilePath)
    {
        var pageScreenshotPaths = GetPageScreenshotPaths(
            pageNumber,
            pdfServiceName,
            pdfFilePath);

        var returnList = new List<byte[]>();
        
        foreach (var pageScreenshotPath in pageScreenshotPaths)
        {
            returnList.Add(await File.ReadAllBytesAsync(pageScreenshotPath.ImageReference!));
        }

        return returnList;
    }

    public Task FinishProcessRunAsync(ProcessRun processRun, int regionId)
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