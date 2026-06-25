using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WALE.OrchestrateFileProcess.Services;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WRADI.QueueFileProcess.Cmd;

public sealed class SingleFileProcessHostedService(
    IAmazonSQS sqs,
    IScrapeFileService scrapeFileService,
    AppSettings settings,
    ILogger<SingleFileProcessHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SQS worker started. Queue: {QueueUrl}", settings.SqsQueueFileProcessUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
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

                var response = await sqs.ReceiveMessageAsync(request, stoppingToken);

                if (response.Messages == null)
                {
                    continue;
                }

                if (response.Messages.Count == 0)
                {
                    continue;
                }

                foreach (var message in response.Messages)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    try
                    {
                        logger.LogInformation("Processing message {MessageId}", message.MessageId);
                        logger.LogInformation("Message body: {Body}", message.Body);

                       var singleFileProcessRequest = JsonConvert.DeserializeObject<SingleFileProcessRequest>(message.Body);

                       if (singleFileProcessRequest?.FilePath != null)
                       {
                           var result = await scrapeFileService.RunAsync(singleFileProcessRequest, stoppingToken);

                           if (result)
                           {
                               await sqs.DeleteMessageAsync(
                                   new DeleteMessageRequest
                                   {
                                       QueueUrl = settings.SqsQueueFileProcessUrl,
                                       ReceiptHandle = message.ReceiptHandle
                                   },
                                   stoppingToken);
                               logger.LogInformation("Deleted message {MessageId}", message.MessageId);
                           }
                       }
                    }
                    catch (Exception ex)
                    { 
                        logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
                        // leave message on queue so it can be retried
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
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("SQS worker stopped.");
    }
}