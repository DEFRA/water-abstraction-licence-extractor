using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile;

namespace WRADI.FileProcess.CmdLine.BackgroundServices;

public sealed class FileProcessSingleFileHostedService(
    IAmazonSQS sqsClient,
    IFileProcessSingleService fileProcessSingleService,
    FileProcessAppSettings settings,
    ILogger<FileProcessSingleFileHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = settings.SqsQueueFileProcessUrl,
            MaxNumberOfMessages = settings.SqsMaxNumberOfMessages,
            WaitTimeSeconds = settings.SqsWaitTimeSeconds
        };
        
        logger.LogInformation("{ServiceName} started. Queue: {QueueUrl}",
            nameof(FileProcessSingleFileHostedService),
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

        logger.LogInformation("{ServiceName} stopped.", nameof(FileProcessSingleFileHostedService));
    }
}