using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface ITableExtractorService
{
    public Task<IReadOnlyList<DocumentTable>> GetTablesAsync(
        PdfDocument pdfDocument,
        Guid fileId,
        int processRunId);

    public string Name { get; }
}