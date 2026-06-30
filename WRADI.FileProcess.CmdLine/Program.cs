using Amazon;
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
            var region = RegionEndpoint.GetBySystemName(settings.SqsRegionName);
            
            return new AmazonSQSClient(region);
        });
    })
    .RunConsoleAsync();