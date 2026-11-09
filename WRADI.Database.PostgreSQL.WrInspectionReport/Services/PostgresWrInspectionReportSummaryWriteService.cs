using Dapper;
using Npgsql;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Interfaces;
using WRADI.DocumentType.WrInspectionReport.Models;

namespace WRADI.Database.PostgreSQL.WrInspectionReport.Services;

public class PostgresWrInspectionReportSummaryWriteService(INpgsqlDataSourceProvider dataSourceProvider)
    : IWrInspectionReportSummaryWriteService
{
    public async Task<int> ReplaceForProcessRunAsync(
        int processRunId,
        IReadOnlyList<WrInspectionReportSummary> summaries)
    {
        await using var connection = dataSourceProvider.DataSource.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await connection.ExecuteAsync(
            "DELETE FROM wr_inspection_report_summary WHERE process_run_id = @ProcessRunId",
            new { ProcessRunId = processRunId },
            transaction);

        if (summaries.Count == 0)
        {
            await transaction.CommitAsync();
            return 0;
        }

        // Binary COPY rather than per-row inserts: a run is tens of thousands of rows and this
        // is rebuilt whenever the extraction changes.
        await using (var writer = await connection.BeginBinaryImportAsync(
            """
            COPY wr_inspection_report_summary (
                process_run_id, file_id, matches_result_id, inspection_date, form_date,
                dms_document_date, dms_upload_date, sort_date, date_source, date_warning,
                permit_number, template, completeness, is_scan, date_time_utc)
            FROM STDIN (FORMAT BINARY)
            """))
        {
            // date_time_utc is a timestamp without time zone, and binary COPY rejects a UTC-kind
            // DateTime for one where a parameterised insert would have coerced it.
            var writtenAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            foreach (var summary in summaries)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(summary.ProcessRunId, NpgsqlTypes.NpgsqlDbType.Integer);
                await writer.WriteAsync(summary.FileId, NpgsqlTypes.NpgsqlDbType.Uuid);
                await writer.WriteAsync(summary.MatchesResultId, NpgsqlTypes.NpgsqlDbType.Integer);

                await WriteNullableDateAsync(writer, summary.InspectionDate);
                await WriteNullableDateAsync(writer, summary.FormDate);
                await WriteNullableDateAsync(writer, summary.DmsDocumentDate);
                await WriteNullableDateAsync(writer, summary.DmsUploadDate);
                await WriteNullableDateAsync(writer, summary.SortDate);
                await writer.WriteAsync(summary.DateSource.ToString(), NpgsqlTypes.NpgsqlDbType.Text);
                await WriteNullableTextAsync(
                    writer,
                    summary.DateWarning == WrInspectionReportDateWarning.None
                        ? null
                        : summary.DateWarning.ToString());
                await WriteNullableTextAsync(writer, summary.PermitNumber);
                await WriteNullableTextAsync(writer, summary.Template);
                await writer.WriteAsync(summary.Completeness, NpgsqlTypes.NpgsqlDbType.Integer);
                await writer.WriteAsync(summary.IsScan, NpgsqlTypes.NpgsqlDbType.Boolean);
                await writer.WriteAsync(writtenAt, NpgsqlTypes.NpgsqlDbType.Timestamp);
            }

            await writer.CompleteAsync();
        }

        await transaction.CommitAsync();

        return summaries.Count;
    }

    private static async Task WriteNullableDateAsync(NpgsqlBinaryImporter writer, DateOnly? value)
    {
        if (value == null)
        {
            await writer.WriteNullAsync();
            return;
        }

        await writer.WriteAsync(value.Value, NpgsqlTypes.NpgsqlDbType.Date);
    }

    private static async Task WriteNullableTextAsync(NpgsqlBinaryImporter writer, string? value)
    {
        if (value == null)
        {
            await writer.WriteNullAsync();
            return;
        }

        await writer.WriteAsync(value, NpgsqlTypes.NpgsqlDbType.Text);
    }
}
