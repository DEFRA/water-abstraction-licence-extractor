using Amazon;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WALE.OrchestrateFileProcess.Services;
using WRADI.ProcessFile.DependInjection;
using WRADI.QueueFileProcess.Cmd;

await Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddEnvironmentVariables();
        config.AddUserSecrets<Program>(optional: true);
    })
    .ConfigureServices((context, services) =>
    {
        services.AddFileProcessingServices(context.Configuration);
        services.AddHostedService<OrchestrationHostedService>();
        services.AddHostedService<SingleFileProcessHostedService>();
        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var settings = sp.GetRequiredService<AppSettings>();
            var region = RegionEndpoint.GetBySystemName(settings.SqsRegionName);
            return new AmazonSQSClient(region);
        });
    })
    .RunConsoleAsync();