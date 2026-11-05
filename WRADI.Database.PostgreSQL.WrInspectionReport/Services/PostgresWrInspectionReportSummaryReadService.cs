using Dapper;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Interfaces;
using WRADI.DocumentType.WrInspectionReport.Models;

namespace WRADI.Database.PostgreSQL.WrInspectionReport.Services;

public class PostgresWrInspectionReportSummaryReadService(INpgsqlDataSourceProvider dataSourceProvider)
    : IWrInspectionReportSummaryReadService
{
    public async Task<List<WrInspectionReportSummary>> GetLatestPerFileAsync(
        DateOnly? from,
        DateOnly? to,
        string? template,
        int skip,
        int take)
    {
        await using var connection = dataSourceProvider.DataSource.CreateConnection();

        // DISTINCT ON picks the highest process_run_id per file before any date filtering, so a
        // file is never shown under an older run's extraction just because the newest one falls
        // outside the window.
        const string sql = """
                           WITH latest AS (
                               SELECT DISTINCT ON (file_id)
                                   process_run_id, file_id, matches_result_id, inspection_date,
                                   form_date, dms_document_date, dms_upload_date, sort_date,
                                   date_source, date_warning, permit_number, template,
                                   completeness, is_scan
                               FROM wr_inspection_report_summary
                               ORDER BY file_id, process_run_id DESC
                           )
                           SELECT process_run_id AS ProcessRunId,
                                  file_id AS FileId,
                                  matches_result_id AS MatchesResultId,
                                  inspection_date AS InspectionDate,
                                  form_date AS FormDate,
                                  dms_document_date AS DmsDocumentDate,
                                  dms_upload_date AS DmsUploadDate,
                                  sort_date AS SortDate,
                                  date_source AS DateSourceText,
                                  date_warning AS DateWarningText,
                                  permit_number AS PermitNumber,
                                  template AS Template,
                                  completeness AS Completeness,
                                  is_scan AS IsScan
                           FROM latest
                           WHERE (@From::date IS NULL OR sort_date >= @From::date)
                             AND (@To::date IS NULL OR sort_date <= @To::date)
                             AND (@Template IS NULL OR template = @Template)
                             AND (sort_date IS NOT NULL OR (@From IS NULL AND @To IS NULL))
                           ORDER BY sort_date DESC NULLS LAST, permit_number
                           OFFSET @Skip LIMIT @Take;
                           """;

        // Dapper cannot bind DateOnly as a parameter, so the bounds cross as DateTime and the SQL
        // casts them back to date. The interface stays DateOnly, which is what the column holds.
        var rows = await connection.QueryAsync<SummaryRow>(
            sql,
            new
            {
                From = from?.ToDateTime(TimeOnly.MinValue),
                To = to?.ToDateTime(TimeOnly.MinValue),
                Template = template,
                Skip = skip,
                Take = take
            });

        return rows.Select(row => row.ToSummary()).ToList();
    }

    public async Task<List<WrInspectionReportSummary>> GetForProcessRunAsync(int processRunId)
    {
        await using var connection = dataSourceProvider.DataSource.CreateConnection();

        const string sql = """
                           SELECT process_run_id AS ProcessRunId,
                                  file_id AS FileId,
                                  matches_result_id AS MatchesResultId,
                                  inspection_date AS InspectionDate,
                                  form_date AS FormDate,
                                  dms_document_date AS DmsDocumentDate,
                                  dms_upload_date AS DmsUploadDate,
                                  sort_date AS SortDate,
                                  date_source AS DateSourceText,
                                  date_warning AS DateWarningText,
                                  permit_number AS PermitNumber,
                                  template AS Template,
                                  completeness AS Completeness,
                                  is_scan AS IsScan
                           FROM wr_inspection_report_summary
                           WHERE process_run_id = @ProcessRunId;
                           """;

        var rows = await connection.QueryAsync<SummaryRow>(sql, new { ProcessRunId = processRunId });

        return rows.Select(row => row.ToSummary()).ToList();
    }

    public async Task<int> CountForProcessRunAsync(int processRunId)
    {
        await using var connection = dataSourceProvider.DataSource.CreateConnection();

        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM wr_inspection_report_summary WHERE process_run_id = @ProcessRunId",
            new { ProcessRunId = processRunId });
    }

    /// date_source is stored as its name, so it is read as text and parsed rather than mapped by
    /// ordinal - an inserted value that no longer matches an enum member reads back as None.
    private class SummaryRow
    {
        public int ProcessRunId { get; set; }
        public Guid FileId { get; set; }
        public int MatchesResultId { get; set; }
        public DateOnly? InspectionDate { get; set; }
        public DateOnly? FormDate { get; set; }
        public DateOnly? DmsDocumentDate { get; set; }
        public DateOnly? DmsUploadDate { get; set; }
        public DateOnly? SortDate { get; set; }
        public string? DateSourceText { get; set; }
        public string? DateWarningText { get; set; }
        public string? PermitNumber { get; set; }
        public string? Template { get; set; }
        public int Completeness { get; set; }
        public bool IsScan { get; set; }

        public WrInspectionReportSummary ToSummary() => new()
        {
            ProcessRunId = ProcessRunId,
            FileId = FileId,
            MatchesResultId = MatchesResultId,
            InspectionDate = InspectionDate,
            FormDate = FormDate,
            DmsDocumentDate = DmsDocumentDate,
            DmsUploadDate = DmsUploadDate,
            SortDate = SortDate,
            DateSource = Enum.TryParse<WrInspectionReportDateSource>(DateSourceText, out var source)
                ? source
                : WrInspectionReportDateSource.None,
            DateWarning = Enum.TryParse<WrInspectionReportDateWarning>(DateWarningText, out var warning)
                ? warning
                : WrInspectionReportDateWarning.None,
            PermitNumber = PermitNumber,
            Template = Template,
            Completeness = Completeness,
            IsScan = IsScan
        };
    }
}
