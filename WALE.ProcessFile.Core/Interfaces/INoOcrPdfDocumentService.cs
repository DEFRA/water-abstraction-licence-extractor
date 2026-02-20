namespace WALE.ProcessFile.Core.Interfaces;

public interface INoOcrPdfDocumentService
{
    public IInternalPdfDocument GetPdfDocument(string pdfFilePath);
    
    string? Name { get; set; }
}