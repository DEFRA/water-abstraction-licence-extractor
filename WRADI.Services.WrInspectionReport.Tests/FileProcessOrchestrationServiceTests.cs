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

        A.CallTo(() => messageQueueService.AddToFileProcessQueue(A<FileProcessSingleRequest>._))
            .Invokes(call => enqueuedRequests.Add(call.GetArgument<FileProcessSingleRequest>(0)!));

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
}
