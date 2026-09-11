using WRADI.Services.Cache.WrInspectionReport.Models;

namespace WRADI.Services.Cache.WrInspectionReport.Interfaces;

public interface IInspectionReportFinderCacheService
{
    Task<List<InspectionReportFinderResult>> GetInspectionReportFinderResultsAsync(int skip, int take);

    Task SaveInspectionReportFinderResultsAsync(List<InspectionReportFinderResult> results);

    Task ClearInspectionReportFinderResultsAsync();
}
