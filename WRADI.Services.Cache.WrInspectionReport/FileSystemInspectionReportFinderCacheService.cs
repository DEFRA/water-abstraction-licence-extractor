using WRADI.Services.Cache.WrInspectionReport.Interfaces;
using WRADI.Services.Cache.WrInspectionReport.Models;

namespace WRADI.Services.Cache.WrInspectionReport;

public class FileSystemInspectionReportFinderCacheService : IInspectionReportFinderCacheService
{
    public Task<List<InspectionReportFinderResult>> GetInspectionReportFinderResultsAsync(int skip, int take)
    {
        return Task.FromResult(new List<InspectionReportFinderResult>());
    }

    public Task SaveInspectionReportFinderResultsAsync(List<InspectionReportFinderResult> results)
    {
        return Task.CompletedTask;
    }

    public Task ClearInspectionReportFinderResultsAsync()
    {
        return Task.CompletedTask;
    }
}
