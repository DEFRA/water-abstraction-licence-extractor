using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Services;

public class DatabaseOutputService(
    IDatabaseReadService databaseReadService,
    IDatabaseAddService databaseAddService) : IOutputService
{
    public Task SetupAsync()
    {
        // Nothing to do in this case
        return Task.CompletedTask;
    }

    public Task<string> GetPageScreenshotReferenceAsync(int pageNumber, string pdfServiceName,
        string pdfFilePath)
    {
        throw new NotImplementedException();
    }
    
    public Task<ProcessRun> SaveProcessRunAsync(ProcessRun processRun)
    {
        return databaseAddService.AddProcessRunAsync(processRun);
    }

    public Task SaveLicenceSetsAsync(IReadOnlyList<LicenceSet> licenceSets, string pdfFilePath)
    {
        return databaseAddService.SaveLicenceSetsAsync(licenceSets, pdfFilePath);
    }

    public Task SaveLicenceAsync(Licence licence, string pdfFilePath)
    {
        return databaseAddService.SaveLicenceAsync(licence, pdfFilePath);
    }

    public Task SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath)
    {
        return databaseAddService.SaveMatchResultAsync(matchesResult, pdfFilePath);
    }
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData)
    {
        return databaseAddService.SaveListDataAsync(listData);
    }

    public Task SavePageScreenshotIfDoesntExistAsync(PdfDocument pdfDocument, int pageNumber, string pdfServiceName,
        string pdfFilePath)
    {
        throw new NotImplementedException();
    }

    public Task SavePageScreenshotIfDoesntExistAsync(PdfDocument pdfDocument, int pageNumber, string pdfServiceName)
    {
        throw new NotImplementedException();
    }

    public Task SaveAllPagesTextIfDoesntExistAsync(List<DocumentLine> documentLines, string pdfFilePath)
    {
        throw new NotImplementedException();
    }
}