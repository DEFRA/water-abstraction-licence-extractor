using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.AwsSqs;
using WALE.ProcessFile.Services.Helpers;
using WRADI.Services.LicenceFinder.Implementations;
using WRADI.Services.ProcessFile.AbstractionLicence;

namespace WRADI.Services.LicenceFinder;

public static class LicenceFinderServiceExtensions
{
    public static IServiceCollection AddLicenceFinderApplication(this IServiceCollection services)
    {
        // Existing registration extension from your solution.
        services.AddLicenceFinderServices();
        services.AddScoped<ILicenceFinderService, LicenceFinderService>();
        return services;
    }

public static IServiceCollection AddLicenceFinderApplicationLocalHosted(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.Configure<FileProcessAppSettings>(options =>
    {
        // AWS general
        options.AwsRegionName = ConfigHelper.GetRequiredString(configuration, "AwsRegionName");

        // Optional S3 support
        options.AwsAccessKey = configuration["AwsAccessKey"];
        options.AwsSecretKey = configuration["AwsSecretKey"];
        options.AwsSessionToken = configuration["AwsSessionToken"];
        options.AwsS3IngressBucketName = configuration["AwsS3BucketName"];

        // SQS
        options.SqsQueueOrchestrationUrl =
            ConfigHelper.GetRequiredString(configuration, "SqsQueueOrchestrationUrl");

        options.SqsQueueFileProcessUrl =
            ConfigHelper.GetRequiredString(configuration, "SqsQueueFileProcessUrl");
        
        options.SqSQueueLicenceFinderUrl =
            ConfigHelper.GetRequiredString(configuration, "SqSQueueLicenceFinderUrl");

        options.SqsWaitTimeSeconds =
            ConfigHelper.GetOptionalInt(configuration, "SqsWaitTimeSeconds") ?? 20;

        options.SqsMaxNumberOfMessages =
            ConfigHelper.GetOptionalInt(configuration, "SqsMaxNumberOfMessages") ?? 10;

        options.SqsVisibilityTimeoutSeconds =
            ConfigHelper.GetOptionalInt(configuration, "SqsVisibilityTimeoutSeconds");

        options.MaxFilesPerRun =
            ConfigHelper.GetOptionalInt(configuration, "MaxFilesPerRun");
    });

    services.AddSingleton(sp =>
        sp.GetRequiredService<IOptions<FileProcessAppSettings>>().Value);
    
    // Existing application services
    services.AddLicenceFinderServices();
    services.AddScoped<ILicenceFinderService, LicenceFinderService>();

    // AWS SQS client
    services.AddSingleton<IAmazonSQS>(sp =>
    {
        var settings = sp
            .GetRequiredService<IOptions<FileProcessAppSettings>>()
            .Value;

        return AwsSqsHelper.GetAwsSqsClient(
            settings.AwsRegionName!,
            settings.AwsAccessKey,
            settings.AwsSecretKey,
            settings.AwsSessionToken);
    });

    return services;
}
}
