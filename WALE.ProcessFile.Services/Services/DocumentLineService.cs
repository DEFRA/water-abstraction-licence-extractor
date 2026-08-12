using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Services;

public class DocumentLineService(List<DocumentLine> documentLines) : IDocumentLineService
{
    public List<DocumentLine> GetDocumentLines(
        int startPageNumber,
        int startLineNumber,
        int endPageNumber,
        int endLineNumber)
    {
        return documentLines
            .Where(l => l.PageNumber >= startPageNumber && l.PageNumber <= endPageNumber)
            .Where(l => l.LineNumber >= startLineNumber && l.LineNumber <= endLineNumber)
            .ToList();
    }
}