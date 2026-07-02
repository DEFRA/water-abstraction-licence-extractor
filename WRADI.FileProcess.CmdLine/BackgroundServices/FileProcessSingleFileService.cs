using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile;

namespace WRADI.FileProcess.CmdLine.BackgroundServices;

public sealed class FileProcessSingleFileService(
    IAmazonSQS sqsClient,
    IFileProcessSingleService fileProcessSingleService,
    FileProcessAppSettings settings,
    ILogger<FileProcessSingleFileService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Single file SQS worker started. Queue: {QueueUrl}",
            settings.SqsQueueFileProcessUrl);

        var request = new ReceiveMessageRequest
        {
            QueueUrl = settings.SqsQueueFileProcessUrl,
            MaxNumberOfMessages = settings.SqsMaxNumberOfMessages,
            WaitTimeSeconds = settings.SqsWaitTimeSeconds
        };

        if (settings.SqsVisibilityTimeoutSeconds.HasValue)
        {
            request.VisibilityTimeout = settings.SqsVisibilityTimeoutSeconds.Value;
        }
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqsClient.ReceiveMessageAsync(request, cancellationToken);

                if (response.Messages == null || response.Messages.Count == 0)
                {
                    continue;
                }

                foreach (var message in response.Messages
                    .TakeWhile(message => !cancellationToken.IsCancellationRequested))
                {
                    try
                    {
                        logger.LogInformation("Processing message {MessageId}", message.MessageId);
                        logger.LogInformation("Message body: {Body}", message.Body);

                        var singleFileProcessRequest =
                            JsonConvert.DeserializeObject<SingleFileProcessRequest>(message.Body);

                        if (singleFileProcessRequest?.FilePath == null)
                        {
                            continue;
                        }

                        var result = await fileProcessSingleService.RunAsync(
                            singleFileProcessRequest,
                            cancellationToken);

                        if (!result)
                        {
                            continue;
                        }
                        
                        await sqsClient.DeleteMessageAsync(
                            new DeleteMessageRequest
                            {
                                QueueUrl = settings.SqsQueueFileProcessUrl,
                                ReceiptHandle = message.ReceiptHandle
                            },
                            cancellationToken);
                           
                        logger.LogInformation("Deleted message {MessageId}", message.MessageId);
                    }
                    catch (Exception ex)
                    { 
                        logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
                        // Leave message on queue so it can be retried
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error while polling SQS");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        logger.LogInformation("Single SQS worker stopped.");
    }
}