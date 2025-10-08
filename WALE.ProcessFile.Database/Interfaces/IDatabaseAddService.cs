using WALE.ProcessFile.Models.Database;

namespace WALE.ProcessFile.Database.Interfaces;

public interface IDatabaseAddService
{
    public Task AddProcessRunAsync(ProcessRun processRun);
}