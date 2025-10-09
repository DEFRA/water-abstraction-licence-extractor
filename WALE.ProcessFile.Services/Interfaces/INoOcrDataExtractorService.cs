using WALE.ProcessFile.Models;
using WALE.ProcessFile.Services.Models;
using PdfDocument = WALE.ProcessFile.Services.Models.PdfDocument;

namespace WALE.ProcessFile.Services.Interfaces;

public interface INoOcrDataExtractorService
{
    public Task<PdfDocument> GetPdfDocumentAsync(
        string pdfFilePath,
        IOutputService outputService,
        ICacheService cacheService);
    
    public Task<List<DocumentLine>>
        GetTextLinesFromPdfAsync(
            PdfDocument pdfDocument,
            ICacheService cacheService);

    public (string imgFolder, string imgOutputFilename) GetPageScreenshotPath(
        IOutputService outputService,
        int pageNumber,
        string pdfServiceName);

    public Task SavePageScreenshotAsync(
        IOutputService outputService,
        PdfDocument pdfDocument,
        int pageNumber,
        string pdfServiceName);
    
    public void Release(PdfDocument pdfDocument);
    
    public string Name { get; }
}