using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Database.PostgreSQL.AbstractionLicence.Services;

public class PostgresAbstractionLicenceWriteService(INpgsqlDataSourceProvider dataSourceProvider)
    : IAbstractionLicenceDatabaseWriteService
{
    public async Task<int> SaveLicenceSetAsync(string licenceSetId, string shortLicenceSetId, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO licence_set (schema_licence_set_id, short_licence_set_id, process_run_id, date_time_utc) 
                           VALUES (@SchemaLicenceSetId, @ShortLicenceSetId, @ProcessRunId, @DateTimeUtc)
                           RETURNING licence_set_id
                           """;

        return await ExecuteScalarAsync(
            connection,
            sql,
            0,
            new {
                SchemaLicenceSetId = licenceSetId,
                ShortLicenceSetId = shortLicenceSetId,
                ProcessRunId = processRunId,
                DateTimeUtc = DateTime.UtcNow
            });
    }

    public async Task UpdateLicenceAsync(int licenceId, string licenceData, Guid fileId, int processRunId, string status)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           UPDATE licence
                           SET
                               file_id = @FileId
                               , status = @Status
                               , data = @Data
                           WHERE
                                licence_id = @LicenceId
                                AND process_run_id = @ProcessRunId
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                FileId = fileId,
                LicenceId = licenceId,
                Data = licenceData,
                ProcessRunId = processRunId,
                Status = status
            });
    }

    public async Task<int> SaveLicenceAsync(
        string? licenceNumber,
        string? filename,
        string status,
        string licenceData,
        Guid? fileId,
        string? permitNumber,
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO licence (file_id, licence_number, filename, status, data, process_run_id, permit_number, date_time_utc)
                           VALUES (@FileId, @LicenceNumber, @filename, @Status, @Data, @ProcessRunId, @PermitNumber, @DateTimeUtc)
                           RETURNING licence_id
                           """;

        return await ExecuteScalarAsync(
            connection,
            sql,
            0,
            new {
                FileId = fileId,
                LicenceNumber = licenceNumber,
                Filename = filename,
                Status = status,
                Data = licenceData,
                ProcessRunId = processRunId,
                PermitNumber = permitNumber,
                DateTimeUtc = DateTime.UtcNow
            });
    }

    public async Task UpdateLicenceSetLicenceAsync(LicenceSetLicence licenceSetLicence)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           UPDATE licence_set_licence 
                           SET licence_id = @LicenceId 
                           WHERE licence_set_id = @LicenceSetId 
                             AND licence_number = @LicenceNumber 
                             AND process_run_id = @ProcessRunId
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                licenceSetLicence.LicenceSetId,
                licenceSetLicence.LicenceId,
                licenceSetLicence.LicenceNumber,
                licenceSetLicence.ProcessRunId
            });
    }

    public async Task InsertLicenceSetLicenceAsync(int licenceSetId, int? licenceId, string? licenceNumber,
        string licenceVersionId,
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO licence_set_licence (licence_set_id, licence_id, licence_number, licence_version_id, process_run_id, date_time_utc) 
                           VALUES (@LicenceSetId, @LicenceId, @LicenceNumber, @LicenceVersionId, @ProcessRunId, @DateTimeUtc)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                LicenceSetId = licenceSetId,
                LicenceId = licenceId,
                LicenceNumber = licenceNumber ?? "UNKNOWN",
                LicenceVersionId = licenceVersionId,
                ProcessRunId = processRunId,
                DateTimeUtc = DateTime.UtcNow
            });
    }

    public async Task SaveLicenceSetTypeAsync(int licenceSetId, int licenceSetType, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO licence_set_type (licence_set_id, licence_set_type) 
                           VALUES (@LicenceSetId, @LicenceSetType)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                LicenceSetId = licenceSetId,
                LicenceSetType = licenceSetType
            });
    }
    
    public async Task SaveLicenceFinderResultsAsync(List<LicenceFinderResult> results)
    {
        foreach (var result in results)
        {
            await SaveLicenceFinderResultAsync(result);
        }
    }

    public async Task SaveVersionFilesToDownloadAsync(List<VersionFileToDownload> results)
    {
        foreach (var result in results)
        {
            await SaveVersionFileToDownloadAsync(result);
        }
    }

    public async Task SaveVersionFilesAsync(List<VersionFile> results)
    {
        foreach (var result in results)
        {
            await SaveVersionFileAsync(result);
        }
    }
    
    private async Task SaveVersionFileToDownloadAsync(VersionFileToDownload result)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO version_files_to_download (
                                    permit_number,
                                    full_path,
                                    site_path,
                                    library_and_file_path)
                               VALUES (
                                    @PermitNumber,
                                    @FullPath,
                                    @SitePath,
                                    @LibraryAndFilePath)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            result);
    }
    
    private async Task SaveVersionFileAsync(VersionFile result)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO version_files (
                                    permit_number,
                                    full_path,
                                    site_path,
                                    library_and_file_path,
                                    region_id,
                                    file_name,
                                    file_id,
                                    file_size)
                               VALUES (
                                    @PermitNumber,
                                    @FullPath,
                                    @SitePath,
                                    @LibraryAndFilePath,
                                    @RegionId,
                                    @FileName,
                                    @FileId,
                                    @FileSize)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            result);
    }

    public async Task ClearVersionFilesToDownloadAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           DELETE FROM version_files_to_download;
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0);
    }
    
    public async Task ClearVersionFilesAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           DELETE FROM version_files;
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0);
    }
    
    public async Task ClearLicenceFinderResultsAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           DELETE FROM licence_finder_result;
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0);
    }
    
    private async Task SaveLicenceFinderResultAsync(LicenceFinderResult result)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO licence_finder_result (
                                    permit_number,
                                    dms_permit_number,
                                    file_url,
                                    rule_used,
                                    change_audit_action,
                                    license_number,
                                    document_date,
                                    signature_date,
                                    date_of_issue,
                                    other_reference,
                                    file_size,
                                    disclosure_status,
                                    region,
                                    nald_id,
                                    nald_issue_no,
                                    nald_increment_no,
                                    primary_template,
                                    secondary_template,
                                    number_of_pages,
                                    doi_signature_date_match,
                                    included_in_version_match,
                                    single_licence_in_version_match,
                                    version_match_file_url,
                                    duplicate_licence_in_version_match_result,
                                    nald_issue,
                                    file_id,
                                    file_id_status,
                                    file_id_status_change_date,
                                    is_water_company,
                                    folder_name_auto_correct,
                                    seen_in_dms_extract,
                                    we_have_downloaded)
                               VALUES (
                                    @PermitNumber,
                                    @DmsPermitNumber,
                                    @FileUrl,
                                    @RuleUsed,
                                    @ChangeAuditAction,
                                    @LicenseNumber,
                                    @DocumentDate,
                                    @SignatureDate,
                                    @DateOfIssue,
                                    @OtherReference,
                                    @FileSize,
                                    @DisclosureStatus,
                                    @Region,
                                    @NaldId,
                                    @NaldIssueNo,
                                    @NaldIncrementNo,
                                    @PrimaryTemplate,
                                    @SecondaryTemplate,
                                    @NumberOfPages,
                                    @DoiSignatureDateMatch,
                                    @IncludedInVersionMatch,
                                    @SingleLicenceInVersionMatch,
                                    @VersionMatchFileUrl,
                                    @DuplicateLicenceInVersionMatchResult,
                                    @NaldIssue,
                                    @FileId,
                                    @FileIdStatus,
                                    @FileIdStatusChangeDate,
                                    @IsWaterCompany,
                                    @FolderNameAutoCorrect,
                                    @SeenInDmsExtract,
                                    @WeHaveDownloaded)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            result);
    }

    public async Task SaveAggregateSetAsync(int licenceSetId, string? aggregateSetId, string data, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO aggregate_set (licence_set_id, schema_aggregate_set_id, data, process_run_id, date_time_utc)
                           VALUES (@LicenceSetId, @SchemaAggregateSetId, @Data, @ProcessRunId, @DateTimeUtc)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                LicenceSetId = licenceSetId,
                SchemaAggregateSetId = aggregateSetId,
                Data = data,
                ProcessRunId = processRunId,
                DateTimeUtc = DateTime.UtcNow
            });
    }
    
    public async Task<int> SaveLicenceSectionVerificationAsync(LicenceSectionVerification verification)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO licence_section_verification (licence_file_id, process_run_id, licence_section_name, licence_section_scraped_value, licence_section_snapshot_value, licence_section_override_value, verification_type, licence_section_item_id, notes, created_date_time_utc)
                           VALUES (@LicenceFileId, @ProcessRunId, @LicenceSectionName, CAST(@LicenceSectionScrapedValue AS jsonb), CAST(@LicenceSectionSnapshotValue AS jsonb), CAST(@LicenceSectionOverrideValue AS jsonb), @VerificationType, @LicenceSectionItemId, @Notes, @CreatedDateTimeUtc)
                           RETURNING licence_section_verification_id
                           """;

        return await ExecuteScalarAsync(
            connection,
            sql,
            0,
            new
            {
                verification.LicenceFileId,
                verification.ProcessRunId,
                verification.LicenceSectionName,
                verification.LicenceSectionScrapedValue,
                verification.LicenceSectionSnapshotValue,
                verification.LicenceSectionOverrideValue,
                verification.VerificationType,
                verification.LicenceSectionItemId,
                verification.Notes,
                CreatedDateTimeUtc = DateTime.UtcNow
            });
    }
    
    private async Task<DateTime> ExecuteDateTimeScalarAsync(NpgsqlConnection connection, string sql, int retryNumber, object? param = null)
    {
        try
        {
            var dtStart = DateTime.Now;
            var thisQueryNumber = NpgsqlDataSourceProvider.QueryNumber++;

            if (NpgsqlDataSourceProvider.AddDebugLogging)
            {
                NpgsqlDataSourceProvider.Queries.Add((thisQueryNumber, sql));
            }

            var result = await connection.ExecuteScalarAsync<DateTime>(sql, param);
            var duration =  DateTime.Now - dtStart;

            if (duration.TotalSeconds > 1)
            {
                ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresWriteService)} - Query {thisQueryNumber} - {sql.Replace("\n", " ")} took {duration.TotalMilliseconds}ms");
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
            
            ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresWriteService)} - ExecuteScalarAsync retrying");

            await RetryHelper.WaitWithMessageAsync(retryNumber, nameof(PostgresWriteService));
            return await ExecuteDateTimeScalarAsync(GetPostgresConnection(), sql, retryNumber + 1, param);
        }
    }
    private async Task<int> ExecuteScalarAsync(NpgsqlConnection connection, string sql, int retryNumber, object? param = null)
    {
        try
        {
            var dtStart = DateTime.Now;
            var thisQueryNumber = NpgsqlDataSourceProvider.QueryNumber++;

            if (NpgsqlDataSourceProvider.AddDebugLogging)
            {
                NpgsqlDataSourceProvider.Queries.Add((thisQueryNumber, sql));
            }

            var result = await connection.ExecuteScalarAsync<int>(sql, param);
            var duration =  DateTime.Now - dtStart;

            if (duration.TotalSeconds > 1)
            {
                ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresWriteService)} - Query {thisQueryNumber} - {sql.Replace("\n", " ")} took {duration.TotalMilliseconds}ms");
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
            
            ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresWriteService)} - ExecuteScalarAsync retrying");

            await RetryHelper.WaitWithMessageAsync(retryNumber, nameof(PostgresWriteService));
            return await ExecuteScalarAsync(GetPostgresConnection(), sql, retryNumber + 1, param);
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
            var duration =  DateTime.Now - dtStart;

            if (duration.TotalSeconds > 1)
            {
                ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresWriteService)} - Query {thisQueryNumber} - {sql.Replace("\n", " ")} took {duration.TotalMilliseconds}ms");
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
            
            ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresWriteService)} - ExecuteAsync retrying");

            await RetryHelper.WaitWithMessageAsync(retryNumber, nameof(PostgresReadService));
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
                $"WARNING - {nameof(PostgresReadService)} - CreateConnection took {duration.TotalMilliseconds}ms");
        }

        return conn;
    }
}