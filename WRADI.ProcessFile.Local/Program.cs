using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WALE.ProcessFile.Services.AwsSqs;
using WRADI.ProcessFile.Local;
using WRADI.ProcessFile.Local.BackgroundServices;
using WRADI.Services.ProcessFile.AbstractionLicence;
using WRADI.Services.ProcessFile.WrInspectionReport;

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<DocumentTypeServiceProviders>(optional: true)
    .Build();

var abstractionLicenceServices = new ServiceCollection()
    .AddFileProcessServices(configuration)
    .BuildServiceProvider();

var wrInspectionReportServices = new ServiceCollection()
    .AddWrInspectionReportFileProcessServices(configuration)
    .BuildServiceProvider();

var documentTypeServiceProviders = new DocumentTypeServiceProviders(
    new Dictionary<string, IServiceProvider>
    {
        ["AbstractionLicence"] = abstractionLicenceServices,
        ["WrInspectionReport"] = wrInspectionReportServices
    });

var pollingSettings = abstractionLicenceServices
    .GetRequiredService<WRADI.Services.ProcessFile.AbstractionLicence.FileProcessAppSettings>();

await Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>(optional: true);
    })
    .ConfigureServices((_, services) =>
    {
        services
            .AddSingleton(documentTypeServiceProviders)
            .AddSingleton(pollingSettings)
            .AddHostedService<FileProcessOrchestrationHostedService>()
            .AddHostedService<FileProcessSingleFileHostedService>()
            .AddSingleton<IAmazonSQS>(_ => AwsSqsHelper.GetAwsSqsClient(
                pollingSettings.AwsRegionName!,
                pollingSettings.AwsAccessKey,
                pollingSettings.AwsSecretKey,
                pollingSettings.AwsSessionToken));
    })
    .RunConsoleAsync();