using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IFileProcessOrchestrator
{
    public Task<bool> RunAsync(FileProcessOrchestrationRequest request, CancellationToken cancellationToken);
}