using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WALE.Api.Areas.BFF.Models;
using WALE.ProcessFile.Core.Helpers;
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
        var payload = BuildFileProcessSingleMessageBody(request);

        await sqsClient.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = awsQueueConfig.Value.FileProcessQueue,
                MessageBody = payload,
                DelaySeconds = request.DelayInSeconds > 0 ? request.DelayInSeconds : null
            });

        return Ok();
    }

    private const int SqsMaxBatchEntries = 10;

    [HttpPost]
    public async Task<ActionResult<SendFileProcessSingleMessagesResponse>> SendFileProcessSingleMessagesAsync(
        [FromBody] List<FileProcessSingleRequest> requests)
    {
        if (requests.Count == 0)
        {
            return Ok(new SendFileProcessSingleMessagesResponse());
        }

        var failedFileIds = new List<Guid>();

        foreach (var chunk in requests.Chunk(SqsMaxBatchEntries))
        {
            // Entry ids must be unique within a batch and are how a failure is traced back to a
            // file, so they are the chunk index, not the FileId.
            var entries = chunk
                .Select((request, index) => new SendMessageBatchRequestEntry
                {
                    Id = index.ToString(),
                    MessageBody = BuildFileProcessSingleMessageBody(request),
                    DelaySeconds = request.DelayInSeconds > 0 ? request.DelayInSeconds : null
                })
                .ToList();

            var response = await sqsClient.SendMessageBatchAsync(
                new SendMessageBatchRequest
                {
                    QueueUrl = awsQueueConfig.Value.FileProcessQueue,
                    Entries = entries
                });

            // SendMessageBatch returns 200 with a per-entry Failed collection: a partial failure is
            // normal (throttling in particular) and is NOT an exception. Ignoring this is how a
            // batch send silently drops files, which would be worse than the per-file send it
            // replaces, so every failed entry is reported back to the caller to deal with.
            foreach (var failure in response.Failed ?? [])
            {
                var failedRequest = int.TryParse(failure.Id, out var entryIndex) && entryIndex < chunk.Length
                    ? chunk[entryIndex]
                    : null;

                ConsoleHelper.WriteLine($"ERROR - {nameof(MessageController)} - SQS rejected "
                    + $"{failedRequest?.FilePath ?? failure.Id}: {failure.Code} {failure.Message} "
                    + $"(senderFault: {failure.SenderFault})");

                if (failedRequest != null)
                {
                    failedFileIds.Add(failedRequest.FileId);
                }
            }
        }

        return Ok(new SendFileProcessSingleMessagesResponse
        {
            Requested = requests.Count,
            Enqueued = requests.Count - failedFileIds.Count,
            FailedFileIds = failedFileIds
        });
    }

    private static string BuildFileProcessSingleMessageBody(FileProcessSingleRequest request)
    {
        return JsonSerializer.Serialize(new
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
    }
}