using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.LicenceFinder;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace WRADI.Lambda.LicenceFinder.EntryPoint;

[UsedImplicitly]
public class MessageReceivedFunction
{
    private readonly IServiceProvider _serviceProvider;

    public MessageReceivedFunction()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

       _serviceProvider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLicenceFinderApplication()
            .BuildServiceProvider();
    }
    
    [UsedImplicitly]
    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        context.Logger.LogInformation($"Licence Finder Message handler received {sqsEvent.Records.Count} SQS message(s).");
        context.Logger.LogInformation($"AwsRequestId: {context.AwsRequestId}");

        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                context.Logger.LogInformation(
                    $"Starting message. MessageId={record.MessageId}, " +
                    $"ApproxReceiveCount={GetAttribute(record, "ApproximateReceiveCount")}");

                context.Logger.LogInformation($"Body: {record.Body}");

                var licenceFinderMessageRequest = JsonConvert.DeserializeObject<LicenceFinderMessageRequest>(record.Body)
                    ?? new LicenceFinderMessageRequest();
                
                using var scope = _serviceProvider.CreateScope();
                var licenceFinderService = scope.ServiceProvider.GetRequiredService<ILicenceFinderService>();

                var result = await licenceFinderService.ExecuteAsync(licenceFinderMessageRequest, CancellationToken.None);

                context.Logger.LogInformation($"Completed Licence Finder Message handler service with result : {result}");

                if (string.IsNullOrEmpty(result))
                {
                    context.Logger.LogWarning(
                        $"Processing returned an empty string. Marking message as failed for retry. " +
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
                context.Logger.LogError($"Exception while processing message {record.MessageId}: {ex}");

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
