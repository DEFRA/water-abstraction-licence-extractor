using WALE.ProcessFile.Models.Database;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Interfaces;

public interface IOutputService
{
    public Task SetupAsync();
    
    public Task<ProcessRun> RecordProcessRunStartAsync();

    public Task SaveLicenceSetsAsync(IReadOnlyList<LicenceSet> licenceSets, string pdfFilePath);
    
    public Task SaveLicenceAsync(Licence licence, string pdfFilePath);
    
    public Task SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath);
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData);
}