using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Tests.Helper;

public class SingletonFirstNamesFixture : IDisposable
{
    public readonly HashSet<string> FirstNamesCsv = CompanyName.GetFirstNamesCsvFromFile();
    
    public void Dispose()
    {
        // Cleanup resources
    }
}