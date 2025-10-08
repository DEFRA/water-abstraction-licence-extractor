using WALE.ProcessFile.Models.Database;

namespace WALE.ProcessFile.Database.Interfaces;

public interface IDatabaseReadService
{
    public List<ProcessRun> GetProcessRuns();
}