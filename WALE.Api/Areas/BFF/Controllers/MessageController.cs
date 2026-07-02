using System.Text.Json;
using Amazon.SQS;
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
            request.FilePath,
            request.ProcessRunId,
            RequestedAt = DateTime.UtcNow
        };

        var messageBody = JsonSerializer.Serialize(payload);

        await sqsClient.SendMessageAsync(awsQueueConfig.Value.FileProcessQueue, messageBody);
        return Ok();
    }
}