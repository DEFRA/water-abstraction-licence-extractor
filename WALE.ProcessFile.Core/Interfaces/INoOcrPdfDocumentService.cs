namespace WALE.ProcessFile.Core.Interfaces;

public interface INoOcrPdfDocumentService
{
    public IInternalPdfDocument GetPdfDocument(string pdfFilename);
    
    string? Name { get; set; }
}