using Microsoft.Extensions.DependencyInjection;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.AwsS3;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddS3Services(
        this IServiceCollection services,
        string s3RegionName,
        string s3BucketName,
        string? s3AccessKey,
        string? s3SecretKey,
        string? s3SessionToken)
    {
        services.AddTransient<IFileService>(_ => new AwsS3FileService(
            s3RegionName,
            s3BucketName,
            s3AccessKey,
            s3SecretKey,
            s3SessionToken));
        
        return services;
    }
}