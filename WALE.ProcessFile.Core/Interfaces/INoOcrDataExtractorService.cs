using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface INoOcrDataExtractorService
{
    public Task<PdfDocument> GetPdfDocumentAsync(
        string pdfFilePath,
        IOutputService outputService,
        ICacheService cacheService,
        INoOcrPdfDocumentService noOcrPdfDocumentService,
        int processRunId);
    
    public Task<List<DocumentLine>>
        GetTextLinesFromPdfAndSaveScreenshotsPageTextLinesAndMetadataAsync(
            PdfDocument pdfDocument,
            ICacheService cacheService,
            IOutputService outputService,
            int processRunId);

    public Task SavePageScreenshotAsync(
        IOutputService outputService,
        PdfDocument pdfDocument,
        int pageNumber,
        string pdfServiceName,
        int processRunId);
    
    public void Release(PdfDocument pdfDocument);
    
    public string Name { get; }
}