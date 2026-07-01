using System.Text.Json;
using Amazon.SQS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WALE.Api.Areas.BFF.Models;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class MessageController(
    IOptions<AwsQueueConfig> awsQueueConfig,
    IAmazonSQS sqsClient) : Controller
{
    [HttpPost]
    public async Task<IActionResult> SendFileProcessOrchestrationMessageAsync()
    {
        var payload = new
        {
            RequestedAt = DateTime.UtcNow
        };

        var messageBody = JsonSerializer.Serialize(payload);

        await sqsClient.SendMessageAsync(awsQueueConfig.Value.OrchestratorQueue, messageBody);
        return Ok();
    }
    
    [HttpPost]
    public async Task<IActionResult> SendFileProcessSingleMessageAsync(
        [FromBody] SendFileProcessSingleMessageRequest request)
    {
        var payload = new
        {
            FilePath = request.filePath,
            ProcessRunId = request.processRunId,
            RequestedAt = DateTime.UtcNow
        };

        var messageBody = JsonSerializer.Serialize(payload);

        await sqsClient.SendMessageAsync(awsQueueConfig.Value.FileProcessQueue, messageBody);
        return Ok();
    }
}