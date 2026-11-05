using Dapper;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.AwsS3;
using WALE.Tools.Config;

namespace WALE.Tools._2ndHalf;

// Migrates pre-existing bytea rows (written before page_screenshot/page_screenshot_thumbnail/
// image_on_page moved to S3) to S3 under the same convention keys the read/write paths already
// compute. Deliberately does not touch the Postgres rows at all - not even to mark them done -
// because nothing here needs a schema change: each row is skipped once its S3 key already
// exists, which makes every method here safe to interrupt and re-run from the start.
//
// Run smallest table first (page_screenshot_thumbnail), then image_on_page, then page_screenshot
// last (86k rows, the one that actually matters for storage cost).
public static class BackfillImagesToS3
{
    static BackfillImagesToS3()
    {
        // Not set here by DI (this class talks to Postgres directly, unlike WALE.Api) - needed
        // for Dapper to map snake_case columns onto the PascalCase row classes below.
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private const int BatchSize = 100;
    private const int MaxConcurrent = 12;

    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider = new(
        KeyConfig.PostgresHost,
        KeyConfig.PostgresPort,
        KeyConfig.PostgresDbName,
        KeyConfig.PostgresUsername,
        KeyConfig.PostgresPassword);

    private static readonly IImageService ImageService = new S3ImageService(
        KeyConfig.AwsS3RegionName,
        KeyConfig.AwsS3AssetsBucketName,
        KeyConfig.AwsS3AccessKey,
        KeyConfig.AwsS3SecretKey,
        KeyConfig.AwsS3SessionToken);

    public static Task BackfillPageScreenshotThumbnailsAsync() =>
        BackfillTableAsync(
            "page_screenshot_thumbnail",
            "page_screenshot_thumbnail_id",
            row => ImageReferenceHelper.GetPageScreenshotThumbnailS3Key(row.FileId, row.NoOcrServiceName, row.PageNumber),
            "image/jpeg");

    public static Task BackfillPageScreenshotsAsync() =>
        BackfillTableAsync(
            "page_screenshot",
            "page_screenshot_id",
            row => ImageReferenceHelper.GetPageScreenshotS3Key(row.FileId, row.NoOcrServiceName, row.PageNumber),
            "image/jpeg");

    public static async Task BackfillImageOnPageAsync()
    {
        ConsoleHelper.WriteLine("Started backfilling image_on_page to S3");

        var lastId = 0;
        var totalUploaded = 0;
        var totalSkipped = 0;

        while (true)
        {
            // Only rows with real bytes are pre-migration - new writes leave an empty
            // placeholder here (see DatabaseCacheService.SaveImageOnPageAsync).
            const string sql = """
                                SELECT image_on_page_id, file_id, no_ocr_service_name, page_number, image_number, extension, data
                                FROM image_on_page
                                WHERE image_on_page_id > @LastId
                                  AND length(data) > 0
                                ORDER BY image_on_page_id
                                LIMIT @BatchSize;
                                """;

            await using var connection = NpgsqlDataSourceProvider.DataSource.CreateConnection();
            var batch = (await connection.QueryAsync<ImageOnPageRow>(sql, new { LastId = lastId, BatchSize }))
                .ToList();

            if (batch.Count == 0)
            {
                break;
            }

            lastId = batch.Max(row => row.ImageOnPageId);

            var results = await RunWithBoundedConcurrencyAsync(batch, row => BackfillImageOnPageRowAsync(row));
            totalUploaded += results.Count(uploaded => uploaded);
            totalSkipped += results.Count(uploaded => !uploaded);

            ConsoleHelper.WriteLine($"image_on_page - uploaded {totalUploaded}, already in S3 {totalSkipped} (up to id {lastId})");
        }

        ConsoleHelper.WriteLine("Finished backfilling image_on_page to S3");
    }

    private static async Task<bool> BackfillImageOnPageRowAsync(ImageOnPageRow row)
    {
        var s3Key = ImageReferenceHelper.GetImageOnPageS3Key(row.FileId, row.NoOcrServiceName, row.PageNumber, row.ImageNumber, row.Extension);

        if (await ImageService.ExistsAsync(s3Key))
        {
            return false;
        }

        var contentType = row.Extension.Equals("png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
        await ImageService.UploadAsync(s3Key, new MemoryStream(row.Data), contentType);
        return true;
    }

    private static async Task BackfillTableAsync(
        string tableName,
        string idColumn,
        Func<ScreenshotRow, string> getS3Key,
        string contentType)
    {
        ConsoleHelper.WriteLine($"Started backfilling {tableName} to S3");

        var lastId = 0;
        var totalUploaded = 0;
        var totalSkipped = 0;

        while (true)
        {
            var sql = $"""
                       SELECT {idColumn} AS Id, file_id, no_ocr_service_name, page_number, data
                       FROM {tableName}
                       WHERE {idColumn} > @LastId
                       ORDER BY {idColumn}
                       LIMIT @BatchSize;
                       """;

            await using var connection = NpgsqlDataSourceProvider.DataSource.CreateConnection();
            var batch = (await connection.QueryAsync<ScreenshotRow>(sql, new { LastId = lastId, BatchSize }))
                .ToList();

            if (batch.Count == 0)
            {
                break;
            }

            lastId = batch.Max(row => row.Id);

            var results = await RunWithBoundedConcurrencyAsync(
                batch,
                row => BackfillScreenshotRowAsync(row, getS3Key, contentType));

            totalUploaded += results.Count(uploaded => uploaded);
            totalSkipped += results.Count(uploaded => !uploaded);

            ConsoleHelper.WriteLine($"{tableName} - uploaded {totalUploaded}, already in S3 {totalSkipped} (up to id {lastId})");
        }

        ConsoleHelper.WriteLine($"Finished backfilling {tableName} to S3");
    }

    private static async Task<bool> BackfillScreenshotRowAsync(
        ScreenshotRow row,
        Func<ScreenshotRow, string> getS3Key,
        string contentType)
    {
        var s3Key = getS3Key(row);

        if (await ImageService.ExistsAsync(s3Key))
        {
            return false;
        }

        await ImageService.UploadAsync(s3Key, new MemoryStream(row.Data), contentType);
        return true;
    }

    private static async Task<List<TResult>> RunWithBoundedConcurrencyAsync<TItem, TResult>(
        List<TItem> items,
        Func<TItem, Task<TResult>> action)
    {
        var results = new List<TResult>();
        var inFlight = new List<Task<TResult>>();

        foreach (var item in items)
        {
            inFlight.Add(action(item));

            if (inFlight.Count < MaxConcurrent)
            {
                continue;
            }

            var completed = await Task.WhenAny(inFlight);
            inFlight.Remove(completed);
            results.Add(await completed);
        }

        results.AddRange(await Task.WhenAll(inFlight));
        return results;
    }

    private class ScreenshotRow
    {
        public int Id { get; init; }
        public Guid FileId { get; init; }
        public string NoOcrServiceName { get; init; } = null!;
        public int PageNumber { get; init; }
        public byte[] Data { get; init; } = null!;
    }

    private class ImageOnPageRow
    {
        public int ImageOnPageId { get; init; }
        public Guid FileId { get; init; }
        public string NoOcrServiceName { get; init; } = null!;
        public int PageNumber { get; init; }
        public int ImageNumber { get; init; }
        public string Extension { get; init; } = null!;
        public byte[] Data { get; init; } = null!;
    }
}
