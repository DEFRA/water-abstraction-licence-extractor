using Amazon.SQS;
using Microsoft.Extensions.DependencyInjection;

namespace WALE.ProcessFile.Services.AwsSqs;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqsServices(
        this IServiceCollection services,
        string awsRegionName,
        string? awsAccessKey,
        string? awsSecretKey,
        string? awsSessionToken)
    {
        services
            .AddSingleton<IAmazonSQS>(_ => SqsHelper.GetSqsClient(
                awsRegionName,
                awsAccessKey,
                awsSecretKey,
                awsSessionToken))
            .AddOptions<AwsQueueConfig>()
            .BindConfiguration("AwsQueueConfig")
            .Validate(configLocal => !string.IsNullOrWhiteSpace(configLocal.OrchestratorQueue),
                "AwsQueueConfig:OrchestratorQueue is required")
            .Validate(configLocal => !string.IsNullOrWhiteSpace(configLocal.FileProcessQueue),
                "AwsQueueConfig:FileProcessQueue is required")
            .ValidateOnStart();

        return services;
    }
}