using Microsoft.Extensions.DependencyInjection;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.AwsS3;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAwsS3Services(
        this IServiceCollection services,
        string s3RegionName,
        string s3IngressBucketName,
        string s3AssetsBucketName,
        string? s3AccessKey,
        string? s3SecretKey,
        string? s3SessionToken)
    {
        services.AddTransient<IFileService>(_ => new AwsS3FileService(
            s3RegionName,
            s3IngressBucketName,
            s3AssetsBucketName,
            s3AccessKey,
            s3SecretKey,
            s3SessionToken));
        
        return services;
    }
}