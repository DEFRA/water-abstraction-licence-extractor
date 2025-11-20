namespace WALE.ProcessFile.Models.Interfaces;

public interface INoOcrDataExtractorService
{
    public Task<PdfDocument> GetPdfDocumentAsync(
        string pdfFilePath,
        IOutputService outputService,
        ICacheService cacheService,
        int processRunId);
    
    public Task<List<DocumentLine>>
        GetTextLinesFromPdfAsync(
            PdfDocument pdfDocument,
            ICacheService cacheService,
            int processRunId);

    public Task SavePageScreenshotIfDoesntExistAsync(
        IOutputService outputService,
        PdfDocument pdfDocument,
        int pageNumber,
        string pdfServiceName,
        int processRunId);
    
    public void Release(PdfDocument pdfDocument);
    
    public string Name { get; }
}