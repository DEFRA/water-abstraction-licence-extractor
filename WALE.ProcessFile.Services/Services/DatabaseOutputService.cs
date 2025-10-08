using WALE.ProcessFile.Models.Database;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Services;

public class DatabaseOutputService : IOutputService
{
    public Task SetupAsync()
    {
        throw new NotImplementedException();
    }

    public Task<ProcessRun> RecordProcessRunStartAsync()
    {
        throw new NotImplementedException();
    }

    public Task SaveLicenceSetsAsync(IReadOnlyList<LicenceSet> licenceSets, string pdfFilePath)
    {
        throw new NotImplementedException();
    }

    public Task SaveLicenceAsync(Licence licence, string pdfFilePath)
    {
        throw new NotImplementedException();
    }

    public Task SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath)
    {
        throw new NotImplementedException();
    }
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData)
    {
        throw new NotImplementedException();
    }
}