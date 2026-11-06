using FakeItEasy;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.Cache.WrInspectionReport.Interfaces;
using WRADI.Services.Cache.WrInspectionReport.Models;
using WRADI.Services.ProcessFile.WrInspectionReport;
using WRADI.Services.ProcessFile.WrInspectionReport.Implementations;

namespace WRADI.Services.WrInspectionReport.Tests;

public class WrInspectionReportFileProcessOrchestrationServiceTests
{
    /// The orchestrator selects on what is in ingress, so a fake listing nothing filters every
    /// candidate out and a send assertion fails for the wrong reason.
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

        var service = new WrInspectionReportFileProcessOrchestrationService(
            settings,
            cacheService,
            inspectionReportFinderCacheService,
            outputService,
            messageQueueService,
            FileServiceHolding(candidate));

        // Act
        var result = await service.RunAsync(
            new FileProcessOrchestrationRequest
            {
                MaxLicencesToTake = 100_000
            },
            CancellationToken.None);

        // Assert
        Assert.True(result);
        var enqueuedRequest = Assert.Single(enqueuedRequests);
        Assert.Equal("WrInspectionReport", enqueuedRequest.DocumentType);
    }

    /// An unparseable or absent upload date cannot be compared, so the candidate is left out -
    /// visibly, not silently.
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

    /// The cap draws from the window, or a windowed run is indistinguishable from an arbitrary
    /// MaxFilesPerRun slice.
    [Fact]
    public async Task WhenAWindowAndACapAreBothSet_ThenTheCapAppliesToTheWindowedCandidates()
    {
        // Arrange
        var settings = new FileProcessAppSettings
        {
            UploadedFrom = new DateOnly(2025, 1, 1),
            MaxFilesPerRun = 2
        };

        // Asserted on which files, not how many: two of five is also what an unwindowed cap sends.
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

    /// Nothing else retains the window afterwards.
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
            List<InspectionReportFinderResult> candidates,
            FileProcessOrchestrationRequest? request = null)
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

        var service = new WrInspectionReportFileProcessOrchestrationService(
            settings,
            A.Fake<ICacheService>(),
            inspectionReportFinderCacheService,
            outputService,
            messageQueueService,
            FileServiceHolding(candidates.ToArray()));

        await service.RunAsync(request ?? new FileProcessOrchestrationRequest(), CancellationToken.None);

        return (enqueuedRequests, startedProcessRun);
    }

    /// A cap spent on documents the window then removes sends fewer than were asked for.
    [Fact]
    public async Task WhenAWindowAndARequestCapAreBothSet_ThenTheCapAppliesToTheWindowedCandidates()
    {
        // Arrange
        var settings = new FileProcessAppSettings
        {
            UploadedFrom = new DateOnly(2025, 1, 1)
        };

        var candidates = new[]
            {
                ("OUTSIDE-1", "2024-01-01 00:00:00"),
                ("OUTSIDE-2", "2024-02-01 00:00:00"),
                ("INSIDE-1", "2025-03-01 00:00:00"),
                ("INSIDE-2", "2025-04-01 00:00:00"),
                ("INSIDE-3", "2025-05-01 00:00:00")
            }
            .Select(candidate => new InspectionReportFinderResult
            {
                PermitNumber = candidate.Item1,
                FileId = Guid.NewGuid().ToString(),
                FileName = "report.pdf",
                UploadDate = candidate.Item2
            })
            .ToList();

        // Act
        var (enqueuedRequests, startedProcessRun) = await RunWithCandidatesAsync(
            settings,
            candidates,
            new FileProcessOrchestrationRequest { MaxLicencesToTake = 2 });

        // Assert
        Assert.Equal(2, enqueuedRequests.Count);
        Assert.Equal(2, startedProcessRun?.NumberOfFiles);
        Assert.All(enqueuedRequests, request => Assert.StartsWith("INSIDE-", request.PermitNumber));
    }

    [Theory]
    // Open-ended end, so 2026 is included: inheriting the configured 2025 end would drop it
    [InlineData("2024-01-01", null, 4)]
    // Open-ended start: inheriting the configured 2025 start would leave an empty window
    [InlineData(null, "2024-12-31", 2)]
    // No request window at all, so configuration's 2025 window still applies
    [InlineData(null, null, 1)]
    public async Task WhenTheRequestNamesAWindow_ThenItReplacesTheConfiguredOneAsAPair(
        string? requestedFrom,
        string? requestedTo,
        int expectedSent)
    {
        // Arrange
        var settings = new FileProcessAppSettings
        {
            UploadedFrom = new DateOnly(2025, 1, 1),
            UploadedTo = new DateOnly(2025, 12, 31)
        };

        var candidates = new[]
            {
                ("OLD-1", "2024-03-01 00:00:00"),
                ("OLD-2", "2024-04-01 00:00:00"),
                ("NEW-1", "2025-06-15 00:00:00"),
                ("FUTURE-1", "2026-02-01 00:00:00")
            }
            .Select(candidate => new InspectionReportFinderResult
            {
                PermitNumber = candidate.Item1,
                FileId = Guid.NewGuid().ToString(),
                FileName = "report.pdf",
                UploadDate = candidate.Item2
            })
            .ToList();

        // Act
        var (enqueuedRequests, _) = await RunWithCandidatesAsync(
            settings,
            candidates,
            new FileProcessOrchestrationRequest
            {
                UploadedFrom = requestedFrom == null ? null : DateOnly.Parse(requestedFrom),
                UploadedTo = requestedTo == null ? null : DateOnly.Parse(requestedTo)
            });

        // Assert
        Assert.Equal(expectedSent, enqueuedRequests.Count);
    }

    public static TheoryData<int?, int> RequestLimits => new()
    {
        { null, 3 },
        { 0, 3 },
        { 2, 2 }
    };

    [Theory]
    [MemberData(nameof(RequestLimits))]
    public async Task WhenTheRequestCapsLicences_ThenOnlyThatManyAreSent(
        int? maxLicencesToTake,
        int expectedSent)
    {
        // Arrange
        var candidates = new[] { "PERMIT-1", "PERMIT-2", "PERMIT-3" }
            .Select(permitNumber => new InspectionReportFinderResult
            {
                PermitNumber = permitNumber,
                FileId = Guid.NewGuid().ToString(),
                FileName = "report.pdf",
                UploadDate = "2025-06-15 00:00:00"
            })
            .ToList();

        // Act
        var (enqueuedRequests, startedProcessRun) = await RunWithCandidatesAsync(
            new FileProcessAppSettings(),
            candidates,
            new FileProcessOrchestrationRequest { MaxLicencesToTake = maxLicencesToTake });

        // Assert
        Assert.Equal(expectedSent, enqueuedRequests.Count);
        Assert.Equal(expectedSent, startedProcessRun?.NumberOfFiles);
    }

    /// NumberOfFiles is written before anything is sent, so a skipped candidate must be absent
    /// from the count as well as the queue.
    [Theory]
    // Both extensions appear with both outcomes: the bucket decides, not the DMS file name.
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

        var service = new WrInspectionReportFileProcessOrchestrationService(
            settings,
            A.Fake<ICacheService>(),
            inspectionReportFinderCacheService,
            outputService,
            messageQueueService,
            inIngress ? FileServiceHolding(candidate) : FileServiceHoldingNothing());

        // Act
        await service.RunAsync(new FileProcessOrchestrationRequest(), CancellationToken.None);

        // Assert
        Assert.Equal(expectSent ? 1 : 0, enqueuedRequests.Count);
        Assert.Equal(expectSent ? 1 : 0, startedProcessRun?.NumberOfFiles);
    }

    /// One try/catch around the whole send loop meant a single rejected send - a 429 - abandoned
    /// every remaining file while the run still recorded the full count.
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

                // The first batch throws as a 429 does, the rest succeed.
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

        var service = new WrInspectionReportFileProcessOrchestrationService(
            settings,
            cacheService,
            inspectionReportFinderCacheService,
            outputService,
            messageQueueService,
            FileServiceHolding(candidates.ToArray()));

        // Act
        var result = await service.RunAsync(new FileProcessOrchestrationRequest(), CancellationToken.None);

        // Assert - the run completes, and the 15 files after the failed batch still went
        Assert.True(result);
        Assert.Equal(15, enqueuedRequests.Count);
        Assert.Equal(3, batchNumber);
    }
}
