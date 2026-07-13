using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WALE.ProcessFile.Core.Interfaces;
using WRADI.Services.ProcessFile;

namespace WRADI.FileProcess.CmdLine.BackgroundServices;

public sealed class FileProcessOrchestrationHostedService(
    IAmazonSQS sqsClient,
    IFileProcessOrchestrator fileProcessOrchestrator,
    FileProcessAppSettings settings,
    ILogger<FileProcessOrchestrationHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = settings.SqsQueueOrchestrationUrl,
            MaxNumberOfMessages = settings.SqsMaxNumberOfMessages,
            WaitTimeSeconds = settings.SqsWaitTimeSeconds
        };
        
        logger.LogInformation("{ServiceName} started. Queue: {QueueUrl}",
            nameof(FileProcessOrchestrationHostedService),
            request.QueueUrl);

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
                    .TakeWhile(_ => !cancellationToken.IsCancellationRequested))
                {
                    try
                    {
                        logger.LogInformation("Processing message {MessageId}", message.MessageId);
                        logger.LogInformation("Message body: {Body}", message.Body);

                        var result = await fileProcessOrchestrator.RunAsync(
                            cancellationToken);

                        if (!result)
                        {
                            continue;
                        }
                        
                        await sqsClient.DeleteMessageAsync(
                            new DeleteMessageRequest
                            {
                                QueueUrl = request.QueueUrl,
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

        logger.LogInformation("{ServiceName} stopped.", nameof(FileProcessOrchestrationHostedService));
    }
}