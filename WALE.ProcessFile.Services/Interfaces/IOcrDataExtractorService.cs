using WALE.ProcessFile.Models;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Interfaces;

public interface IOcrDataExtractorService
{
    public Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(
            string imageReference,
            string pdfFilepath,
            int pageNumber,
            int imageNumber,
            PdfDocument pdfDocument,
            int processRunId);
    
    public bool HasDirectCost { get; }
    
    public string Name { get; }

    public void Dispose();
}