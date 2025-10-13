using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;

namespace WALE.ProcessFile.Database.Services;

public class SqlSeverAddServiceService(string connectionString) : IDatabaseAddService
{
    public Task<ProcessRun> AddProcessRunAsync(ProcessRun processRun)
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