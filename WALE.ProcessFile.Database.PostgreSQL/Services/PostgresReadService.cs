using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.OutputSchema.Table;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public class PostgresReadService(INpgsqlDataSourceProvider dataSourceProvider)
    : IDatabaseReadService
{
    public async Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT response 
                           FROM no_ocr_pages_metadata_cache 
                           WHERE filename = @Filename 
                             AND no_ocr_service_name = @NoOcrServiceName
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
        {
            Filename = request.Filepath,
            request.NoOcrServiceName
        });
    }

    public async Task<byte[]?> GetPageScreenshotAsync(int pageNumber, string fileName, string noOcrServiceName)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM page_screenshot 
                           WHERE filename = @Filename 
                               AND no_ocr_service_name = @NoOcrServiceName 
                               AND page_number = @PageNumber
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<byte[]>(
            connection,
            sql,
            0,
            new
            {
                Filename = fileName,
                NoOcrServiceName = noOcrServiceName,
                PageNumber = pageNumber
            });
    }

    public async Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM no_ocr_page_text_cache 
                           WHERE filename = @Filename 
                             AND page_number = @PageNumber 
                             AND no_ocr_service_name = @NoOcrServiceName
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                Filename = request.Filepath,
                request.PageNumber,
                request.NoOcrServiceName
            });
    }

    public async Task<string?> GetAllPagesTextAsync(string pdfFilename, string noOcrServiceName)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM all_pages_text
                           WHERE filename = @Filename
                             AND no_ocr_service_name = @NoOcrServiceName
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                Filename = pdfFilename,
                NoOcrServiceName = noOcrServiceName
            });
    }

    public async Task<string?> GetNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT response 
                           FROM no_ocr_images_metadata_cache
                           WHERE filename = @Filename 
                             AND no_ocr_service_name = @NoOcrServiceName
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                Filename = request.Filepath,
                request.NoOcrServiceName
            });
    }

    public async Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM ocr_image_text_cache 
                           WHERE filename = @Filename 
                             AND ocr_service_name = @OcrServiceName 
                             AND page_number = @PageNumber 
                             AND image_number = @ImageNumber
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                Filename = request.Filepath,
                request.OcrServiceName,
                request.PageNumber,
                request.ImageNumber
            });
    }

    public async Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM ocr_screenshot_text_cache 
                           WHERE filename = @Filename
                             AND ocr_service_name = @OcrServiceName
                             AND page_number = @PageNumber
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                Filename = request.Filepath,
                request.OcrServiceName,
                request.PageNumber
            });
    }
    
    public async Task<string?> GetTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM ocr_temporary_image_text_cache 
                           WHERE filename = @Filename 
                             AND ocr_service_name = @OcrServiceName 
                             AND page_number = @PageNumber 
                             AND image_number = @ImageNumber
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                Filename = request.Filepath,
                request.OcrServiceName,
                request.PageNumber,
                request.ImageNumber
            });
    }

    public async Task<string?> GetTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM ocr_temporary_screenshot_text_cache 
                           WHERE filename = @Filename
                             AND ocr_service_name = @OcrServiceName
                             AND page_number = @PageNumber
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                Filename = request.Filepath,
                request.OcrServiceName,
                request.PageNumber
            });
    }

    public async Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM image_on_page 
                           WHERE filename = @Filename 
                             AND no_ocr_service_name = @NoOcrServiceName 
                             AND page_number = @PageNumber 
                             AND image_number = @ImageNumber 
                             AND extension = @Extension
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<byte[]>(
            connection,
            sql,
            0,
            new
            {
                Filename = request.Filepath,
                request.NoOcrServiceName,
                request.PageNumber,
                request.ImageNumber,
                request.Extension
            });
    }

    public async Task<List<(int pageNumber, int imageNumber, string extension, int width, int height)>>
        GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                                page_number
                                , image_number
                                , extension
                                , width
                                , height
                           FROM image_on_page 
                           WHERE
                               (filename = @Filename or @Filename is null)
                                AND (page_number = @PageNumber or @PageNumber is null)
                           """;

        var results = await QueryAsync<(int, int, string, int, int)>(
            connection,
            sql,
            0,
            new
            {
                Filename = request.Filepath,
                request.PageNumber
            });

        return results.ToList();
    }

    public async Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               process_run_id, 
                               description, 
                               start_date_time_utc, 
                               end_date_time_utc, 
                               number_of_files 
                           FROM process_run
                           """;

        return (await QueryAsync<ProcessRun>(
            connection,
            sql,
            0)).ToList();
    }

    public async Task<ProcessRun?> GetMostRecentProcessRunAsync(string filename)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               l.process_run_id, 
                               pr.description, 
                               pr.start_date_time_utc, 
                               pr.end_date_time_utc, 
                               pr.number_of_files 
                           FROM licence l
                           JOIN process_run pr
                               ON l.process_run_id = pr.process_run_id 
                           WHERE filename = @Filename 
                           ORDER BY l.process_run_id DESC
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<ProcessRun>(
            connection,
            sql,
            0,
            new
            {
                Filename = filename
            });
    }

    public async Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data, licence_id 
                           FROM licence 
                           WHERE process_run_id = @ProcessRunId
                           """;

        var results = await QueryAsync<(string Data, int LicenceId)>(
            connection,
            sql,
            0,
            new
            {
                ProcessRunId = processRunId
            });

        return results.Select(r =>
        {
            var licence = JsonSerializer.Deserialize<Licence>(r.Data, GetSerializerOptions())!;
            licence.NoneSchemaData.TryAdd("licenceId", r.LicenceId);
            
            return licence;
        }).ToList();
    }

    public async Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               licence_set_id, 
                               short_licence_set_id, 
                               schema_licence_set_id 
                           FROM licence_set 
                           WHERE process_run_id = @ProcessRunId
                           """;

        return (await QueryAsync<LicenceSetTable>(
            connection,
            sql,
            0,
            new
            {
                ProcessRunId = processRunId
            })).ToList();
    }

    public async Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(string filename, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT DISTINCT
                               ls.licence_set_id,
                               ls.short_licence_set_id, 
                               ls.schema_licence_set_id 
                           FROM licence_set ls
                           JOIN licence_set_licence lsl on lsl.licence_set_id = ls.licence_set_id
                           JOIN licence l on l.licence_id = lsl.licence_id AND l.filename = @Filename
                           WHERE
                               ls.process_run_id = @ProcessRunId
                           """;

        return (await QueryAsync<LicenceSetTable>(
            connection,
            sql,
            0,
            new
            {
                ProcessRunId = processRunId,
                Filename = filename
            })).ToList();
    }

    public async Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               licence_id,
                               licence_number,
                               licence_version_id,
                               licence_set_id,
                               process_run_id
                           FROM licence_set_licence 
                           WHERE process_run_id = @ProcessRunId
                           """;

        return (await QueryAsync<LicenceSetLicence>(
            connection,
            sql,
            0,
            new
            {
                ProcessRunId = processRunId
            })).ToList();
    }

    public async Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int licenceSetId, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               licence_id,
                               licence_number,
                               licence_version_id,
                               licence_set_id,
                               process_run_id
                           FROM licence_set_licence 
                           WHERE licence_set_id = @LicenceSetId 
                             AND process_run_id = @ProcessRunId
                           """;
        
        return (await QueryAsync<LicenceSetLicence>(
            connection,
            sql,
            0,
            new
            {
                ProcessRunId = processRunId,
                LicenceSetId = licenceSetId
            })).ToList();
    }

    public async Task<LicenceSetType[]> GetLicenceSetTypes(int licenceSetId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT licence_set_type 
                           FROM licence_set_type 
                           WHERE licence_set_id = @LicenceSetId
                           """;

        return (await QueryAsync<int>(
                connection,
                sql,
                0,
                new { LicenceSetId = licenceSetId }))
            .Select(x => (LicenceSetType)x)
            .ToArray();
    }

    public async Task<List<(int LicenceSetId, LicenceSetType Type)>> GetLicenceSetTypesForProcessRun(int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                            lst.licence_set_id,
                            lst.licence_set_type
                           FROM licence_set_type lst
                           JOIN licence_set ls on ls.licence_set_id = lst.licence_set_id
                           WHERE ls.process_run_id = @ProcessRunId
                           """;

        var results = await QueryAsync<(int LicenceSetId, int LicenceSetType)>(
            connection,
            sql,
            0,
            new { ProcessRunId = processRunId });
        
        return results.Select(r => (r.LicenceSetId, (LicenceSetType)r.LicenceSetType)).ToList();
    }

    public async Task<AggregateSet[]?> GetAggregateSets(int licenceSetId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               aggregate_set_id,
                               schema_aggregate_set_id,
                               data 
                           FROM aggregate_set 
                           WHERE licence_set_id = @LicenceSetId
                           """;

        // This was not fully implemented in the SqlServerReadService, so I am skipping it for now.
        return [];
    }

    public async Task<List<(int LicenceSetId, AggregateSet AggregateSet)>> GetAggregateSetsForProcessRun(
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               aggregate_set_id,
                               schema_aggregate_set_id,
                               data 
                           FROM aggregate_set 
                           WHERE process_run_id = @ProcessRunId
                           """;
        
        // This was not fully implemented in the SqlServerReadService, so I am skipping it for now.
        return [];
    }

    public async Task<Licence?> GetLicenceAsync(string filename)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               data,
                               licence_id 
                           FROM licence
                           WHERE filename = @Filename 
                           ORDER BY process_run_id DESC
                           LIMIT 1;
                           """;
        
        var result =  await QuerySingleOrDefaultAsync<(string Data, int LicenceId)?>(
            connection,
            sql,
            0,
            new { Filename = filename });
        if (result == null)
        {
            return null;
        }
        
        var data = JsonSerializer.Deserialize<Licence>(result.Value.Data, GetSerializerOptions())!;
        data.NoneSchemaData.TryAdd("licenceId", result.Value.LicenceId);
        return data;
    }

    public async Task<Licence?> GetLicenceAsync(string licenceNumber, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               data,
                               licence_id 
                           FROM licence
                           WHERE licence_number = @LicenceNumber 
                             AND process_run_id = @ProcessRunId
                           LIMIT 1;
                           """;
        
        var result =  await QuerySingleOrDefaultAsync<(string Data, int LicenceId)?>(
            connection,
            sql,
            0,
            new
            {
                LicenceNumber = licenceNumber,
                ProcessRunId = processRunId
            });
        
        if (result == null)
        {
            return null;
        }
        
        var data = JsonSerializer.Deserialize<Licence>(result.Value.Data, GetSerializerOptions())!;
        data.NoneSchemaData.TryAdd("licenceId", result.Value.LicenceId);
        return data;
    }

    public async Task<MatchesResult?> GetMatchesResult(string filename)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM matches_result 
                           WHERE filename = @Filename 
                           ORDER BY process_run_id DESC
                           LIMIT 1;
                           """;
        
        var result =  await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new { Filename = filename });

        return result == null 
            ? null 
            : JsonSerializer.Deserialize<MatchesResult>(result, GetSerializerOptions());
    }

    private async Task<T?> QuerySingleOrDefaultAsync<T>(NpgsqlConnection connection, string sql, int retryNumber, object? param = null)
    {
        try
        {
            return await connection.QuerySingleOrDefaultAsync<T>(sql, param);
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

            await RetryHelper.WaitWithMessageAsync(retryNumber);
            return await QuerySingleOrDefaultAsync<T>(GetPostgresConnection(), sql, retryNumber + 1, param);
        }
    }
    
    private async Task<IEnumerable<T>> QueryAsync<T>(NpgsqlConnection connection, string sql, int retryNumber, object? param = null)
    {
        try
        {
            return await connection.QueryAsync<T>(sql, param);
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

            await RetryHelper.WaitWithMessageAsync(retryNumber);
            return await QueryAsync<T>(GetPostgresConnection(), sql, retryNumber + 1, param);
        }
    }
    
    private NpgsqlConnection GetPostgresConnection()
        => dataSourceProvider.DataSource.CreateConnection();

    // TODO move to a 'Core' layer
    private static JsonSerializerOptions GetSerializerOptions() =>
        new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
}