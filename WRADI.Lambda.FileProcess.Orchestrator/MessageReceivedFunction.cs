using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.AbstractionLicence;
using WRADI.Services.ProcessFile.WrInspectionReport;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace WRADI.Lambda.FileProcess.Orchestrator;

[UsedImplicitly]
public class MessageReceivedFunction
{
    private readonly IReadOnlyDictionary<string, IServiceProvider> _serviceProvidersByDocumentType;

    public MessageReceivedFunction()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var abstractionLicenceServices = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddFileProcessServices(configuration)
            .BuildServiceProvider();

        var wrInspectionReportServices = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddWrInspectionReportFileProcessServices(configuration)
            .BuildServiceProvider();

        _serviceProvidersByDocumentType = new Dictionary<string, IServiceProvider>
        {
            ["AbstractionLicence"] = abstractionLicenceServices,
            ["WrInspectionReport"] = wrInspectionReportServices
        };
    }

    [UsedImplicitly]
    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        context.Logger.LogInformation($"File Process Orchestrator received {sqsEvent.Records.Count} SQS message(s).");
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

                var orchestrationRequest = JsonConvert.DeserializeObject<FileProcessOrchestrationRequest>(record.Body)
                    ?? new FileProcessOrchestrationRequest();

                if (!_serviceProvidersByDocumentType.TryGetValue(
                        orchestrationRequest.DocumentType, out var serviceProvider))
                {
                    context.Logger.LogWarning(
                        $"Unrecognised DocumentType '{orchestrationRequest.DocumentType}' - " +
                        $"falling back to AbstractionLicence. MessageId={record.MessageId}");

                    serviceProvider = _serviceProvidersByDocumentType["AbstractionLicence"];
                }

                using var scope = serviceProvider.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IFileProcessOrchestrator>();

                var result = await orchestrator.RunAsync(CancellationToken.None);

                context.Logger.LogInformation($"Completed Orchestration service with result : {result}");

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
