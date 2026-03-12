using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.Docnet;

public class DocnetNoOcrAlternativePdfDocumentService : INoOcrAlternativePdfDocumentService
{
    public IAlternativeImageProvider GetAlternativeImageProvider()
    {
        return new DocnetAlternativeImageProvider();
    }
}