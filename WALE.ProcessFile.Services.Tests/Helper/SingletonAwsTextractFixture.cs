using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.AwsTextract;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Output;

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

    public readonly Task<HashSet<string>> FirstNamesCsvTask = CompanyName.GetFirstNamesCsvFromFileAsync();
    
    public void Dispose()
    {
        // Cleanup resources
    }
}