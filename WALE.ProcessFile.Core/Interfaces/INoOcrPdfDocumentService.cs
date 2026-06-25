namespace WALE.ProcessFile.Core.Interfaces;

public interface INoOcrPdfDocumentService
{
    public Task<IInternalPdfDocument?> GetPdfDocumentAsync(IFileService fileService, string filename);
    
    string? Name { get; set; }
}