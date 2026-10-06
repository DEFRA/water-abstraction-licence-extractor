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
            messageQueueService);

        // Act
        var result = await service.RunAsync(CancellationToken.None);

        // Assert
        Assert.True(result);
        var enqueuedRequest = Assert.Single(enqueuedRequests);
        Assert.Equal("WrInspectionReport", enqueuedRequest.DocumentType);
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
            messageQueueService);

        // Act
        var result = await service.RunAsync(CancellationToken.None);

        // Assert - the run completes, and the 15 files after the failed batch still went
        Assert.True(result);
        Assert.Equal(15, enqueuedRequests.Count);
        Assert.Equal(3, batchNumber);
    }
}
