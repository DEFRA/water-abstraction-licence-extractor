using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WRADI.FileProcess.CmdLine;
using WRADI.Services.ProcessFile;
using WRADI.Services.ProcessFile.Orchestrate;

// NOTE - This is used rather then running the lambdas locally to process messages

await Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>(optional: true);
    })
    .ConfigureServices((context, services) =>
    {
        services
            .AddFileProcessingServices(context.Configuration)
            .AddHostedService<FileProcessOrchestrationService>()
            .AddHostedService<FileProcessSingleFileService>()
            .AddSingleton<IAmazonSQS>(sp =>
        {
            var settings = sp.GetRequiredService<FileProcessAppSettings>();

            return GetSqsClient(
                settings.AwsRegionName!,
                settings.AwsAccessKey,
                settings.AwsSecretKey,
                settings.AwsSessionToken);
        });
    })
    .RunConsoleAsync();

return;

static AmazonSQSClient GetSqsClient(
    string regionName,
    string? accessKey,
    string? secretKey,
    string? sessionToken)
{
    var sqsConfig = new AmazonSQSConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(regionName)
    };
        
    AmazonSQSClient client;

    if (!string.IsNullOrEmpty(accessKey))
    {
        if (!string.IsNullOrEmpty(sessionToken))
        {
            client = new AmazonSQSClient(
                new SessionAWSCredentials(accessKey, secretKey, sessionToken),
                sqsConfig);                
        }
        else
        {
            client = new AmazonSQSClient(
                new BasicAWSCredentials(accessKey, secretKey),
                sqsConfig);
        }
    }
    else
    {
        client = new AmazonSQSClient(sqsConfig);
    }
    
    return client;
}