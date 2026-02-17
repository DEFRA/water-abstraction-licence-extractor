using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Tests.Helper;

public class SingletonFirstNamesFixture : IDisposable
{
    private Task<HashSet<string>>? _firstNamesCsvTask;
    
    public Task<HashSet<string>> FirstNamesCsvTask()
    {
        return _firstNamesCsvTask ??= CompanyName.GetFirstNamesCsvFromFileAsync();
    }
    
    public void Dispose()
    {
        // Cleanup resources
    }
}