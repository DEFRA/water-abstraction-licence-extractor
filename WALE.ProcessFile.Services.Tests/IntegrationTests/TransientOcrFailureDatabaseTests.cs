using Dapper;
using FakeItEasy;
using Meziantou.Xunit;
using Microsoft.Extensions.Caching.Memory;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Core.Models.OcrService;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Formats;
using WRADI.DocumentType.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Services;
using WRADI.Services.Cache.AbstractionLicence;
using WRADI.Services.Output.AbstractionLicence;

namespace WALE.ProcessFile.Services.Tests.IntegrationTests;

/// <summary>
/// WRADI-392. An OCR provider that is throttled (Textract ProvisionedThroughputExceededException
/// after the SDK's own retries are exhausted) used to leave a result that claimed the provider was
/// used and saved as a normal success, so a throttled run was indistinguishable from a rules
/// regression. These run the throttle-and-recover loop against the real database: the failure is
/// recorded, the row is left reclaimable, and the retry reclaims that same row in place rather than
/// inserting a second one (matches_result has no unique constraint on file and run).
/// </summary>
[EnableParallelization]
public class TransientOcrFailureDatabaseTests
{
    private const string ScannedPdfFilename = "14460030853 licence effective 24.07.2005.PDF";
    private const int RegionCode = 1;

    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider =
        new(TestConfig.PostgresHost,
            TestConfig.PostgresPort,
            TestConfig.PostgresDbName,
            TestConfig.PostgresUsername,
            TestConfig.PostgresPassword,
            maxPoolSize: 10);

    private static IDatabaseReadService ReadService =>
        new PostgresReadService(NpgsqlDataSourceProvider);

    private static IDatabaseWriteService WriteService =>
        new PostgresWriteService(NpgsqlDataSourceProvider);

    private static readonly ICacheService CacheService = new DatabaseCacheService(
        ReadService,
        WriteService);

    private static IAbstractionLicenceDatabaseReadService AbsLicReadService =>
        new PostgresAbstractionLicenceReadService(NpgsqlDataSourceProvider);

    private static IAbstractionLicenceDatabaseWriteService AbsLicWriteService =>
        new PostgresAbstractionLicenceWriteService(NpgsqlDataSourceProvider);

    private static readonly IAbstractionLicenceCacheService AbsLicCacheService =
        new DatabaseAbstractionLicenceCacheService(
            AbsLicReadService,
            AbsLicWriteService);

    private static readonly IAbstractionLicenceOutputService AbsLicOutputService =
        new DatabaseAbstractionLicenceOutputService(
            null!,
            AbsLicReadService,
            AbsLicWriteService,
            null!);

    private static readonly IOutputService OutputService = new DatabaseOutputService(ReadService, WriteService);
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();

    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();

    private static readonly IMessageQueueService MessageQueueService = A.Fake<IMessageQueueService>();

