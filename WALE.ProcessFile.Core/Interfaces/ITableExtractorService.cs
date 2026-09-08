using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface ITableExtractorService
{
    public Task<IReadOnlyList<OcrTable>> GetTablesAsync(
        byte[] documentBytes,
        Guid fileId,
        int processRunId);

    public string Name { get; }
}
