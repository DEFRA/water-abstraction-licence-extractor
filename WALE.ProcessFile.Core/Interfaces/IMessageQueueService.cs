using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IMessageQueueService
{
    Task AddToFileProcessQueue(FileProcessSingleRequest fileProcessSingleRequest);
}