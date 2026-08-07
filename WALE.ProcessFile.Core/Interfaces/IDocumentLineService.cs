using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IDocumentLineService
{
    public List<DocumentLine> GetDocumentLines(
        int startPageNumber,
        int startLineNumber,
        int endPageNumber,
        int endLineNumber);
}