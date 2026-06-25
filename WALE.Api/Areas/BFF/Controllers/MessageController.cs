using Amazon.SQS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WALE.Api.Areas.BFF.Models;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class MessageController(IOptions<AwsQueueConfig> awsQueueConfig, IAmazonSQS sqsClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> StartOrchestrationAsync()
    {
        var payload = new
        {
            RequestedAt = DateTime.UtcNow
        };

        var messageBody = System.Text.Json.JsonSerializer.Serialize(payload);

        await sqsClient.SendMessageAsync(awsQueueConfig.Value.OrchestratorQueue, messageBody);
        return Ok();
    }
    
    [HttpGet]
    public async Task<IActionResult> AddFileToProcessAsync([FromQuery] string filePath, int  processRunId)
    {
        var payload = new
        {
            filePath,
            processRunId,
            RequestedAt = DateTime.UtcNow
        };

        var messageBody = System.Text.Json.JsonSerializer.Serialize(payload);

        await sqsClient.SendMessageAsync(awsQueueConfig.Value.FileProcessQueue, messageBody);
        return Ok();
    }
}