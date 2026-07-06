using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace WRADI.Lambda.FileProcess.Single;

[UsedImplicitly]
public class MessageReceivedFunction
{
    private readonly IServiceProvider _serviceProvider;

    public MessageReceivedFunction()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddFileProcessServices(configuration);

        _serviceProvider = services.BuildServiceProvider();
    }
    
    [UsedImplicitly]
    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        context.Logger.LogInformation($"File Process Single - " +
            $"Received {sqsEvent.Records.Count} SQS message(s).");
        
        context.Logger.LogInformation($"AwsRequestId: {context.AwsRequestId}");
        
        using var scope = _serviceProvider.CreateScope();
        var scrapeFileService = scope.ServiceProvider.GetRequiredService<IFileProcessSingleService>();

        var failures = new List<SQSBatchResponse.BatchItemFailure>();
        
        foreach (var record in sqsEvent.Records)
        {
            try
            {
                context.Logger.LogInformation(
                    $"Starting message. MessageId={record.MessageId}, " +
                    $"ApproxReceiveCount={GetAttribute(record, "ApproximateReceiveCount")}");

                context.Logger.LogInformation($"Body: {record.Body}");
                
                var singleFileProcessRequest =
                    JsonConvert.DeserializeObject<SingleFileProcessRequest>(record.Body);

                if (singleFileProcessRequest?.FilePath == null)
                {
                    continue;
                }
             
                context.Logger.LogInformation($"Scrapping service starting for : {singleFileProcessRequest.FilePath}");
                
                var result = await scrapeFileService.RunAsync(
                    singleFileProcessRequest,
                    CancellationToken.None);
                
                context.Logger.LogInformation($"Scrapping service completed for:" +
                    $"{singleFileProcessRequest.FilePath} with result : {result}");
                
                if (!result)
                {
                    context.Logger.LogWarning(
                        $"Processing returned false. Marking message as failed for retry. " +
                        $"MessageId={record.MessageId}");

                    failures.Add(new SQSBatchResponse.BatchItemFailure
                    {
                        ItemIdentifier = record.MessageId
                    });

                    continue;
                }

                context.Logger.LogInformation($"Processing succeeded. MessageId={record.MessageId}");
            }
            catch (Exception ex)
            {
                context.Logger.LogError(
                    $"Exception while processing message single file {record.MessageId}: {ex} - {record.Body}");

                failures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = record.MessageId
                });
            }
        }

        context.Logger.LogInformation(
            $"Finished batch. Total={sqsEvent.Records.Count}, Failed={failures.Count}, " +
            $"Succeeded={sqsEvent.Records.Count - failures.Count}");

        return new SQSBatchResponse(failures);
    }

    private static string GetAttribute(SQSEvent.SQSMessage record, string key)
    {
        if (record.Attributes != null && record.Attributes.TryGetValue(key, out var value))
        {
            return value;
        }

        return "unknown";
    }
}