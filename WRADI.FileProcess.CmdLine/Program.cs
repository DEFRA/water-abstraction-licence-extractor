using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WALE.ProcessFile.Services.AwsSqs;
using WRADI.FileProcess.CmdLine.BackgroundServices;
using WRADI.Services.ProcessFile.AbstractionLicence;

// NOTE - This is used locally rather than running the lambdas to process messages

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
            .AddFileProcessServices(context.Configuration)
            .AddHostedService<FileProcessOrchestrationHostedService>()
            .AddHostedService<FileProcessSingleFileHostedService>()
            .AddSingleton<IAmazonSQS>(sp =>
            {
                var settings = sp.GetRequiredService<FileProcessAppSettings>();

                return AwsSqsHelper.GetAwsSqsClient(
                    settings.AwsRegionName!,
                    settings.AwsAccessKey,
                    settings.AwsSecretKey,
                    settings.AwsSessionToken);
            });
    })
    .RunConsoleAsync();