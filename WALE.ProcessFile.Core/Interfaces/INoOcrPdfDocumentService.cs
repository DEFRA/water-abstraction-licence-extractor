namespace WALE.ProcessFile.Core.Interfaces;

public interface INoOcrPdfDocumentService
{
    public IInternalPdfDocument GetPdfDocument(IFileService fileService, string filename);
    
    string? Name { get; set; }
}