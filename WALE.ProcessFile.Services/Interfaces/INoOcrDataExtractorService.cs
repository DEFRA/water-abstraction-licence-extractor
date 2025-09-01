using WALE.ProcessFile.Services.Models;
using PdfDocument = WALE.ProcessFile.Services.Models.PdfDocument;

namespace WALE.ProcessFile.Services.Interfaces;

public interface INoOcrDataExtractorService
{
    public Task<PdfDocument> GetPdfDocumentAsync(string pdfFilePath, string outputFolder, string cacheFolder);
    
    public Task<List<DocumentLine>>
        GetTextLinesFromPdfAsync(PdfDocument pdfDocument);

    public (string imgFolder, string imgOutputFilename) GetPageScreenshotPath(PdfDocument pdfDocument, int pageNumber);
    
    public Task SavePageScreenshotAsync(PdfDocument pdfDocument, int pageNumber);    
    
    public void Release(PdfDocument pdfDocument);
    
    public string Name { get; }
}