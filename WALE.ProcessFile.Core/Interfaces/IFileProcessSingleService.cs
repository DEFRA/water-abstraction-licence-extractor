using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IFileProcessSingleService
{
    Task<bool> RunAsync(
        SingleFileProcessRequest singleFileProcessRequest,
        CancellationToken cancellationToken);
}