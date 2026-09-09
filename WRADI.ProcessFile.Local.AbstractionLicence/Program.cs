using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WALE.ProcessFile.Services.AwsSqs;
using WRADI.ProcessFile.Local.AbstractionLicence.BackgroundServices;
using WRADI.Services.ProcessFile.AbstractionLicence;
//using WRADI.Services.AbstractionLicence.Extensions;

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
            //.AddAddAbstractionLicenceServices(context.Configuration)
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