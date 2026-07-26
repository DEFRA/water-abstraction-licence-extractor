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
    [HttpPost]
    public async Task<IActionResult> SendFileProcessOrchestrationMessageAsync(
        [FromQuery] int delayInSeconds = 0)
    {
        var payload = JsonSerializer.Serialize(new
        {
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
            request.LockRetryCount
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