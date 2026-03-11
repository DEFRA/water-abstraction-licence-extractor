namespace WALE.ProcessFile.Core.Interfaces;

public interface INoOcrPdfDocumentService
{
    public IInternalPdfDocument GetPdfDocument(string filepath);
    
    string? Name { get; set; }
}