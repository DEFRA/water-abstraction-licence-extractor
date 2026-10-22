using FakeItEasy;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.Cache.WrInspectionReport.Interfaces;
using WRADI.Services.Cache.WrInspectionReport.Models;
using WRADI.Services.ProcessFile.WrInspectionReport;
using WRADI.Services.ProcessFile.WrInspectionReport.Implementations;

namespace WRADI.Services.WrInspectionReport.Tests;

public class FileProcessOrchestrationServiceTests
{
    /// <summary>
    /// A file service whose bucket contains exactly the keys these candidates resolve to. The
    /// orchestrator now selects on what is in ingress, so a fake that lists nothing filters every
    /// candidate out and a test asserting a send would fail for the wrong reason.
    /// </summary>
    private static IFileService FileServiceHolding(params InspectionReportFinderResult[] candidates)
    {
        var fileService = A.Fake<IFileService>();

        var keys = candidates
            .Where(WrInspectionReportCandidateSelector.HasIdentity)
            .Select(candidate => WrInspectionReportIngressFile.BuildFileName(
                candidate.PermitNumber!, candidate.FileId!))
            .ToList();

        A.CallTo(() => fileService.GetAllFilesAsync()).Returns(keys);

        return fileService;
    }

    private static IFileService FileServiceHoldingNothing()
    {
        var fileService = A.Fake<IFileService>();
        A.CallTo(() => fileService.GetAllFilesAsync()).Returns(new List<string>());

        return fileService;
    }

    [Fact]
    public async Task WhenCandidatesExist_ThenEnqueuedRequestsAreStampedWithWrInspectionReportDocumentType()
    {
        // Arrange
        var settings = new FileProcessAppSettings();
        var cacheService = A.Fake<ICacheService>();
        var inspectionReportFinderCacheService = A.Fake<IInspectionReportFinderCacheService>();
        var outputService = A.Fake<IOutputService>();
        var messageQueueService = A.Fake<IMessageQueueService>();

        var candidate = new InspectionReportFinderResult
        {
            PermitNumber = "PERMIT-1",
            FileId = Guid.NewGuid().ToString(),
            FileUrl = "https://example/file.pdf"
        };

        A.CallTo(() => inspectionReportFinderCacheService.GetInspectionReportFinderResultsAsync(0, int.MaxValue))
            .Returns([candidate]);

        A.CallTo(() => outputService.StartProcessRunAsync(A<ProcessRun>._))
            .ReturnsLazily(call => Task.FromResult(call.GetArgument<ProcessRun>(0)!));

        var enqueuedRequests = new List<FileProcessSingleRequest>();

        A.CallTo(() => messageQueueService.AddToFileProcessQueueBatch(A<IReadOnlyList<FileProcessSingleRequest>>._))
            .ReturnsLazily(call =>
            {
                var batch = call.GetArgument<IReadOnlyList<FileProcessSingleRequest>>(0)!;
                enqueuedRequests.AddRange(batch);

                return Task.FromResult(new FileProcessQueueBatchResult
                {
                    Requested = batch.Count,
                    Enqueued = batch.Count
                });
            });

        var service = new FileProcessOrchestrationService(
            settings,
            cacheService,
            inspectionReportFinderCacheService,
            outputService,
            messageQueueService,
            FileServiceHolding(candidate));

        // Act
        var result = await service.RunAsync(CancellationToken.None);

        // Assert
        Assert.True(result);
        var enqueuedRequest = Assert.Single(enqueuedRequests);
        Assert.Equal("WrInspectionReport", enqueuedRequest.DocumentType);
    }

    /// <summary>
    /// A window selects by what DMS recorded, so an unparseable or absent upload date cannot be
    /// compared and the candidate is left out - which must be visible rather than silent.
    /// </summary>
    [Theory]
    [InlineData("2025-06-15 00:00:00", true)]
    [InlineData("2025-01-01 00:00:00", true)]
    [InlineData("2025-12-31 00:00:00", true)]
    [InlineData("2024-12-31 00:00:00", false)]
    [InlineData("2026-01-01 00:00:00", false)]
    [InlineData("not a date", false)]
    [InlineData("", false)]
    public async Task WhenAnUploadWindowIsSet_ThenOnlyCandidatesInsideItAreSent(
        string uploadDate,
        bool expectSent)
    {
        // Arrange
        var settings = new FileProcessAppSettings
        {
            UploadedFrom = new DateOnly(2025, 1, 1),
            UploadedTo = new DateOnly(2025, 12, 31)
        };

        var (enqueuedRequests, startedProcessRun) = await RunWithCandidatesAsync(
            settings,
            [
                new InspectionReportFinderResult
                {
                    PermitNumber = "PERMIT-1",
                    FileId = Guid.NewGuid().ToString(),
                    FileName = "report.pdf",
                    UploadDate = uploadDate
                }
            ]);

        // Assert
        Assert.Equal(expectSent ? 1 : 0, enqueuedRequests.Count);
        Assert.Equal(expectSent ? 1 : 0, startedProcessRun?.NumberOfFiles);
    }

