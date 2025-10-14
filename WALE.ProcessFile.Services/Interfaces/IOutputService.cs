using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Interfaces;

public interface IOutputService
{
    public Task SetupAsync();
    
    public Task<string> GetPageScreenshotReferenceAsync(
        int pageNumber,
        string pdfServiceName,
        string pdfFilePath);
    
    public Task<ProcessRun> SaveProcessRunAsync(ProcessRun processRun);

    public Task SaveLicenceSetsAsync(IReadOnlyList<LicenceSet> licenceSets, string pdfFilePath);
    
    public Task SaveLicenceAsync(Licence licence, string pdfFilePath);
    
    public Task SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath);
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData);
    
    public Task SavePageScreenshotIfDoesntExistAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        string pdfFilePath);

    public Task SaveAllPagesTextIfDoesntExistAsync(List<DocumentLine> documentLines, string pdfFilePath, string noOcrServiceName);
    
    Task FinishProcessRunAsync(ProcessRun processRun);
}