using FakeItEasy;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Services.ProcessFile.AbstractionLicence;
using WRADI.Services.ProcessFile.AbstractionLicence.Implementations;

namespace WRADI.Services.AbstractionLicence.Tests;

public class FileProcessOrchestrationServiceTests
{
    [Fact]
    public async Task WhenFilesToProcessExist_ThenEnqueuedRequestsAreStampedWithAbstractionLicenceDocumentType()
    {
        // Arrange
        var settings = new FileProcessAppSettings();
        var cacheService = A.Fake<ICacheService>();
        var abstractionLicenceCacheService = A.Fake<IAbstractionLicenceCacheService>();
        var outputService = A.Fake<IOutputService>();
        var fileService = A.Fake<IFileService>();
        var messageQueueService = A.Fake<IMessageQueueService>();

        var fileId = Guid.NewGuid().ToString().ToLower();
        var permitNumber = "permit1";
        var destinationFileName = $"{permitNumber}__{fileId}.pdf";

        A.CallTo(() => fileService.GetAllFilesAsync())
            .Returns([destinationFileName]);

        A.CallTo(() => abstractionLicenceCacheService.GetLicenceFinderResultsAsync(0, int.MaxValue))
            .Returns(
            [
                new LicenceFinderResult
                {
                    PermitNumber = permitNumber,
                    FileId = fileId,
                    LiveLicenceFound = true,
                    Region = "Anglian",
                    LicenseNumber = "12/34/56",
                    FileUrl = "https://example/file.pdf"
                }
            ]);

        A.CallTo(() => outputService.StartProcessRunAsync(A<ProcessRun>._))
            .ReturnsLazily(call => Task.FromResult(call.GetArgument<ProcessRun>(0)!));

        var enqueuedRequests = new List<FileProcessSingleRequest>();

        A.CallTo(() => messageQueueService.AddToFileProcessQueue(A<FileProcessSingleRequest>._))
            .Invokes(call => enqueuedRequests.Add(call.GetArgument<FileProcessSingleRequest>(0)!));

        var service = new FileProcessOrchestrationService(
            settings,
            cacheService,
            abstractionLicenceCacheService,
            outputService,
            fileService,
            messageQueueService);

        // Act
        var result = await service.RunAsync(CancellationToken.None);

        // Assert
        Assert.True(result);
        var enqueuedRequest = Assert.Single(enqueuedRequests);
        Assert.Equal("AbstractionLicence", enqueuedRequest.DocumentType);
    }
}
