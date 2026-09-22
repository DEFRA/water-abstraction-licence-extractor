using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WALE.Api.Areas.BFF.Models;
using WALE.ProcessFile.Services.AwsSqs;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class MessageController(
    IOptions<AwsSqsQueueConfig> awsQueueConfig,
    IAmazonSQS sqsClient) : Controller
{
    // Both queues are shared across every document type - the consumer picks the right
    // document-type-specific implementation per message (DocumentType below), not per
    // queue/deployment.
    [HttpPost]
    public async Task<IActionResult> SendFileProcessOrchestrationMessageAsync(
        [FromQuery] int delayInSeconds = 0,
        [FromQuery] string documentType = "AbstractionLicence")
    {
        var payload = JsonSerializer.Serialize(new WALE.ProcessFile.Core.Models.FileProcessOrchestrationRequest
        {
            DocumentType = documentType,
            RequestedAt = DateTime.UtcNow
        });

        await sqsClient.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = awsQueueConfig.Value.OrchestratorQueue,
                MessageBody = payload,
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