using System.Text.Json;
using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WRADI.DocumentType.WrInspectionReport.Interfaces;
using WRADI.DocumentType.WrInspectionReport.Models;
using WRADI.DocumentType.WrInspectionReport.Services;

namespace WRADI.Database.PostgreSQL.WrInspectionReport.Services;

/// Reads matches_result directly rather than through IOutputService: a run is tens of thousands of
/// rows, there is no bulk matches-result read on that interface, and per-file fetches over the API
/// path share a 50-request-a-second limiter with every other caller.
public class PostgresWrInspectionReportProjectionService(
    INpgsqlDataSourceProvider dataSourceProvider,
    IWrInspectionReportSummaryWriteService writeService)
    : IWrInspectionReportProjectionService
{
    public async Task<WrInspectionReportProjectionOutcome> ProjectProcessRunAsync(
        int processRunId,
        CancellationToken cancellationToken = default)
    {
        var discovery = await GetDiscoveryByFileIdAsync(dataSourceProvider.DataSource);

        var summaries = new List<WrInspectionReportSummary>();
        var skipped = 0;

        await foreach (var (fileId, matchesResultId, data) in
            ReadMatchesResultsAsync(dataSourceProvider.DataSource, processRunId, cancellationToken))
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
                    discovered?.PermitNumber,
                    (WrInspectionReportSummaryBuilder.ParseDmsDate(discovered?.DocumentDate),
                        WrInspectionReportSummaryBuilder.ParseDmsDate(discovered?.UploadDate))));
        }

        var written = await writeService.ReplaceForProcessRunAsync(processRunId, summaries);

        return new WrInspectionReportProjectionOutcome(written, skipped);
    }

    /// Only Error rows are excluded - a row with no extracted content is still a real result, and
    /// showing it with a completeness of zero is the point of the list.
    private static async IAsyncEnumerable<(Guid FileId, int MatchesResultId, string Data)> ReadMatchesResultsAsync(
        NpgsqlDataSource dataSource,
        int processRunId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection = dataSource.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT file_id, matches_result_id, data
                              FROM matches_result
                              WHERE process_run_id = @processRunId
                                AND file_id IS NOT NULL
                                AND COALESCE(status, '') <> 'Error'
                              """;
        command.Parameters.AddWithValue("processRunId", processRunId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            yield return (reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2));
        }
    }

    /// upload_date is selected only when inspection_report_finder_result has the column, which it
    /// does not yet. DMS holds an upload date for every file, including those whose document_date
    /// is blank, so it becomes the only available date for some files once the finder persists it.
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
