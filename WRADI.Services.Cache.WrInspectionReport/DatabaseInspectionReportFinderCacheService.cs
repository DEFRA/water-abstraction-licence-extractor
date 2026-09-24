using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WRADI.Services.Cache.WrInspectionReport.Interfaces;
using WRADI.Services.Cache.WrInspectionReport.Models;

namespace WRADI.Services.Cache.WrInspectionReport;

public class DatabaseInspectionReportFinderCacheService(INpgsqlDataSourceProvider dataSourceProvider)
    : IInspectionReportFinderCacheService
{
    public async Task<List<InspectionReportFinderResult>> GetInspectionReportFinderResultsAsync(int skip, int take)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               permit_number,
                               file_url,
                               file_name,
                               library_name,
                               regime,
                               file_size,
                               file_id,
                               document_date,
                               other_reference,
                               disclosure_status,
                               process_run_id
                           FROM public.inspection_report_finder_result
                           ORDER BY
                               permit_number,
                               file_url
                           LIMIT @take
                           OFFSET @skip;
                           """;

        var results = await QueryAsync<InspectionReportFinderResult>(
            connection,
            sql,
            0,
            new
            {
                Skip = skip,
                Take = take
            });

        return results.ToList();
    }

    public async Task SaveInspectionReportFinderResultsAsync(List<InspectionReportFinderResult> results)
    {
        foreach (var result in results)
        {
            await SaveInspectionReportFinderResultAsync(result);
        }
    }

    private async Task SaveInspectionReportFinderResultAsync(InspectionReportFinderResult result)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO inspection_report_finder_result (
                                    permit_number,
                                    file_url,
                                    file_name,
                                    library_name,
                                    regime,
                                    file_size,
                                    file_id,
                                    document_date,
                                    other_reference,
                                    disclosure_status,
                                    process_run_id)
                               VALUES (
                                    @PermitNumber,
                                    @FileUrl,
                                    @FileName,
                                    @LibraryName,
                                    @Regime,
                                    @FileSize,
                                    @FileId,
                                    @DocumentDate,
                                    @OtherReference,
                                    @DisclosureStatus,
                                    @ProcessRunId);
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                result.PermitNumber,
                result.FileUrl,
                result.FileName,
                result.LibraryName,
                result.Regime,
                result.FileSize,
                result.FileId,
                result.DocumentDate,
                result.OtherReference,
                result.DisclosureStatus,
                result.ProcessRunId
            });
    }

    public async Task ClearInspectionReportFinderResultsAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           DELETE FROM inspection_report_finder_result;
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0);
    }

    private async Task<IEnumerable<T>> QueryAsync<T>(NpgsqlConnection connection, string sql, int retryNumber,
        object? param = null)
    {
        try
        {
            var dtStart = DateTime.Now;
            var thisQueryNumber = NpgsqlDataSourceProvider.QueryNumber++;

            if (NpgsqlDataSourceProvider.AddDebugLogging)
            {
                NpgsqlDataSourceProvider.Queries.Add((thisQueryNumber, sql));
            }

            var result = await connection.QueryAsync<T>(sql, param);
            var duration = DateTime.Now - dtStart;

            if (duration.TotalSeconds > 1)
            {
                ConsoleHelper.WriteLine(
                    $"WARNING - {nameof(DatabaseInspectionReportFinderCacheService)} - Query {thisQueryNumber} - {sql.Replace("\n", " ")} took {duration.TotalMilliseconds}ms");
            }

            return result;
        }
        catch (NpgsqlException ex)
        {
            if (ex.InnerException is not EndOfStreamException)
            {
                throw;
            }

            if (retryNumber > RetryHelper.MaxRetries)
            {
                throw;
            }

            ConsoleHelper.WriteLine($"WARNING - {nameof(DatabaseInspectionReportFinderCacheService)} - QueryAsync retrying");

            await RetryHelper.WaitWithMessageAsync(retryNumber, nameof(DatabaseInspectionReportFinderCacheService));
            return await QueryAsync<T>(
                GetPostgresConnection(),
                sql,
                retryNumber + 1,
                param);
        }
    }

    private async Task ExecuteAsync(NpgsqlConnection connection, string sql, int retryNumber, object? param = null)
    {
        try
        {
            var dtStart = DateTime.Now;
            var thisQueryNumber = NpgsqlDataSourceProvider.QueryNumber++;

            if (NpgsqlDataSourceProvider.AddDebugLogging)
            {
                NpgsqlDataSourceProvider.Queries.Add((thisQueryNumber, sql));
            }

            await connection.ExecuteAsync(sql, param);
            var duration = DateTime.Now - dtStart;

            if (duration.TotalSeconds > 1)
            {
                ConsoleHelper.WriteLine($"WARNING - {nameof(DatabaseInspectionReportFinderCacheService)} - Query {thisQueryNumber} - {sql.Replace("\n", " ")} took {duration.TotalMilliseconds}ms");
            }
        }
        catch (NpgsqlException ex)
        {
            if (ex.InnerException is not EndOfStreamException)
            {
                throw;
            }

            if (retryNumber > RetryHelper.MaxRetries)
            {
                throw;
            }

            ConsoleHelper.WriteLine($"WARNING - {nameof(DatabaseInspectionReportFinderCacheService)} - ExecuteAsync retrying");

            await RetryHelper.WaitWithMessageAsync(retryNumber, nameof(DatabaseInspectionReportFinderCacheService));
            await ExecuteAsync(GetPostgresConnection(), sql, retryNumber + 1, param);
        }
    }

    private NpgsqlConnection GetPostgresConnection()
    {
        var dtStart = DateTime.Now;

        var conn = dataSourceProvider.DataSource.CreateConnection();
        var duration = DateTime.Now - dtStart;

        if (duration.TotalSeconds > 1)
        {
            ConsoleHelper.WriteLine(
                $"WARNING - {nameof(DatabaseInspectionReportFinderCacheService)} - CreateConnection took {duration.TotalMilliseconds}ms");
        }

        return conn;
    }
}
