using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Tests.Helper;

public class SingletonFirstNamesFixture : IDisposable
{
    public readonly Task<HashSet<string>> FirstNamesCsvTask = CompanyName.GetFirstNamesCsvFromFileAsync();
    
    public void Dispose()
    {
        // Cleanup resources
    }
}