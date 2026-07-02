namespace WALE.ProcessFile.Core.Interfaces;

public interface IFileProcessOrchestrator
{
    public Task<bool> RunAsync(CancellationToken cancellationToken);
}