    public TransientOcrFailureDatabaseTests()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    static TransientOcrFailureDatabaseTests()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        _ = new NaldDataLookupService(AbsLicCacheService, AbsLicOutputService, memoryCache);
    }

    /// <summary>
    /// A provider that fails the way a throttled Textract does, without calling anything paid.
    /// </summary>
    private static IOcrDataExtractorService ThrottledProvider(OcrServiceFailurePolicy policy)
    {
        var provider = A.Fake<IOcrDataExtractorService>();

        A.CallTo(() => provider.Name).Returns("FakeThrottledOcr");
        A.CallTo(() => provider.HasDirectCost).Returns(true);
        A.CallTo(() => provider.FailurePolicy).Returns(policy);

        A.CallTo(() => provider.GetTextLinesFromImageAsync(
                A<string>._, A<int>._, A<int>._, A<PdfDocument>._, A<int>._, A<string>._))
            .ThrowsAsync(_ => new OcrServiceTransientException(
                "FakeThrottledOcr",
                "FakeThrottledOcr transient failure",
                new Exception("The number of requests exceeded your throughput limit.")));

        return provider;
    }

    /// <summary>
    /// A provider that succeeds but finds nothing, standing in for the retry once the throttle has
    /// passed. Finding no lines is a normal outcome and does not record a failure.
    /// </summary>
    private static IOcrDataExtractorService RecoveredProvider()
    {
        var provider = A.Fake<IOcrDataExtractorService>();

        A.CallTo(() => provider.Name).Returns("FakeThrottledOcr");
        A.CallTo(() => provider.HasDirectCost).Returns(true);
        A.CallTo(() => provider.FailurePolicy).Returns(OcrServiceFailurePolicy.FailFileOnTransientError);

        A.CallTo(() => provider.GetTextLinesFromImageAsync(
                A<string>._, A<int>._, A<int>._, A<PdfDocument>._, A<int>._, A<string>._))
            .Returns(Task.FromResult<IReadOnlyList<DocumentLine>>([]));

        return provider;
    }

    private static IPdfDataExtractorService ExtractorWith(IOcrDataExtractorService ocrService)
    {
        return new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService> { ocrService },
            CacheService,
            OutputService,
            DocumentService,
            DocnetAlternativeDocumentService,
            MessageQueueService);
    }

    private static async Task<ILicenceNumberService> GetLicenceNumbersAsync(short regionCode)
    {
        var allNaldData = await AbsLicCacheService.GetNaldDataAsync(regionCode, false, 0, int.MaxValue);
        return new AbstractionLicenceNumber(allNaldData.AbstractionAndImpoundmentLicences!, []);
    }

    private static async Task<LookupConfiguration> LookupConfigurationAsync()
    {
        return new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            await CompanyNameHelper.GetFirstNamesCsvFromFileAsync(),
            new LocalFileService(TestConfig.PdfFolder),
            CacheService,
            OutputService,
            await GetLicenceNumbersAsync(RegionCode),
            null,
            null,
            new DmsLookupService(),
            RegionCode,
            DateTime.Now,
            useLockExclusivity: true);
    }

    private static async Task<ProcessRun> StartRunAsync()
    {
        return await OutputService.StartProcessRunAsync(new ProcessRun
        {
            Description = $"{nameof(TransientOcrFailureDatabaseTests)} test run",
            StartDateTimeUtc = DateTime.UtcNow,
            NumberOfFiles = 1
        });
    }

    private static async Task<int> CountMatchesResultRowsAsync(Guid fileId, int processRunId)
    {
        await using var connection = await NpgsqlDataSourceProvider.DataSource.OpenConnectionAsync();

        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM matches_result WHERE file_id = @FileId AND process_run_id = @ProcessRunId",
            new { FileId = fileId, ProcessRunId = processRunId });
    }

    private static async Task<DateTime?> GetProcessRunFileEndAsync(int processRunFileId)
    {
        await using var connection = await NpgsqlDataSourceProvider.DataSource.OpenConnectionAsync();

        return await connection.ExecuteScalarAsync<DateTime?>(
            "SELECT end_date_time_utc FROM process_run_file WHERE process_run__file_id = @ProcessRunFileId",
            new { ProcessRunFileId = processRunFileId });
    }

    [Fact]
    public async Task WhenProviderIsThrottled_ThenFileFailsAndTheRetryReclaimsTheSameRow()
    {
        // Arrange
        var processRun = await StartRunAsync();
        var fileId = Guid.NewGuid();
        var dmsFileData = new DmsFileData { FileId = fileId };

        await CacheService.ClearCacheAsync(fileId);

        // Act - first attempt, provider throttled
        var throttledExtractor = ExtractorWith(
            ThrottledProvider(OcrServiceFailurePolicy.FailFileOnTransientError));

        var throttledConfiguration = await LookupConfigurationAsync();

        var thrown = await Assert.ThrowsAsync<OcrServiceTransientException>(() =>
            throttledExtractor.GetMatchesAsync(
                ScannedPdfFilename,
                dmsFileData,
                throttledConfiguration,
                [ScannedPdfFilename],
                processRun.ProcessRunId));

        // Assert - the file was not quietly extracted without the provider
        Assert.Equal("FakeThrottledOcr", thrown.ServiceName);

        var afterFailure = await OutputService.GetMatchesResultAsync(fileId, processRun.ProcessRunId);
        Assert.NotNull(afterFailure);
        Assert.Equal(nameof(ScrapeStatus.TransientError), afterFailure.Status);

        var rowIdAfterFailure = afterFailure.MatchesResultId;
        Assert.NotEqual(0, rowIdAfterFailure);

        // Act - the queue redelivers the message and the throttle has passed
        var recoveredExtractor = ExtractorWith(RecoveredProvider());

        var (stopExecution, alreadySaved, matchesResult) = await recoveredExtractor.GetMatchesAsync(
            ScannedPdfFilename,
            dmsFileData,
            await LookupConfigurationAsync(),
            [ScannedPdfFilename],
            processRun.ProcessRunId);

        // Assert - it reprocessed rather than treating the failed row as a finished result
        Assert.False(stopExecution);
        Assert.False(alreadySaved);
        Assert.NotNull(matchesResult);

        await recoveredExtractor.SaveMatchResultAsync(
            matchesResult,
            fileId,
            processRun.ProcessRunId,
            isUpdate: true);

        var afterRetry = await OutputService.GetMatchesResultAsync(fileId, processRun.ProcessRunId);
        Assert.NotNull(afterRetry);
        Assert.Equal(nameof(ScrapeStatus.Ok), afterRetry.Status);

        // The same row, reclaimed in place - a second row here would mean one file with two
        // different extraction results in the same run.
        Assert.Equal(rowIdAfterFailure, afterRetry.MatchesResultId);
        Assert.Equal(1, await CountMatchesResultRowsAsync(fileId, processRun.ProcessRunId));
    }

    [Fact]
    public async Task WhenProviderIsThrottledButOnlySupplementary_ThenFileSucceedsWithTheFailureRecorded()
    {
        // Arrange
        var processRun = await StartRunAsync();
        var fileId = Guid.NewGuid();

        await CacheService.ClearCacheAsync(fileId);

        var extractor = ExtractorWith(ThrottledProvider(OcrServiceFailurePolicy.DegradeOnError));

        // Act
        var (_, _, matchesResult) = await extractor.GetMatchesAsync(
            ScannedPdfFilename,
            new DmsFileData { FileId = fileId },
            await LookupConfigurationAsync(),
            [ScannedPdfFilename],
            processRun.ProcessRunId);

        // Assert
        Assert.NotNull(matchesResult);

        // Not claimed as used, which is the bug this ticket started from
        Assert.DoesNotContain("FakeThrottledOcr", matchesResult.ServicesUsed);

        var failures = matchesResult.ServicesFailed
            .Where(failure => failure.ServiceName == "FakeThrottledOcr")
            .ToList();

        Assert.NotEmpty(failures);
        Assert.All(failures, failure => Assert.True(failure.IsTransient));
        Assert.All(failures, failure => Assert.NotEqual(0, failure.PageNumber));

        // And it survives the trip through matches_result.data
        await extractor.SaveMatchResultAsync(matchesResult, fileId, processRun.ProcessRunId, isUpdate: true);

        var persisted = await OutputService.GetMatchesResultAsync(fileId, processRun.ProcessRunId);
        Assert.NotNull(persisted);
        Assert.Equal(nameof(ScrapeStatus.Ok), persisted.Status);
        Assert.NotEmpty(persisted.ServicesFailed);
        Assert.DoesNotContain("FakeThrottledOcr", persisted.ServicesUsed);
    }

    [Fact]
    public async Task WhenErrorIsNotTransient_ThenStatusStaysTerminalForTheRun()
    {
        // Arrange
        var processRun = await StartRunAsync();
        var fileId = Guid.NewGuid();

        await OutputService.SaveStubMatchesResultAsync(ScannedPdfFilename, fileId, processRun.ProcessRunId);

        // Act
        await OutputService.SaveErrorMatchesResultAsync(
            ScannedPdfFilename,
            fileId,
            processRun.ProcessRunId,
            "something permanent",
            isUpdate: true);

        // Assert - the default is unchanged, so a genuine failure is not retried forever
        var result = await OutputService.GetMatchesResultAsync(fileId, processRun.ProcessRunId);
        Assert.NotNull(result);
        Assert.Equal(nameof(ScrapeStatus.Error), result.Status);
    }

    [Fact]
    public async Task WhenTheSameFileIsDeliveredTwice_ThenProcessRunFileKeepsItsIdAndCanComplete()
    {
        // Arrange
        var processRun = await StartRunAsync();
        var fileName = $"{Guid.NewGuid()}.pdf";

        var first = await OutputService.AddProcessRunFileAsync(new ProcessRunFile
        {
            ProcessRunId = processRun.ProcessRunId,
            FileName = fileName
        });

        Assert.NotEqual(0, first.ProcessRunFileId);

        // Act - the redelivery registers the same file again
        var second = await OutputService.AddProcessRunFileAsync(new ProcessRunFile
        {
            ProcessRunId = processRun.ProcessRunId,
            FileName = fileName
        });

        // Assert - previously ON CONFLICT DO NOTHING returned no row, so this was 0 and the
        // mark-complete update below matched nothing, leaving the run unable to finish.
        Assert.Equal(first.ProcessRunFileId, second.ProcessRunFileId);

        await OutputService.MarkProcessRunFileCompleteAsync(second);

        Assert.NotNull(await GetProcessRunFileEndAsync(second.ProcessRunFileId));
    }
}
