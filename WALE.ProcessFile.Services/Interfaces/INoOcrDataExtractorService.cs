using WALE.ProcessFile.Services.Models;
using PdfDocument = WALE.ProcessFile.Services.Models.PdfDocument;

namespace WALE.ProcessFile.Services.Interfaces;

public interface INoOcrDataExtractorService
{
    public Task<PdfDocument> GetPdfDocumentAsync(string pdfFilePath, string outputFolder, bool useCache);
    
    public Task<List<DocumentLine>>
        GetTextLinesFromPdfAsync(PdfDocument pdfDocument);

    public Task<IReadOnlyList<INoOcrPdfPageService>>
        GetPagesThatContainImagesAsync(PdfDocument pdfDocument, string pdfFilePath);

    public Task<string> SavePageScreenshotAsync(PdfDocument pdfDocument, int pageNumber);    
    
    public void Release(PdfDocument pdfDocument);
    
    public string Name { get; }
}