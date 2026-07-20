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
        
        var handleMessageTasks = new List<Task>();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqsClient.ReceiveMessageAsync(request, cancellationToken);

                if (response.Messages == null || response.Messages.Count == 0)
                {
                    handleMessageTasks = handleMessageTasks
                        .Where(handleMessageTask => !handleMessageTask.IsCompleted)
                        .ToList();
                    
                    continue;
                }

                handleMessageTasks.AddRange(response.Messages
                    .TakeWhile(_ => !cancellationToken.IsCancellationRequested)
                    .Select(message => HandleMessageAsync(message, cancellationToken, request)));
                
                handleMessageTasks = handleMessageTasks
                    .Where(handleMessageTask => !handleMessageTask.IsCompleted)
                    .ToList();
            }
            catch (OperationCanceledException operationCanceledException)
            {
                logger.LogError(operationCanceledException, "Operation canceled");
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled error while polling SQS");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        logger.LogInformation("{ServiceName} stopped.", nameof(FileProcessSingleFileHostedService));
    }
    
    private async Task HandleMessageAsync(
        Message message,
        CancellationToken cancellationToken,
        ReceiveMessageRequest request)
    {
        try
        {
            logger.LogInformation("Processing message {MessageId}", message.MessageId);
            logger.LogInformation("Message body: {Body}", message.Body);

            var fileProcessSingleRequest =
                JsonConvert.DeserializeObject<FileProcessSingleRequest>(message.Body);

            if (fileProcessSingleRequest?.FilePath == null)
            {
                return;
            }

            var result = await fileProcessSingleService.RunAsync(
                fileProcessSingleRequest,
                cancellationToken);

            if (!result)
            {
                return;
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