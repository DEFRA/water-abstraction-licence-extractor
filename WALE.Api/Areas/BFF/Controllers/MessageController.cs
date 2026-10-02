using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.AwsSqs;
using FileProcessSingleRequest = WALE.Api.Areas.BFF.Models.FileProcessSingleRequest;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class MessageController(
    IOptions<AwsSqsQueueConfig> awsQueueConfig,
    IAmazonSQS sqsClient) : Controller
{
    [HttpPost]
    public async Task<IActionResult> SendFileProcessOrchestrationMessageAsync(
        [FromQuery] int delayInSeconds = 0,
        [FromQuery] int regionId = 0,
        [FromQuery] int maxLicencesToTake = 100_000,        
        [FromQuery] string documentType = "AbstractionLicence")
    {
        var messagePayload = JsonSerializer.Serialize(
            new FileProcessOrchestrationRequest
            {
                DocumentType = documentType,
                RequestedAt = DateTime.UtcNow,
                RegionId = regionId != 0 ? regionId : null,
                MaxLicencesToTake = maxLicencesToTake
            });

        await sqsClient.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = awsQueueConfig.Value.OrchestratorQueue,
                MessageBody = messagePayload,
                DelaySeconds = delayInSeconds > 0 ? delayInSeconds : null
            });

        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> SendFileProcessSingleMessageAsync(
        [FromBody] FileProcessSingleRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            request.DestinationFileName,
            request.DmsPath,
            request.FileId,
            request.FilePath,
            request.PermitNumber,
            request.RegionId,
            request.ProcessRunId,
            request.RequestedAt,
            request.LockRetryCount,
            request.DocumentType
        });

        await sqsClient.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = awsQueueConfig.Value.FileProcessQueue,
                MessageBody = payload,
                DelaySeconds = request.DelayInSeconds > 0 ? request.DelayInSeconds : null
            });

        return Ok();
    }
}