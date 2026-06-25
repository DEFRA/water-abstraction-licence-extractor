namespace WALE.ProcessFile.Core.Interfaces;

public interface IOrchestrateFileProcess
{
    public Task<bool> RunAsync(CancellationToken cancellationToken = default);
}