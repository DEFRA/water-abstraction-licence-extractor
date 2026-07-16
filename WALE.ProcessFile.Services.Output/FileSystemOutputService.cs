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
    
    public Task<Dictionary<string, LicenceSet>> GetProcessRunLicenceSetsAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    public List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(
        int pageNumber,
        string pdfServiceName,
        Guid fileId)
    {
        return GetPageScreenshotPaths(pageNumber, pdfServiceName, fileId);
    }

    public Task<byte[]?> GetPageScreenshotThumbnailAsync(int pageNumber, string pdfServiceName, Guid fileId)
    {
        throw new NotImplementedException();
    }

    private List<(string ProviderName, string? ImageReference)> GetPageScreenshotPaths(
        int pageNumber,
        string pdfServiceName,
        Guid fileId)
    {
        var folderName = outputFolder + "/" + fileId;
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

    public Task<ProcessRun> MarkProcessRunCompleteIfCompleteAsync(ProcessRun processRun)
    {
        throw new NotImplementedException();
    }

    public Task<ProcessRunFile> AddProcessRunFileAsync(ProcessRunFile processRunFile)
    {
        throw new NotImplementedException();
    }

    public Task<ProcessRunFile> MarkProcessRunFileCompleteAsync(ProcessRunFile processRunFile)
    {
        throw new NotImplementedException();
    }

    public Task<ProcessRunFile> ReportErrorProcessRunFileAsync(ProcessRunFile processRunFile)
    {
        throw new NotImplementedException();
    }
    
    public Task SaveLicenceSetsAsync(
        Dictionary<string, LicenceSet> licenceSets,
        Guid? fileId,
        int processRunId)
    {
        if (fileId == null)
        {
            return Task.CompletedTask;
        }
        
        var licenceSetsJson = JsonHelper.GetAsString(licenceSets);
        
        return File.WriteAllTextAsync(
            $"{outputFolder}/{fileId}/licence-sets.jsonp",
            $"var licenceSets = {licenceSetsJson}");
    }

    public Task SaveLicenceSetAsync(LicenceSet licenceSet, Guid? fileId, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<int> SaveLicenceAsync(Licence licence, int processRunId)
    {
        Directory.CreateDirectory($"{outputFolder}/{licence.DmsFileId}");
        
        var licenceJson = JsonHelper.GetAsString(licence);

        await File.WriteAllTextAsync(
            $"{outputFolder}/{licence.DmsFileId}/licence.jsonp",
            $"var data2 = {licenceJson}");

        return -1;
    }

    public Task UpdateLicenceAsync(Licence licence, int licenceId, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task SaveMatchesAsync(List<(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data)> matches)
    {
        throw new NotImplementedException();
    }

    public Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data)
    {
        throw new NotImplementedException();
    }

    public async Task<int> SaveMatchResultAsync(MatchesResult matchesResult, Guid fileId, int processRunId)
    {
        Directory.CreateDirectory($"{outputFolder}/{fileId}");

        var internalJson = JsonHelper.GetAsString(matchesResult);
        
        await File.WriteAllTextAsync(
            $"{outputFolder}/{fileId}/internal.jsonp",
            $"var data = {internalJson}");

        return -1;
    }

    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId)
    {
        var jsListFilePath = $"{outputFolder}list-data.js";

        return File.WriteAllTextAsync(jsListFilePath, "var data = " +
            JsonSerializer.Serialize(listData, JsonHelper.GetSerializerOptions()) + ";");
    }

    public async Task<int> SavePageScreenshotAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        Guid fileId,
        int processRunId)
    {
        var imagePaths = GetPageScreenshotPaths(
            pageNumber,
            noOcrServiceName,
            fileId);
        
        var exists1 = File.Exists(imagePaths[0].ImageReference);
        var exists2 = imagePaths.Count >= 2 && File.Exists(imagePaths[1].ImageReference);

        if (exists1 && exists2)
        {
            return -1;
        }
        
        var images = await pdfDocument.GetPageAsSkBitmapAsync(pageNumber, noOcrServiceName);
        var size = 0;
        
        foreach (var (provider, bitmap) in images)
        {
            var imgOutputFilename = imagePaths
                .First(x => x.ProviderName == provider)
                .ImageReference!;
            
            size += await SaveAsJpegAsync(bitmap, imgOutputFilename);
        }

        return size;
    }

    public Task SavePageScreenshotInternalAsync(
        int pageNumber,
        string noOcrServiceName,
        Guid fileId,
        byte[] data,
        int processRunId)
    {
        throw new NotImplementedException();
    }

    private static async Task<int> SaveAsJpegAsync(SKBitmap bitmap, string filePath, int quality = 80)
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
        
        var streamLength = stream.Length;
        stream.Close();

        return (int)streamLength;
    }

    public async Task SaveAllPagesTextAsync(List<DocumentLine> documentLines, Guid fileId, string noOcrServiceName, int processRunId)
    {
        Directory.CreateDirectory($"{outputFolder}/{fileId}");
        
        var folder = $"{outputFolder}/{fileId}/Text";
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
    
    public async Task<List<byte[]>> GetPageScreenshotDataAsync(int pageNumber, string pdfServiceName, Guid fileId)
    {
        var pageScreenshotPaths = GetPageScreenshotPaths(
            pageNumber,
            pdfServiceName,
            fileId);

        var returnList = new List<byte[]>();
        
        foreach (var pageScreenshotPath in pageScreenshotPaths)
        {
            returnList.Add(await File.ReadAllBytesAsync(pageScreenshotPath.ImageReference!));
        }

        return returnList;
    }

    public Task FinishProcessRunAsync(ProcessRun processRun)
    {
        return Task.CompletedTask;
    }

    public Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<ProcessRun>> GetAllProcessRunsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<Licence>> GetLicencesAsync(int processRunId, int skip, int take)
    {
        throw new NotImplementedException();
    }

    public Task<List<Licence>> GetLicencesSearchAsync(int processRunId, ProcessRunQuery processRunQuery)
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

    public Task<List<LicenceSet>> GetLicenceSetsAsync(Guid fileId)
    {
        throw new NotImplementedException();
    }

    public Task<Licence?> GetLicenceAsync(Guid fileId, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<MatchesResult?> GetMatchesResult(Guid fileId)
    {
        throw new NotImplementedException();
    }

    public Task<LinkedLicence[]?> GetLinkedLicencesAsync(string permitNumber)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<LicenceSectionVerification>> GetLicenceSectionVerificationsAsync(Guid licenceFileId)
    {
        return Task.FromResult<IEnumerable<LicenceSectionVerification>>([]);
    }

    public Task<IEnumerable<LicenceSectionVerification>> GetLatestLicenceSectionVerificationsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<int> SaveLicenceSectionVerificationAsync(LicenceSectionVerification verification)
    {
        return Task.FromResult(0);
    }

    public Task SavePageScreenshotThumbnailAsync(int pageNumber, string serviceName, Guid fileId, byte[] thumbnail, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<int> GetTotalLicenceCountAsync(int processRunId, ProcessRunQuery processRunQuery)
    {
        throw new NotImplementedException();
    }
}