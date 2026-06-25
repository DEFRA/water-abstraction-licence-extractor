using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IOrchestratorService
{
    Task AddToFileProcessQueue(SingleFileProcessRequest singleFileProcessRequest);
}