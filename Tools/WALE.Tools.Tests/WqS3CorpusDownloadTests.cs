using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using WALE.ProcessFile.Services.AwsS3;
using Xunit.Abstractions;

namespace WALE.Tools.Tests;

/// <summary>
/// Pulls the WQ permit corpus out of S3 into the local test licence folder, so the WQ work can run
/// against the whole corpus rather than the ad hoc sample that happens to be in ~/Downloads.
///
/// Both tests do nothing and report why unless this project's user secrets carry the S3 settings,
/// so an ordinary test run of the solution is unaffected:
/// <code>
/// dotnet user-secrets --project Tools/WALE.Tools.Tests set "AwsS3BucketName" "&lt;bucket&gt;"
/// dotnet user-secrets --project Tools/WALE.Tools.Tests set "AwsRegionName" "eu-west-2"
/// dotnet user-secrets --project Tools/WALE.Tools.Tests set "AwsAccessKey" "&lt;key&gt;"
/// dotnet user-secrets --project Tools/WALE.Tools.Tests set "AwsSecretKey" "&lt;secret&gt;"
/// </code>
/// Add <c>AwsSessionToken</c> as well when the credentials are a temporary session. The key names
/// match WALE.Api's own appsettings, so they can be copied from an existing secrets file.
///
/// Run <see cref="WhenS3IsConfigured_ThenWqFilesAreListed"/> first: it proves the credentials and
/// bucket are right without moving any data.
///
/// The download is resumable. A file already on disk is left alone, so a re-run after an
/// interruption fetches only what is missing, and a second run costs one listing pass. Each file
/// is written under a .part name and moved into place once complete, so an interrupted run cannot
/// leave a truncated PDF that a later run would mistake for a finished download.
/// </summary>
public class WqS3CorpusDownloadTests(ITestOutputHelper testOutputHelper)
{
    /// <summary>
    /// Where the corpus lands. Its own folder under the test licence directory, so the ~10,000 WQ
    /// permits do not swamp the abstraction licence PDFs beside them - that parent folder is the
    /// local runner's configured PDF path, and anything globbing it would otherwise pick up both.
    /// </summary>
    private static string DestinationFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Documents",
        "TestLicences",
        "WQ");

    /// <summary>How many downloads run at once. S3 is happy with far more; this is polite.</summary>
    private const int MaxConcurrentDownloads = 8;

    private const int ProgressEvery = 250;

    private static IConfigurationRoot Configuration => new ConfigurationBuilder()
        .AddUserSecrets<WqS3CorpusDownloadTests>()
        .Build();

    /// <summary>
    /// The WQ documents are the ones whose own filename starts with "wq" - the ingestion convention
    /// is <c>wq__{permit}__{fileId}.pdf</c>. Matched on the filename rather than the whole key so a
    /// bucket that organises objects into folders still works.
    /// </summary>
    private static bool IsWqPdf(string key)
    {
        var fileName = key.Split('/').Last();

        return fileName.StartsWith("wq", StringComparison.OrdinalIgnoreCase)
            && fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the S3 service from user secrets, or returns null and the names of whatever is
    /// missing, so the tests can report a configuration gap instead of failing on a null reference.
    /// </summary>
    private static (AwsS3FileService? Service, string? Missing) TryGetFileService()
    {
        var configuration = Configuration;

        var regionName = configuration["AwsRegionName"];
        var bucketName = configuration["AwsS3BucketName"];
        var accessKey = configuration["AwsAccessKey"];
        var secretKey = configuration["AwsSecretKey"];
        var sessionToken = configuration["AwsSessionToken"];

        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(regionName))
        {
            missing.Add("AwsRegionName");
        }

        if (string.IsNullOrWhiteSpace(bucketName))
        {
            missing.Add("AwsS3BucketName");
        }

        if (string.IsNullOrWhiteSpace(accessKey))
        {
            missing.Add("AwsAccessKey");
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            missing.Add("AwsSecretKey");
        }

        if (missing.Count > 0)
        {
            return (null, string.Join(", ", missing));
        }

        var service = new AwsS3FileService(
            regionName!,
            bucketName!,
            accessKey,
            secretKey,
            sessionToken);

        return (service, null);
    }

    [Fact]
    public async Task WhenS3IsConfigured_ThenWqFilesAreListed()
    {
        // Arrange
        var (fileService, missing) = TryGetFileService();

        if (fileService == null)
        {
            testOutputHelper.WriteLine(
                $"Skipped - set these in this project's user secrets first: {missing}");

            return;
        }

        // Act
        var allKeys = await fileService.GetAllFilesAsync();
        var wqKeys = allKeys.Where(IsWqPdf).ToList();

        // Assert
        testOutputHelper.WriteLine($"Objects in the bucket: {allKeys.Count}");
        testOutputHelper.WriteLine($"WQ PDFs among them: {wqKeys.Count}");

        var alreadyLocal = Directory.Exists(DestinationFolder)
            ? wqKeys.Count(key => File.Exists(Path.Combine(DestinationFolder, key.Split('/').Last())))
            : 0;

        testOutputHelper.WriteLine($"Already in {DestinationFolder}: {alreadyLocal}");
        testOutputHelper.WriteLine($"Still to download: {wqKeys.Count - alreadyLocal}");

        foreach (var key in wqKeys.Take(5))
        {
            testOutputHelper.WriteLine($"  e.g. {key}");
        }

        Assert.NotEmpty(wqKeys);
    }

    [Fact]
    public async Task WhenS3IsConfigured_ThenAllWqFilesAreDownloaded()
    {
        // Arrange
        var (fileService, missing) = TryGetFileService();

        if (fileService == null)
        {
            testOutputHelper.WriteLine(
                $"Skipped - set these in this project's user secrets first: {missing}");

            return;
        }

        Directory.CreateDirectory(DestinationFolder);

        var allKeys = await fileService.GetAllFilesAsync();
        var wqKeys = allKeys.Where(IsWqPdf).OrderBy(key => key).ToList();

        testOutputHelper.WriteLine(
            $"{wqKeys.Count} WQ PDFs in the bucket, downloading into {DestinationFolder}");

        var downloaded = 0;
        var skipped = 0;
        var completed = 0;
        var failures = new ConcurrentBag<string>();

        using var concurrencyLimit = new SemaphoreSlim(MaxConcurrentDownloads);

        // Act
        var downloads = wqKeys.Select(async key =>
        {
            await concurrencyLimit.WaitAsync();

            try
            {
                var fileName = key.Split('/').Last();
                var destinationPath = Path.Combine(DestinationFolder, fileName);

                // Resumable: anything already on disk with content is left alone.
                if (File.Exists(destinationPath) && new FileInfo(destinationPath).Length > 0)
                {
                    Interlocked.Increment(ref skipped);
                    return;
                }

                var partialPath = destinationPath + ".part";

                try
                {
                    await using var source = await fileService.GetFileAsStreamAsync(key);

                    if (source == null)
                    {
                        failures.Add($"{key} - no content returned");
                        return;
                    }

                    await using (var destination = File.Create(partialPath))
                    {
                        await source.CopyToAsync(destination);
                    }

                    // Only now does it become a file a later run will skip.
                    File.Move(partialPath, destinationPath, overwrite: true);
                    Interlocked.Increment(ref downloaded);
                }
                catch (Exception exception)
                {
                    failures.Add($"{key} - {exception.GetType().Name}: {exception.Message}");

                    if (File.Exists(partialPath))
                    {
                        File.Delete(partialPath);
                    }
                }
            }
            finally
            {
                concurrencyLimit.Release();

                var done = Interlocked.Increment(ref completed);

                if (done % ProgressEvery == 0)
                {
                    testOutputHelper.WriteLine($"  {done} of {wqKeys.Count} processed");
                }
            }
        });

        await Task.WhenAll(downloads);

        // Assert
        testOutputHelper.WriteLine(
            $"Downloaded {downloaded}, already present {skipped}, failed {failures.Count}");

        // Per-file failures go to a file as well: xUnit's console logger does not reliably surface
        // a long run's test output, and a re-run would otherwise lose which keys to chase.
        if (!failures.IsEmpty)
        {
            var failureLog = Path.Combine(AppContext.BaseDirectory, "wq-s3-download-failures.txt");
            await File.WriteAllLinesAsync(failureLog, failures.OrderBy(line => line));

            testOutputHelper.WriteLine($"Failures written to {failureLog}");

            foreach (var failure in failures.Take(10))
            {
                testOutputHelper.WriteLine($"  {failure}");
            }
        }

        Assert.Empty(failures);
        Assert.Equal(wqKeys.Count, downloaded + skipped);
    }
}
