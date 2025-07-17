using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Interfaces;

public interface IOcrDataExtractorService
{
    public Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(byte[] imageData, int pageNumber, int imageNumber, PdfDocument pdfDocument);    
    
    public bool HasDirectCost { get; }
    
    public string Name { get; }

    public void Dispose();
}