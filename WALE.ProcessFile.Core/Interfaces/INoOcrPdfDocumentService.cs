namespace WALE.ProcessFile.Core.Interfaces;

public interface INoOcrPdfDocumentService
{
    public IInternalPdfDocument GetPdfDocument(string pdfFilepath);
    
    string? Name { get; set; }
}