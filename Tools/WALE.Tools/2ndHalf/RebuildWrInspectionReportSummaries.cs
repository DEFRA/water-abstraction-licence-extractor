using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.Tools.Config;
using WRADI.Database.PostgreSQL.WrInspectionReport.Services;

namespace WALE.Tools._2ndHalf;

/// Manual re-trigger for the projection, which a process run now writes for itself as it
/// completes. Still needed for runs that finished before that, and to reproject after a change to
/// WrInspectionReportSummaryBuilder changes what a row holds.
public static class RebuildWrInspectionReportSummaries
{
    public static async Task<int> RebuildAsync(int processRunId)
    {
        ConsoleHelper.WriteLine($"Rebuilding WR51 summaries for ProcessRun {processRunId} "
            + $"on {KeyConfig.PostgresHost}...");

        NpgsqlDataSourceProvider dataSourceProvider = new(
            KeyConfig.PostgresHost,
            KeyConfig.PostgresPort,
            KeyConfig.PostgresDbName,
            KeyConfig.PostgresUsername,
            KeyConfig.PostgresPassword);

        await using var dataSource = dataSourceProvider.DataSource;

        var projectionService = new PostgresWrInspectionReportProjectionService(
            dataSourceProvider,
            new PostgresWrInspectionReportSummaryWriteService(dataSourceProvider));

        var outcome = await projectionService.ProjectProcessRunAsync(processRunId);

        ConsoleHelper.WriteLine($"Wrote {outcome.Written} summary rows for ProcessRun {processRunId}");

        if (outcome.SkippedUndeserializable > 0)
        {
            ConsoleHelper.WriteLine(
                $"WARNING - {outcome.SkippedUndeserializable} rows skipped as their data would not deserialize");
        }

        // Read back through the same service the list view uses, so a rebuild also proves the
        // projection is queryable rather than just written.
        var readService = new PostgresWrInspectionReportSummaryReadService(dataSourceProvider);

        ConsoleHelper.WriteLine($"Read back {await readService.CountForProcessRunAsync(processRunId)} rows "
            + "for this run; newest by inspection date:");

        foreach (var summary in await readService.GetLatestPerFileAsync(null, null, null, 0, 3))
        {
            ConsoleHelper.WriteLine($"  {summary.InspectionDate?.ToString("yyyy-MM-dd") ?? "no date"} "
                + $"({summary.DateSource}) {summary.PermitNumber} {summary.Template} {summary.Completeness}%");
        }

        return 0;
    }
}
