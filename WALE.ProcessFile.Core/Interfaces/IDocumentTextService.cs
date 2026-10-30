namespace WALE.ProcessFile.Core.Interfaces;

public interface IDocumentTextService
{
    Task<string?> GetDocumentTextAsync(Guid fileId);

    Task StoreDocumentTextAsync(Guid fileId);
}
