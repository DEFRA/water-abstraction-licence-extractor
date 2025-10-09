using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;

namespace WALE.ProcessFile.Database.Interfaces;

public interface IDatabaseAddService
{
    public Task<ProcessRun> AddProcessRunAsync(ProcessRun processRun);
    
    public Task SaveLicenceSetsAsync(IReadOnlyList<LicenceSet> licenceSets, string pdfFilePath);
    
    public Task SaveLicenceAsync(Licence licence, string pdfFilePath);
    
    public Task SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath);
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData);
}