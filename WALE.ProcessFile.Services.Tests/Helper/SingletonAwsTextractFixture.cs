using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.AwsTextract;
using WALE.ProcessFile.Services.Services;

namespace WALE.ProcessFile.Services.Tests.Helper;

public class SingletonAwsTextractFixture : IDisposable
{
    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    
    public AwsTextractOcrDataExtractorService Instance =>
        AwsTextractOcrDataExtractorService.Instance(
            TestConfig.AwsAccessKey,
            TestConfig.AwsSecretKey,
            CacheService,
            OutputService);

    public void Dispose()
    {
        // Cleanup resources
    }
}