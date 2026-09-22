using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WALE.ProcessFile.Services.AwsSqs;
using WRADI.ProcessFile.Local;
using WRADI.ProcessFile.Local.BackgroundServices;
using WRADI.Services.ProcessFile.AbstractionLicence;
using WRADI.Services.ProcessFile.WrInspectionReport;

// NOTE - This is used locally rather than running the lambdas to process messages. Polls the one
// shared queue pair for every document type - see DocumentTypeServiceProviders for why each
// document type keeps its own independent IServiceProvider rather than sharing one container.

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

// The two hosted services poll the shared queue pair using AbstractionLicence's own
// FileProcessAppSettings for the queue URLs/polling parameters - the queues are shared, so only
// one settings instance's queue URLs are actually used going forward. Which document type's
// services actually process a given message is decided per-message via
// DocumentTypeServiceProviders, independently of this.
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