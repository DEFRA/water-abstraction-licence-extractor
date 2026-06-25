using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using WALE.OrchestrateFileProcess.Services;
using WALE.OrchestrateFileProcess.Services.Implementations;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.AwsS3;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WRADI.ProcessFile.DependInjection;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace WRADI.Lambda.FileProcess;

public class MessageReceivedFunction
{
    private readonly IServiceProvider _serviceProvider;

    public MessageReceivedFunction()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddFileProcessingServices(configuration);

        _serviceProvider = services.BuildServiceProvider();
    }
    
public async Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
{
    context.Logger.LogInformation($"Entry Point handler Received {sqsEvent.Records.Count} SQS message(s).");
    context.Logger.LogInformation($"AwsRequestId: {context.AwsRequestId}");
    
    using var scope = _serviceProvider.CreateScope();
    var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrateFileProcess>();

    var failures = new List<SQSBatchResponse.BatchItemFailure>();
    
    foreach (var record in sqsEvent.Records)
    {
        try
        {
            context.Logger.LogInformation(
                $"Starting message. MessageId={record.MessageId}, ApproxReceiveCount={GetAttribute(record, "ApproximateReceiveCount")}");

            context.Logger.LogInformation($"Body: {record.Body}");
            context.Logger.LogInformation(
                $"Starting Orchestration service");
            var result = await orchestrator.RunAsync(CancellationToken.None);
            context.Logger.LogInformation(
                $"Completed Orchestration service with result : {result}");

            if (!result)
            {
                context.Logger.LogWarning(
                    $"Processing returned false. Marking message as failed for retry. MessageId={record.MessageId}");

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
                $"Exception while processing message {record.MessageId}: {ex}");

            failures.Add(new SQSBatchResponse.BatchItemFailure
            {
                ItemIdentifier = record.MessageId
            });
        }
    }

    context.Logger.LogInformation(
        $"Finished batch. Total={sqsEvent.Records.Count}, Failed={failures.Count}, Succeeded={sqsEvent.Records.Count - failures.Count}");

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