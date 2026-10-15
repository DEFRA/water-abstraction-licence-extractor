using System.Text.Json;
using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.Tools.Config;
using WRADI.Database.PostgreSQL.WrInspectionReport.Services;
using WRADI.DocumentType.WrInspectionReport.Models;
using WRADI.DocumentType.WrInspectionReport.Services;

namespace WALE.Tools._2ndHalf;

/// Rebuilds wr_inspection_report_summary for one process run from the matches_result rows it
/// already holds. Pure derived data, so it is safe to re-run, and safe to run against a process
/// run that is still in progress - it projects whatever has landed so far.
///
/// Goes straight to Postgres rather than through the API: a run is tens of thousands of rows, and
/// the API path is capped at 50 requests a second for all callers.
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

        var discovery = await GetDiscoveryByFileIdAsync(dataSource);

        ConsoleHelper.WriteLine($"Loaded {discovery.Count} discovery rows for the permit number "
            + "and the DMS date fallback");

        var summaries = new List<WrInspectionReportSummary>();
        var skipped = 0;

        await foreach (var (fileId, matchesResultId, data) in ReadMatchesResultsAsync(dataSource, processRunId))
        {
            var matchesResult = Deserialize(data);

            if (matchesResult == null)
            {
                skipped++;
                continue;
            }

            matchesResult.MatchesResultId = matchesResultId;
            discovery.TryGetValue(fileId, out var discovered);

            summaries.Add(
                WrInspectionReportSummaryBuilder.Build(
                    matchesResult,
                    fileId,
                    processRunId,
                    discovered.PermitNumber,
                    (WrInspectionReportSummaryBuilder.ParseDmsDate(discovered.DocumentDate),
                        WrInspectionReportSummaryBuilder.ParseDmsDate(discovered.UploadDate))));
        }

        if (skipped > 0)
        {
            ConsoleHelper.WriteLine($"WARNING - {skipped} rows skipped as their data would not deserialize");
        }

        var writeService = new PostgresWrInspectionReportSummaryWriteService(dataSourceProvider);
        var written = await writeService.ReplaceForProcessRunAsync(processRunId, summaries);

        ConsoleHelper.WriteLine($"Wrote {written} summary rows for ProcessRun {processRunId}");

        foreach (var group in summaries.GroupBy(summary => summary.DateSource).OrderBy(group => group.Key))
        {
            ConsoleHelper.WriteLine($"  date source {group.Key}: {group.Count()}");
        }

        // Read back through the same service the list view uses, so a rebuild also proves the
        // projection is queryable rather than just written.
        var readService = new PostgresWrInspectionReportSummaryReadService(dataSourceProvider);
        var newest = await readService.GetLatestPerFileAsync(null, null, null, 0, 3);

        ConsoleHelper.WriteLine($"Read back {await readService.CountForProcessRunAsync(processRunId)} rows "
            + "for this run; newest by inspection date:");

        foreach (var summary in newest)
        {
            ConsoleHelper.WriteLine($"  {summary.InspectionDate?.ToString("yyyy-MM-dd") ?? "no date"} "
                + $"({summary.DateSource}) {summary.PermitNumber} {summary.Template} {summary.Completeness}%");
        }

        return 0;
    }

    /// Only Error rows are excluded - a row with no extracted content is still a real result, and
    /// showing it with a completeness of zero is the point of the list.
    private static async IAsyncEnumerable<(Guid FileId, int MatchesResultId, string Data)> ReadMatchesResultsAsync(
        NpgsqlDataSource dataSource,
        int processRunId)
    {
        await using var connection = dataSource.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT file_id, matches_result_id, data
                              FROM matches_result
                              WHERE process_run_id = @processRunId
                                AND file_id IS NOT NULL
                                AND COALESCE(status, '') <> 'Error'
                              """;
        command.Parameters.AddWithValue("processRunId", processRunId);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            yield return (reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2));
        }
    }

    /// upload_date is selected only when inspection_report_finder_result has the column, which it
    /// does not yet. DMS holds an upload date for every file, including those whose document_date
    /// is blank, so it becomes the only available date for some files once the finder persists it.
    /// The API's own fallback path passes null regardless, so it cannot source that date.
    private static async Task<Dictionary<Guid, DiscoveredFile>> GetDiscoveryByFileIdAsync(
        NpgsqlDataSource dataSource)
    {
        await using var connection = dataSource.CreateConnection();

        var hasUploadDate = await connection.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_name = 'inspection_report_finder_result' AND column_name = 'upload_date')
            """);

        var uploadDateColumn = hasUploadDate ? "upload_date" : "NULL";

        var rows = await connection.QueryAsync<DiscoveredFile>(
            $"""
            SELECT file_id AS FileId,
                   permit_number AS PermitNumber,
                   document_date AS DocumentDate,
                   {uploadDateColumn} AS UploadDate
            FROM public.inspection_report_finder_result
            WHERE file_id IS NOT NULL
            """);

        if (!hasUploadDate)
        {
            ConsoleHelper.WriteLine(
                "WARNING - inspection_report_finder_result has no upload_date column, so files "
                + "with no DMS document date have no date at all");
        }

        var discovery = new Dictionary<Guid, DiscoveredFile>();

        foreach (var row in rows)
        {
            if (Guid.TryParse(row.FileId, out var fileId))
            {
                discovery[fileId] = row;
            }
        }

        return discovery;
    }

    private class DiscoveredFile
    {
        public string? FileId { get; set; }
        public string? PermitNumber { get; set; }
        public string? DocumentDate { get; set; }
        public string? UploadDate { get; set; }
    }

    private static MatchesResult? Deserialize(string data)
    {
        try
        {
            return JsonSerializer.Deserialize<MatchesResult>(data, JsonHelper.GetSerializerOptions());
        }
        catch (JsonException)
        {
            return null;
        }
    }

}
