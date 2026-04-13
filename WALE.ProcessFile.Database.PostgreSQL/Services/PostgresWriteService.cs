using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public class PostgresWriteService(INpgsqlDataSourceProvider dataSourceProvider)
    : IDatabaseWriteService
{
    public async Task<ProcessRun> AddProcessRunAsync(ProcessRun processRun)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO process_run (description, start_date_time_utc, number_of_files) 
                           VALUES (@Description, @StartDateTimeUtc, @NumberOfFiles) 
                           RETURNING process_run_id
                           """;

        processRun.ProcessRunId = await ExecuteScalarAsync(
            connection,
            sql,
            0,
            new
            {
                processRun.Description,
                processRun.StartDateTimeUtc,
                processRun.NumberOfFiles
            });

        return processRun;
    }

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

    public async Task UpdateLicenceAsync(int licenceId, string licenceData, Guid fileId, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           UPDATE licence
                           SET
                               file_id = @FileId
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
                ProcessRunId = processRunId
            });
    }
    
    public async Task<int> SaveLicenceAsync(
        string? licenceNumber,
        string licenceData,
        Guid? fileId,
        string? permitNumber,
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO licence (file_id, licence_number, data, process_run_id, permit_number, date_time_utc)
                           VALUES (@FileId, @LicenceNumber, @Data, @ProcessRunId, @PermitNumber, @DateTimeUtc)
                           RETURNING licence_id
                           """;

        return await ExecuteScalarAsync(
            connection,
            sql,
            0,
            new {
                FileId = fileId,
                LicenceNumber = licenceNumber,
                Data = licenceData,
                ProcessRunId = processRunId,
                PermitNumber = permitNumber,
                DateTimeUtc = DateTime.UtcNow
            });
    }

    public async Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, string data)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO match (matches_result_id, label_name, label_group_name, data)
                           VALUES (@MatchesResultId, @LabelName, @LabelGroupName, @Data)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                MatchesResultId = matchesResultId,
                LabelName = labelName,
                LabelGroupName = labelGroupName,
                Data = data
            });
    }

    public async Task<int> SaveMatchesResultAsync(string matchesResult, Guid fileId, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO matches_result (file_id, data, process_run_id, date_time_utc)
                           VALUES (@FileId, @Data, @ProcessRunId, @DateTimeUtc)
                           RETURNING matches_result_id
                           """;

        return await ExecuteScalarAsync(
            connection,
            sql,
            0,
            new
            {
                FileId = fileId,
                Data = matchesResult,
                ProcessRunId = processRunId,
                DateTimeUtc = DateTime.UtcNow
            });
    }

    public async Task SavePageScreenshotAsync(
        int pageNumber,
        string noOcrServiceName,
        Guid fileId,
        byte[] data,
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO page_screenshot (file_id, page_number, no_ocr_service_name, data, date_time_utc, process_run_id)
                           VALUES (@FileId, @PageNumber, @NoOcrServiceName, @Data, @DateTimeUtc, @ProcessRunId)
                           """;
        
        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                FileId = fileId,
                PageNumber = pageNumber,
                NoOcrServiceName = noOcrServiceName,
                Data = data,
                DateTimeUtc = DateTime.UtcNow,
                ProcessRunId = processRunId
            });
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageAsync(
        NoOcrServicePageCacheRequest request,
        string data,
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO no_ocr_page_text_cache (file_id, page_number, no_ocr_service_name, data, process_run_id, date_time_utc)
                           VALUES (@FileId, @PageNumber, @NoOcrServiceName, @Data, @ProcessRunId, @DateTimeUtc)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                request.FileId,
                request.PageNumber,
                request.NoOcrServiceName,
                Data = data,
                ProcessRunId = processRunId,
                DateTimeUtc = DateTime.UtcNow
            });
        
        return request;
    }

    public async Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, string imagesMetadataStr,
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO no_ocr_images_metadata_cache (file_id, no_ocr_service_name, response, date_time_utc, process_run_id)
                           VALUES (@FileId, @NoOcrServiceName, @Response, @DateTimeUtc, @ProcessRunId)
                           """;
        
        await ExecuteAsync(
            connection,
            sql,
            0,
        new
            {
                request.FileId,
                request.NoOcrServiceName,
                Response = imagesMetadataStr,
                DateTimeUtc = DateTime.UtcNow,
                ProcessRunId = processRunId
            });
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(NoOcrServiceMetadataCacheRequest request,
        string dataStr, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO no_ocr_pages_metadata_cache (file_id, no_ocr_service_name, response, date_time_utc, process_run_id) 
                           VALUES (@FileId, @NoOcrServiceName, @Response, @DateTimeUtc, @ProcessRunId)
                           """;
        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                request.FileId,
                request.NoOcrServiceName,
                Response = dataStr,
                DateTimeUtc = DateTime.UtcNow,
                ProcessRunId = processRunId
            });
        
        return request;
    }

    public async Task SaveAllPagesTextAsync(
        string documentLinesStr,
        Guid fileId,
        string noOcrServiceName,
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO all_pages_text (file_id, no_ocr_service_name, data, date_time_utc, process_run_id)
                           VALUES (@FileId, @NoOcrServiceName, @Data, @DateTimeUtc, @ProcessRunId)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                FileId = fileId,
                NoOcrServiceName = noOcrServiceName,
                Data = documentLinesStr,
                DateTimeUtc = DateTime.UtcNow,
                ProcessRunId = processRunId
            });
    }

    public async Task SaveImageOnPageAsync(
        byte[] bytes,
        int width,
        int height,
        Guid fileId,
        string noOcrServiceName,
        int imageNumber,
        int pageNumber,
        string extension,
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO image_on_page (file_id, no_ocr_service_name, image_number, page_number, data, width, height, extension, date_time_utc, process_run_id) 
                           VALUES (@FileId, @NoOcrServiceName, @ImageNumber, @PageNumber, @Data, @Width, @Height, @Extension, @DateTimeUtc, @ProcessRunId)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                FileId = fileId,
                NoOcrServiceName = noOcrServiceName,
                Data = bytes,
                Width = width,
                Height = height,
                ImageNumber = imageNumber,
                PageNumber = pageNumber,
                Extension = extension,
                DateTimeUtc = DateTime.UtcNow,
                ProcessRunId = processRunId
            });
    }

    public async Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO ocr_image_text_cache (file_id, ocr_service_name, image_number, page_number, data, process_run_id, date_time_utc)
                           VALUES (@FileId, @OcrServiceName, @ImageNumber, @PageNumber, @Data, @ProcessRunId, @DateTimeUtc)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                request.FileId,
                request.OcrServiceName,
                Data = data,
                request.ImageNumber,
                request.PageNumber,
                ProcessRunId = processRunId,
                DateTimeUtc = DateTime.UtcNow
            });
    }

    public async Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO ocr_screenshot_text_cache (file_id, ocr_service_name, page_number, data, process_run_id, date_time_utc) 
                           VALUES (@FileId, @OcrServiceName, @PageNumber, @Data, @ProcessRunId, @DateTimeUtc)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                request.FileId,
                request.OcrServiceName,
                Data = data,
                request.PageNumber,
                ProcessRunId = processRunId,
                DateTimeUtc = DateTime.UtcNow
            });
    }
    
    public async Task SaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO ocr_temporary_image_text_cache (file_id, ocr_service_name, image_number, page_number, data, process_run_id, date_time_utc)
                           VALUES (@FileId, @OcrServiceName, @ImageNumber, @PageNumber, @Data, @ProcessRunId, @DateTimeUtc)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                request.FileId,
                request.OcrServiceName,
                Data = data,
                request.ImageNumber,
                request.PageNumber,
                ProcessRunId = processRunId,
                DateTimeUtc = DateTime.UtcNow
            });
    }

    public async Task SaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO ocr_temporary_screenshot_text_cache (file_id, ocr_service_name, page_number, data, process_run_id, date_time_utc) 
                           VALUES (@FileId, @OcrServiceName, @PageNumber, @Data, @ProcessRunId, @DateTimeUtc)
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                request.FileId,
                request.OcrServiceName,
                Data = data,
                request.PageNumber,
                ProcessRunId = processRunId,
                DateTimeUtc = DateTime.UtcNow
            });
    }

    public async Task ClearCacheAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           DELETE FROM all_pages_text;
                           DELETE FROM image_on_page;
                           DELETE FROM no_ocr_images_metadata_cache;
                           DELETE FROM no_ocr_pages_metadata_cache;
                           DELETE FROM no_ocr_page_text_cache;
                           DELETE FROM ocr_image_text_cache;
                           DELETE FROM ocr_screenshot_text_cache;
                           DELETE FROM page_screenshot;
                           DELETE FROM ocr_temporary_image_text_cache;
                           DELETE FROM ocr_temporary_screenshot_text_cache;
                           """;

        await ExecuteAsync(connection, sql, 0);
    }

    public async Task ClearCacheAsync(Guid fileId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           DELETE FROM all_pages_text WHERE file_id = @FileId;
                           DELETE FROM image_on_page WHERE file_id = @FileId;
                           DELETE FROM no_ocr_images_metadata_cache WHERE file_id = @FileId;
                           DELETE FROM no_ocr_pages_metadata_cache WHERE file_id = @FileId;
                           DELETE FROM no_ocr_page_text_cache WHERE file_id = @FileId;
                           DELETE FROM ocr_image_text_cache WHERE file_id = @FileId;
                           DELETE FROM ocr_screenshot_text_cache WHERE file_id = @FileId;
                           DELETE FROM page_screenshot WHERE file_id = @FileId;
                           DELETE FROM ocr_temporary_image_text_cache WHERE file_id = @FileId;
                           DELETE FROM ocr_temporary_screenshot_text_cache WHERE file_id = @FileId;
                           """;
        
        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                FileId = fileId
            });
    }

    public async Task UpdateProcessRunAsync(ProcessRun processRun)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           UPDATE process_run 
                           SET end_date_time_utc = @EndDateTimeUtc 
                           WHERE process_run_id = @ProcessRunId
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                processRun.ProcessRunId,
                EndDateTimeUtc = DateTime.UtcNow
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

    public async Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO sharepoint_fileid (file_id, dms_file_path, process_run_id, status, status_date_utc) 
                           VALUES (@FileId, @DmsFilePath, @ProcessRunId, @Status, @StatusDateUtc);
                           """;

        await ExecuteAsync(
            connection,
            sql,
            0,
            new
            {
                newDmsFileIdInformation.FileId,
                newDmsFileIdInformation.DmsFilePath,
                newDmsFileIdInformation.ProcessRunId,
                newDmsFileIdInformation.Status,
                newDmsFileIdInformation.StatusDateUtc
            });
    }

    
    public async Task<int> SaveLicenceSectionVerificationAsync(LicenceSectionVerification verification)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           INSERT INTO licence_section_verification (licence_file_id, process_run_id, licence_section_name, licence_section_value, verification_type, created_date_time_utc)
                           VALUES (@LicenceFileId, @ProcessRunId, @LicenceSectionName, CAST(@LicenceSectionValue AS jsonb), @VerificationType, @CreatedDateTimeUtc)
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
                verification.LicenceSectionValue,
                verification.VerificationType,
                CreatedDateTimeUtc = DateTime.UtcNow
            });
    }

    private async Task<int> ExecuteScalarAsync(NpgsqlConnection connection, string sql, int retryNumber, object? param = null)
    {
        try
        {
            var dtStart = DateTime.Now;
            var thisQueryNumber = NpgsqlDataSourceProvider.QueryNumber++;
            NpgsqlDataSourceProvider.Queries.Add((thisQueryNumber, sql));
            
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
            NpgsqlDataSourceProvider.Queries.Add((thisQueryNumber, sql));
            
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
        => dataSourceProvider.DataSource.CreateConnection();
}