    /// <summary>
    /// The cap draws from the window, not from every discovered document, or a windowed run would be
    /// indistinguishable from an arbitrary MaxFilesPerRun slice.
    /// </summary>
    [Fact]
    public async Task WhenAWindowAndACapAreBothSet_ThenTheCapAppliesToTheWindowedCandidates()
    {
        // Arrange
        var settings = new FileProcessAppSettings
        {
            UploadedFrom = new DateOnly(2025, 1, 1),
            MaxFilesPerRun = 2
        };

        // Permit numbers carry the expectation, so the assertion is on which files were taken
        // rather than how many - two of five is also what an unwindowed cap would send.
        var candidates = new List<InspectionReportFinderResult>();

        foreach (var (permitNumber, uploadDate) in new[]
                 {
                     ("OUTSIDE-1", "2024-01-01 00:00:00"),
                     ("OUTSIDE-2", "2024-02-01 00:00:00"),
                     ("INSIDE-1", "2025-03-01 00:00:00"),
                     ("INSIDE-2", "2025-04-01 00:00:00"),
                     ("INSIDE-3", "2025-05-01 00:00:00")
                 })
        {
            candidates.Add(new InspectionReportFinderResult
            {
                PermitNumber = permitNumber,
                FileId = Guid.NewGuid().ToString(),
                FileName = "report.pdf",
                UploadDate = uploadDate
            });
        }

        var (enqueuedRequests, startedProcessRun) = await RunWithCandidatesAsync(settings, candidates);

        // Assert - three fall inside the window, capped to two, and both come from inside it
        Assert.Equal(2, enqueuedRequests.Count);
        Assert.Equal(2, startedProcessRun?.NumberOfFiles);
        Assert.All(enqueuedRequests, request => Assert.StartsWith("INSIDE-", request.PermitNumber));
    }

    /// <summary>
    /// The run has to record what it covered; nothing else retains the window afterwards.
    /// </summary>
    [Fact]
    public async Task WhenAWindowIsSet_ThenTheProcessRunDescriptionSaysSo()
    {
        var settings = new FileProcessAppSettings
        {
            UploadedFrom = new DateOnly(2025, 1, 1),
            UploadedTo = new DateOnly(2025, 12, 31)
        };

        var (_, startedProcessRun) = await RunWithCandidatesAsync(
            settings,
            [
                new InspectionReportFinderResult
                {
                    PermitNumber = "PERMIT-1",
                    FileId = Guid.NewGuid().ToString(),
                    FileName = "report.pdf",
                    UploadDate = "2025-06-15 00:00:00"
                }
            ]);

        Assert.Contains("from 2025-01-01 to 2025-12-31", startedProcessRun?.Description);
    }

    private static async Task<(List<FileProcessSingleRequest> Enqueued, ProcessRun? Run)>
        RunWithCandidatesAsync(
            FileProcessAppSettings settings,
            List<InspectionReportFinderResult> candidates)
    {
        var inspectionReportFinderCacheService = A.Fake<IInspectionReportFinderCacheService>();
        var outputService = A.Fake<IOutputService>();
        var messageQueueService = A.Fake<IMessageQueueService>();

        A.CallTo(() => inspectionReportFinderCacheService.GetInspectionReportFinderResultsAsync(0, int.MaxValue))
            .Returns(candidates);

        ProcessRun? startedProcessRun = null;

        A.CallTo(() => outputService.StartProcessRunAsync(A<ProcessRun>._))
            .ReturnsLazily(call =>
            {
                startedProcessRun = call.GetArgument<ProcessRun>(0);

                return Task.FromResult(startedProcessRun!);
            });

        var enqueuedRequests = new List<FileProcessSingleRequest>();

        A.CallTo(() => messageQueueService.AddToFileProcessQueueBatch(A<IReadOnlyList<FileProcessSingleRequest>>._))
            .ReturnsLazily(call =>
            {
                var batch = call.GetArgument<IReadOnlyList<FileProcessSingleRequest>>(0)!;
                enqueuedRequests.AddRange(batch);

                return Task.FromResult(new FileProcessQueueBatchResult
                {
                    Requested = batch.Count,
                    Enqueued = batch.Count
                });
            });

        var service = new FileProcessOrchestrationService(
            settings,
            A.Fake<ICacheService>(),
            inspectionReportFinderCacheService,
            outputService,
            messageQueueService,
            FileServiceHolding(candidates.ToArray()));

        await service.RunAsync(CancellationToken.None);

        return (enqueuedRequests, startedProcessRun);
    }

