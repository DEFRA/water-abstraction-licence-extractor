using System.Text.Json;
using SkiaSharp;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.ProcessFile.Services.Output;

public class DatabaseOutputService(
    IDatabaseReadService databaseReadService,
    IDatabaseWriteService databaseWriteService,
    IImageService imageService) : IOutputService
{
    public string? OutputFolder { get; set; } = null;

    public Task SetupAsync()
    {
        // Nothing to do in this case
        return Task.CompletedTask;
    }

    public List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(
        int pageNumber,
        string pdfServiceName,
        Guid fileId)
    {
        return ImageReferenceHelper.GetPageScreenshotReferences(pageNumber, pdfServiceName, fileId);
    }

    public async Task<byte[]?> GetPageScreenshotThumbnailAsync(int pageNumber, string pdfServiceName, Guid fileId)
    {
        // S3 first (all new writes land there - see SavePageScreenshotThumbnailAsync below),
        // falling back to Postgres for rows written before this table moved to S3 and not yet
        // migrated by the Tools backfill.
        var s3Key = ImageReferenceHelper.GetPageScreenshotThumbnailS3Key(fileId, pdfServiceName, pageNumber);

        return await imageService.DownloadAsync(s3Key)
            ?? await databaseReadService.GetPageScreenshotThumbnailAsync(pageNumber, fileId, pdfServiceName);
    }

    public async Task<List<byte[]>> GetPageScreenshotDataAsync(int pageNumber, string pdfServiceName, Guid fileId)
    {
        var bytes1 = await GetPageScreenshotAsync(pageNumber, fileId, pdfServiceName);
        var bytes2 = await GetPageScreenshotAsync(pageNumber, fileId, GeneralConstants.DocnetExtractorServiceName); // TODO tidy this up

        return
        [
            bytes1!,
            bytes2!
        ];
    }

    private async Task<byte[]?> GetPageScreenshotAsync(int pageNumber, Guid fileId, string noOcrServiceName)
    {
        // Same S3-first, Postgres-fallback shape as GetPageScreenshotThumbnailAsync above.
        var s3Key = ImageReferenceHelper.GetPageScreenshotS3Key(fileId, noOcrServiceName, pageNumber);

        return await imageService.DownloadAsync(s3Key)
            ?? await databaseReadService.GetPageScreenshotAsync(pageNumber, fileId, noOcrServiceName);
    }

    public Task<ProcessRun> StartProcessRunAsync(ProcessRun processRun)
    {
        return databaseWriteService.AddProcessRunAsync(processRun);
    }

    public Task<ProcessRun> MarkProcessRunCompleteIfCompleteAsync(ProcessRun processRun)
    {
        return databaseWriteService.MarkProcessRunCompleteIfCompleteAsync(processRun);
    }

    public Task<ProcessRunFile> AddProcessRunFileAsync(ProcessRunFile processRunFile)
    {
        return databaseWriteService.AddProcessRunFileAsync(processRunFile);
    }

    public Task<ProcessRunFile> MarkProcessRunFileCompleteAsync(ProcessRunFile processRunFile)
    {
        return databaseWriteService.CompleteProcessRunFileAsync(processRunFile);
    }

    public Task<ProcessRunFile> ReportErrorProcessRunFileAsync(ProcessRunFile processRunFile)
    {
        return databaseWriteService.ReportErrorProcessRunFileAsync(processRunFile);
    }
    
    public async Task SaveMatchesAsync(
        List<(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data)> matches)
    {
        var tasks = new List<Task>();

        foreach (var match in matches)
        {
            tasks.Add(SaveMatchAsync(match.matchesResultId, match.labelName, match.labelGroupName, match.data));

            if (tasks.Count == 5)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
            }
        }

        await Task.WhenAll(tasks);
        tasks.Clear();
    }

    public Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data)
    {
        var matchStr = JsonSerializer.Serialize(data, JsonHelper.GetSerializerOptions());
        return databaseWriteService.SaveMatchAsync(matchesResultId, labelName, labelGroupName, matchStr);
    }

    public Task<int> SaveStubMatchesResultAsync(string filename, Guid fileId, int processRunId)
    {
        return databaseWriteService.SaveStubMatchesResultAsync(filename, fileId, processRunId);
    }

    public Task<int> SaveErrorMatchesResultAsync(string filename, Guid fileId, int processRunId, string? error, bool isUpdate)
    {
        return databaseWriteService.SaveErrorMatchesResultAsync(filename, fileId, processRunId, error, isUpdate);
    }

    public Task<int> SaveMatchResultAsync(MatchesResult matchesResult, Guid fileId, int processRunId, bool isUpdate)
    {
        var matchesResultStr = JsonSerializer.Serialize(matchesResult, JsonHelper.GetSerializerOptions());

        return databaseWriteService.SaveMatchesResultAsync(matchesResultStr, fileId, processRunId, isUpdate);
    }
    
    public async Task<List<MatchResultSimple>> GetSimpleMatchResults(int processRunId)
    {
        return await databaseReadService.GetSimpleMatchResults(processRunId);
    }
    
    public async Task<int> SavePageScreenshotAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        Guid fileId,
        int processRunId)
    {
        var images = await pdfDocument.GetPageAsSkBitmapAsync(pageNumber, noOcrServiceName);

        foreach (var (providerName, bitmap) in images)
        {
            var bytes = await GetAsJpegAsync(bitmap);

            await SavePageScreenshotInternalAsync(
                pageNumber,
                noOcrServiceName,
                fileId,
                bytes,
                processRunId);
        }

        return images.Sum(i => i.Bitmap.ByteCount);
    }

    public async Task SavePageScreenshotInternalAsync(
        int pageNumber,
        string noOcrServiceName,
        Guid fileId,
        byte[] data,
        int processRunId)
    {
        var s3Key = ImageReferenceHelper.GetPageScreenshotS3Key(fileId, noOcrServiceName, pageNumber);
        await imageService.UploadAsync(s3Key, new MemoryStream(data), "image/jpeg");
    }

    public async Task SaveAllPagesTextAsync(List<DocumentLine> documentLines, Guid fileId, string noOcrServiceName,
        int processRunId)
    {
        var documentLinesStr = JsonSerializer.Serialize(documentLines, JsonHelper.GetSerializerOptions());
        await databaseWriteService.SaveAllPagesTextAsync(documentLinesStr, fileId, noOcrServiceName, processRunId);
    }
    
    public Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        return databaseReadService.GetProcessRunsAsync();
    }

    public Task<List<ProcessRun>> GetAllProcessRunsAsync()
    {
        return databaseReadService.GetAllProcessRunsAsync();
    }

    public Task<MatchesResult?> GetMatchesResultAsync(int matchesResultId)
    {
        return databaseReadService.GetMatchesResult(matchesResultId);
    }

    public Task<MatchesResult?> GetMatchesResultAsync(Guid fileId)
    {
        return databaseReadService.GetMatchesResult(fileId);
    }

    public Task<MatchesResult?> GetMatchesResultAsync(Guid fileId, int processRunId)
    {
        return databaseReadService.GetMatchesResult(fileId, processRunId);
    }

    public Task SaveThumbnailAsync(Guid fileId)
    {
        throw new NotImplementedException();
    }

    public Task SavePageScreenshotThumbnailAsync(int pageNumber, string serviceName, Guid fileId, byte[] thumbnail,
        int processRunId)
    {
        var s3Key = ImageReferenceHelper.GetPageScreenshotThumbnailS3Key(fileId, serviceName, pageNumber);
        return imageService.UploadAsync(s3Key, new MemoryStream(thumbnail), "image/jpeg");
    }

    public Task UpdateProcessRunByLicenceNumbersAsync(int processRunId, string[] licenceNumbers)
    {
        throw new NotImplementedException();
    }

    public Task UpdateLicenceListProcessRunAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    private static async Task<byte[]> GetAsJpegAsync(SKBitmap bitmap, int quality = 60)
    {
        using var image = SKImage.FromBitmap(bitmap);

        if (image == null)
        {
            throw new FileNotFoundException("Could not load image");
        }

        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

        if (data == null)
        {
            throw new FileNotFoundException("Could not encode image");
        }

        await using var stream = new MemoryStream();
        data.SaveTo(stream);

        await stream.FlushAsync();

        stream.Position = 0;
        var bytes = stream.ToArray();
        stream.Close();

        return bytes;
    }
}