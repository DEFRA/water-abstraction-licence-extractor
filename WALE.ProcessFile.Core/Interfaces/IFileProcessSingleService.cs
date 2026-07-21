using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IFileProcessSingleService
{
    Task<bool> RunAsync(
        FileProcessSingleRequest fileProcessSingleRequest,
        CancellationToken cancellationToken);
}