    /// <summary>
    /// A skipped candidate must be absent from the run's NumberOfFiles as well as from the queue,
    /// since the count is written before anything is sent.
    /// </summary>
    [Theory]
    // The DMS file name does not decide this: a .doc whose PDF has been converted into the bucket
    // is sendable, and a .pdf that was never collected is not.
    [InlineData("Compliance Inspection WR51 - 032842002.docx", true, true)]
    [InlineData("Compliance Inspection WR51 - 032842002.docx", false, false)]
    [InlineData("Compliance Inspection WR51 - 032842002.pdf", true, true)]
    [InlineData("Compliance Inspection WR51 - 032842002.pdf", false, false)]
    [InlineData("", true, true)]
    public async Task WhenNoIngressObjectExists_ThenTheCandidateIsNeitherSentNorCounted(
        string fileName,
        bool inIngress,
        bool expectSent)
    {
        // Arrange
        var settings = new FileProcessAppSettings();
        var inspectionReportFinderCacheService = A.Fake<IInspectionReportFinderCacheService>();
        var outputService = A.Fake<IOutputService>();
        var messageQueueService = A.Fake<IMessageQueueService>();

        var candidate = new InspectionReportFinderResult
        {
            PermitNumber = "PERMIT-1",
            FileId = Guid.NewGuid().ToString(),
            FileName = fileName
        };

        A.CallTo(() => inspectionReportFinderCacheService.GetInspectionReportFinderResultsAsync(0, int.MaxValue))
            .Returns([candidate]);

        ProcessRun? startedProcessRun = null;

        A.CallTo(() => outputService.StartProcessRunAsync(A<ProcessRun>._))
            .ReturnsLazily(call =>
            {
                startedProcessRun = call.GetArgument<ProcessRun>(0);

                return Task.FromResult(startedProcessRun!);
            });

        var enqueuedRequests = new List<FileProcessSingleRequest>();

        A.CallTo(() => messageQueueService.AddToFileProcessQueueBatch(A<IReadOnlyList<FileProcessSingleRequest>>._))
            .ReturnsLazily(call =>
            {
                var batch = call.GetArgument<IReadOnlyList<FileProcessSingleRequest>>(0)!;
                enqueuedRequests.AddRange(batch);

                return Task.FromResult(new FileProcessQueueBatchResult
                {
                    Requested = batch.Count,
                    Enqueued = batch.Count
                });
            });

        var service = new FileProcessOrchestrationService(
            settings,
            A.Fake<ICacheService>(),
            inspectionReportFinderCacheService,
            outputService,
            messageQueueService,
            inIngress ? FileServiceHolding(candidate) : FileServiceHoldingNothing());

        // Act
        await service.RunAsync(CancellationToken.None);

        // Assert
        Assert.Equal(expectSent ? 1 : 0, enqueuedRequests.Count);
        Assert.Equal(expectSent ? 1 : 0, startedProcessRun?.NumberOfFiles);
    }

    /// <summary>
    /// The bug WRADI-391 fixes: the send loop was wrapped in one try/catch that rethrew, so a single
    /// rejected send (a 429 from the API, in the incident that prompted this) abandoned every
    /// remaining file in the run while the process run still recorded the full count.
    /// </summary>
    [Fact]
    public async Task WhenOneBatchFails_ThenTheRemainingBatchesAreStillSent()
    {
        // Arrange - 25 candidates over a batch size of 10 gives three batches
        var settings = new FileProcessAppSettings();
        var cacheService = A.Fake<ICacheService>();
        var inspectionReportFinderCacheService = A.Fake<IInspectionReportFinderCacheService>();
        var outputService = A.Fake<IOutputService>();
        var messageQueueService = A.Fake<IMessageQueueService>();

        var candidates = Enumerable.Range(0, 25)
            .Select(i => new InspectionReportFinderResult
            {
                PermitNumber = $"PERMIT-{i}",
                FileId = Guid.NewGuid().ToString(),
                FileUrl = $"https://example/file-{i}.pdf"
            })
            .ToList();

        A.CallTo(() => inspectionReportFinderCacheService.GetInspectionReportFinderResultsAsync(0, int.MaxValue))
            .Returns(candidates);

        A.CallTo(() => outputService.StartProcessRunAsync(A<ProcessRun>._))
            .ReturnsLazily(call => Task.FromResult(call.GetArgument<ProcessRun>(0)!));

        var enqueuedRequests = new List<FileProcessSingleRequest>();
        var batchNumber = 0;

        A.CallTo(() => messageQueueService.AddToFileProcessQueueBatch(A<IReadOnlyList<FileProcessSingleRequest>>._))
            .ReturnsLazily(call =>
            {
                var batch = call.GetArgument<IReadOnlyList<FileProcessSingleRequest>>(0)!;

                // The first batch throws the way a 429 did, the rest succeed.
                if (++batchNumber == 1)
                {
                    throw new HttpRequestException("Response status code does not indicate success: 429 (Too Many Requests).");
                }

                enqueuedRequests.AddRange(batch);

                return Task.FromResult(new FileProcessQueueBatchResult
                {
                    Requested = batch.Count,
                    Enqueued = batch.Count
                });
            });

        var service = new FileProcessOrchestrationService(
            settings,
            cacheService,
            inspectionReportFinderCacheService,
            outputService,
            messageQueueService,
            FileServiceHolding(candidates.ToArray()));

        // Act
        var result = await service.RunAsync(CancellationToken.None);

        // Assert - the run completes, and the 15 files after the failed batch still went
        Assert.True(result);
        Assert.Equal(15, enqueuedRequests.Count);
        Assert.Equal(3, batchNumber);
    }
}
