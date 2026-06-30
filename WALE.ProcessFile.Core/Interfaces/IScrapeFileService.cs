using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IScrapeFileService
{
    Task<bool> RunAsync(
        SingleFileProcessRequest singleFileProcessRequest,
        CancellationToken cancellationToken = default);
}