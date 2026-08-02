using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public class PostgresReadService(INpgsqlDataSourceProvider dataSourceProvider)
    : IDatabaseReadService
{
    public async Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               file_id,
                               dms_file_path,
                               process_run_id,
                               status,
                               status_date_utc
                           FROM sharepoint_fileid
                           """;

        var results = await QueryAsync<DmsFileIdInformation>(
            connection,
            sql,
            0,
            new { });

        return results.ToList();
    }

    public async Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT response 
                           FROM no_ocr_pages_metadata_cache 
                           WHERE file_id = @FileId
                             AND no_ocr_service_name = @NoOcrServiceName
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                request.FileId,
                request.NoOcrServiceName
            });
    }

    public async Task<byte[]?> GetPageScreenshotThumbnailAsync(int pageNumber, Guid fileId, string noOcrServiceName)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data
                           FROM page_screenshot_thumbnail
                           WHERE file_id = @FileId
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
                FileId = fileId,
                NoOcrServiceName = noOcrServiceName,
                PageNumber = pageNumber
            });
    }
    
    public async Task<List<string>> GetDistinctIssuersAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();

        const string sql =
            """
            SELECT DISTINCT
                   data::jsonb -> 'licenceVersion' ->> 'issuer' AS issuer
            FROM licence
            WHERE process_run_id = @ProcessRunId
              AND data::jsonb ->> 'status' = 'Ok'
              AND NULLIF(
                    trim(data::jsonb -> 'licenceVersion' ->> 'issuer'),
                    ''
                  ) IS NOT NULL
            ORDER BY issuer;
            """;

        var issuers = await QueryAsync<string>(
            connection,
            sql,
            0,
            new
            {
                ProcessRunId = processRunId
            });

        return issuers.ToList();
    }

    public async Task<DmsFileData?> GetDmsFileDataAsync(string? licenceNumber)
    {
        await using var connection = GetPostgresConnection();
        
        var sql = """
                   SELECT
                       lfr.permit_number,
                       de.file_url as dmsPath,
                       concat(lower(lfr.permit_number), '__', lower(lfr.file_id), '.pdf') as destinationFileName,
                       uuid(de.file_id) as file_id
                   FROM public.licence_finder_result lfr
                   JOIN public.dms_extract de
                       ON lower(lfr.permit_number) like CONCAT(lower(de.permit_number), '%') -- TODO change this in future for performance (we shouldnt have to lower or use a partial match)
                       AND de.file_id = lfr.file_id
                   WHERE
                       lfr.license_number = @LicenceNumber
                   LIMIT 1;
               """;
        
        return await QueryFirstOrDefaultAsync<DmsFileData>(
            connection,
            sql,
            0,
            new { LicenceNumber = licenceNumber });
    }
    
    public async Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync(Guid fileId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               file_id,
                               dms_file_path,
                               process_run_id,
                               status,
                               status_date_utc
                           FROM sharepoint_fileid
                           WHERE file_id = @FileId
                           """;

        var results = await QueryAsync<DmsFileIdInformation>(
            connection,
            sql,
            0,
            new { FileId = fileId });

        return results.ToList();
    }
    
    public async Task<byte[]?> GetPageScreenshotAsync(int pageNumber, Guid fileId, string noOcrServiceName)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data
                           FROM page_screenshot
                           WHERE file_id = @FileId
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
                FileId = fileId,
                NoOcrServiceName = noOcrServiceName,
                PageNumber = pageNumber
            });
    }

    public async Task<Dictionary<int, string>?> GetNoOcrAllPagesTextLinesAsync(NoOcrServiceMetadataCacheRequest request)
    {
        await using var connection = GetPostgresConnection();

        const string sql = """
                           SELECT
                                page_number
                                , data 
                           FROM no_ocr_page_text_cache
                           WHERE
                               file_id = @FileId
                               AND no_ocr_service_name = @NoOcrServiceName;
                           """;

        var results = await QueryAsync<(int, string)>(
            connection,
            sql,
            0,
            new
            {
                request.FileId,
                request.NoOcrServiceName
            });

        var resultList = results.ToList();
        if (resultList.Count == 0)
        {
            return null;
        }

        var returnDict = new Dictionary<int, string>();

        foreach (var (pageNumber, data) in resultList)
        {
            if (!returnDict.TryAdd(pageNumber, data))
            {
                // TODO some weird circumstance meant that certain (not all) pages were repeated
                // PROBABLY because of retry logic (might be limited to Ryan's machine)
                ConsoleHelper.WriteLine(
                    $"WARNING - {nameof(PostgresReadService)} - Page number {pageNumber} is duplicated in {request.FileId}");
            }
        }

        return returnDict;
    }

    public async Task<string?> GetAllPagesTextAsync(Guid fileId, string noOcrServiceName)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM all_pages_text
                           WHERE file_id = @FileId
                             AND no_ocr_service_name = @NoOcrServiceName
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                FileId = fileId,
                NoOcrServiceName = noOcrServiceName
            });
    }

    public async Task<string?> GetNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT response 
                           FROM no_ocr_images_metadata_cache
                           WHERE file_id = @FileId 
                             AND no_ocr_service_name = @NoOcrServiceName
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                request.FileId,
                request.NoOcrServiceName
            });
    }

    public async Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM ocr_image_text_cache 
                           WHERE file_id = @FileId
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
                request.FileId,
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
                           WHERE file_id = @FileId
                             AND ocr_service_name = @OcrServiceName
                             AND page_number = @PageNumber
                           ORDER BY date_time_utc desc
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                request.FileId,
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
                           WHERE file_id = @FileId
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
                request.FileId,
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
                           WHERE file_id = @FileId
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
                request.FileId,
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
                           WHERE file_id = @FileId
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
                request.FileId,
                request.NoOcrServiceName,
                request.PageNumber,
                request.ImageNumber,
                request.Extension
            });
    }

    public async Task<List<ImageDetails>>
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
                               (file_id = @FileId or @FileId is null)
                                AND (page_number = @PageNumber or @PageNumber is null)
                           """;

        var results = await QueryAsync<ImageDetails>(
            connection,
            sql,
            0,
            new
            {
                request.FileId,
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
                           WHERE end_date_time_utc IS NOT NULL
                           """;

        return (await QueryAsync<ProcessRun>(
            connection,
            sql,
            0)).ToList();
    }

    public async Task<ProcessRun?> GetMostRecentProcessRunAsync(Guid fileId)
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
                           WHERE file_id = @FileId 
                           ORDER BY l.process_run_id DESC
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<ProcessRun>(
            connection,
            sql,
            0,
            new
            {
                FileId = fileId
            });
    }
    
    public async Task<MatchesResult?> GetMatchesResult(Guid fileId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM matches_result 
                           WHERE file_id = @FileId
                           ORDER BY process_run_id DESC
                           LIMIT 1;
                           """;

        var result = await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new { FileId = fileId });

        return result == null
            ? null
            : JsonSerializer.Deserialize<MatchesResult>(result, GetSerializerOptions());
    }

    public async Task<MatchesResult?> GetMatchesResult(Guid fileId, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data 
                           FROM matches_result 
                           WHERE
                               file_id = @FileId
                                and process_run_id = @ProcessRunId
                           ORDER BY process_run_id DESC
                           LIMIT 1;
                           """;

        var result = await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                FileId = fileId,
                ProcessRunId = processRunId
            });

        return result == null
            ? null
            : JsonSerializer.Deserialize<MatchesResult>(result, GetSerializerOptions());
    }
    
    public async Task<List<DmsExtract>> GetDmsExtractAsync(int skip, int take)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               site_collection,
                               library_name,
                               permit_number,
                               file_name,
                               file_size,
                               file_type,
                               customer_operator_name,
                               facility_name,
                               facility_address,
                               facility_address_postcode,
                               regime,
                               activity_class,
                               activity_sub_class,
                               type_of_permit,
                               catchment,
                               national_security,
                               disclosure_status,
                               document_date,
                               upload_date,
                               file_url,
                               other_reference,
                               modified_date,
                               file_id
                           FROM public.dms_extract
                           ORDER BY
                               site_collection,
                               library_name,
                               permit_number,
                               file_name
                           LIMIT @take
                           OFFSET @skip;
                           """;

        var results = await QueryAsync<DmsExtract>(
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
    
    public async Task<List<DmsFileReaderResult>> GetDmsFileReaderResultsAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               status,
                               error_message,
                               licence_number,
                               permit_number,
                               file_name,
                               original_file_name,
                               file_id,
                               date_of_issue,
                               number_of_pages,
                               primary_type,
                               secondary_type,
                               file_type,
                               confidence,
                               identified_by_rule,
                               matched_terms,
                               file_size
                           FROM public.dms_file_reader
                           """;

        var results = await QueryAsync<DmsFileReaderResult>(
            connection,
            sql,
            0);

        return results.ToList();
    }

    public async Task<string?> GetImportRunDateAsync(string dataSource)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           select date_time
                           from import_dates
                           where "data_source" = @DataSource
                           order by date_time desc
                           limit 1;
                           """;

        var result = await QuerySingleOrDefaultAsync<string?>(
            connection,
            sql,
            0,
            new
            {
                DataSource = dataSource
            });

        if (result == null)
        {
            return null;
        }

        return result;
    }
    
    public async Task<List<ProcessRun>> GetAllProcessRunsAsync()
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
    
    private async Task<T?> QuerySingleOrDefaultAsync<T>(
        NpgsqlConnection connection,
        string sql,
        int retryNumber,
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

            var result = await connection.QuerySingleOrDefaultAsync<T>(sql, param);
            var duration = DateTime.Now - dtStart;

            if (_showAllLogs || duration.TotalSeconds > 1)
            {
                ConsoleHelper.WriteLine(
                    $"WARNING - {nameof(PostgresReadService)} - Query {thisQueryNumber} - {sql.Replace("\n", " ")} took {duration.TotalMilliseconds}ms");
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

            ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresReadService)} - QuerySingleOrDefaultAsync retrying");

            await RetryHelper.WaitWithMessageAsync(retryNumber, nameof(PostgresReadService));
            return await QuerySingleOrDefaultAsync<T>(
                GetPostgresConnection(),
                sql,
                retryNumber + 1,
                param);
        }
    }

    private async Task<T?> QueryFirstOrDefaultAsync<T>(
        NpgsqlConnection connection,
        string sql,
        int retryNumber,
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

            var result = await connection.QueryFirstOrDefaultAsync<T>(sql, param);
            var duration = DateTime.Now - dtStart;

            if (_showAllLogs || duration.TotalSeconds > 1)
            {
                ConsoleHelper.WriteLine(
                    $"WARNING - {nameof(PostgresReadService)} - Query {thisQueryNumber} - {sql.Replace("\n", " ")} took {duration.TotalMilliseconds}ms");
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

            ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresReadService)} - QueryFirstOrDefaultAsync retrying");

            await RetryHelper.WaitWithMessageAsync(retryNumber, nameof(PostgresReadService));
            return await QueryFirstOrDefaultAsync<T>(
                GetPostgresConnection(),
                sql,
                retryNumber + 1,
                param);
        }
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

            if (_showAllLogs || duration.TotalSeconds > 1)
            {
                ConsoleHelper.WriteLine(
                    $"WARNING - {nameof(PostgresReadService)} - Query {thisQueryNumber} - {sql.Replace("\n", " ")} took {duration.TotalMilliseconds}ms");
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

            ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresReadService)} - QueryAsync retrying");

            await RetryHelper.WaitWithMessageAsync(retryNumber, nameof(PostgresReadService));
            return await QueryAsync<T>(
                GetPostgresConnection(),
                sql,
                retryNumber + 1,
                param);
        }
    }

    private NpgsqlConnection GetPostgresConnection()
    {
        var dtStart = DateTime.Now;

        var conn = dataSourceProvider.DataSource.CreateConnection();
        var duration = DateTime.Now - dtStart;

        if (_showAllLogs || duration.TotalSeconds > 1)
        {
            ConsoleHelper.WriteLine(
                $"WARNING - {nameof(PostgresReadService)} - CreateConnection took {duration.TotalMilliseconds}ms");
        }

        return conn;
    }
    
    private static void AddLicenceSearchTermFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return;
        }

        sql.AppendLine(
            """
              AND (
                  data::jsonb
                      -> 'licenceNumber'
                      ->> 'value'
                      ILIKE '%' || @SearchTerm || '%'

                  OR EXISTS (
                      SELECT 1
                      FROM jsonb_array_elements(
                          COALESCE(
                              data::jsonb -> 'linkedLicences',
                              '[]'::jsonb
                          )
                      ) AS linked_licence
                      WHERE linked_licence ->> 'licenceNumber'
                            ILIKE '%' || @SearchTerm || '%'
                  )
              )
            """);

        parameters.Add("SearchTerm", searchTerm.Trim());
    }
    private static void AddIssuerFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string? issuer)
    {
        if (string.IsNullOrWhiteSpace(issuer))
        {
            return;
        }

        sql.AppendLine(
            """
              AND data::jsonb -> 'licenceVersion' ->> 'issuer' = @Issuer
            """);

        parameters.Add("Issuer", issuer);
    }
    
    private static void AddOcrScanFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        bool? ocrScan)
    {
        if (!ocrScan.HasValue)
        {
            return;
        }

        sql.AppendLine(
            ocrScan.Value
                ? """
                    AND data::jsonb -> 'noneSchemaData' ->> 'ocr' = 'OCR'
                  """
                : """
                    AND COALESCE(
                        data::jsonb -> 'noneSchemaData' ->> 'ocr',
                        ''
                    ) <> 'OCR'
                  """);

        parameters.Add("OcrScan", ocrScan.Value);
    }
    
    private static void AddMeansFoundFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        bool? meansFound)
    {
        if (!meansFound.HasValue)
        {
            return;
        }

        sql.AppendLine(
            meansFound.Value
                ? """
                    AND jsonb_array_length(
                        COALESCE(
                            data::jsonb -> 'meansOfAbstraction',
                            '[]'::jsonb
                        )
                    ) > 0
                  """
                : """
                    AND jsonb_array_length(
                        COALESCE(
                            data::jsonb -> 'meansOfAbstraction',
                            '[]'::jsonb
                        )
                    ) = 0
                  """);
    }
    
    private static void AddArrayEmptyFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string parameterName,
        bool? empty,
        string jsonProperty)
    {
        if (!empty.HasValue)
        {
            return;
        }

        var comparison = empty.Value ? "= 0" : "> 0";

        sql.AppendLine(
            $"""
               AND jsonb_array_length(
                   COALESCE(
                       data::jsonb -> '{jsonProperty}',
                       '[]'::jsonb
                   )
               ) {comparison}
             """);
    }
    
    private static void AddArrayEmptyFilter(
        StringBuilder sql,
        bool? empty,
        string jsonProperty)
    {
        if (!empty.HasValue)
        {
            return;
        }

        var comparison = empty.Value ? "= 0" : "> 0";

        sql.AppendLine(
            $"""
               AND jsonb_array_length(
                   COALESCE(
                       data::jsonb -> '{jsonProperty}',
                       '[]'::jsonb
                   )
               ) {comparison}
             """);
    }
    
    private static void AddNestedLimitsFilter(
        StringBuilder sql,
        bool? empty,
        string collectionName)
    {
        if (!empty.HasValue)
        {
            return;
        }

        var existsKeyword = empty.Value
            ? "NOT EXISTS"
            : "EXISTS";

        sql.AppendLine(
            $"""
               AND {existsKeyword} (
                   SELECT 1
                   FROM jsonb_array_elements(
                       COALESCE(
                           data::jsonb
                               -> 'abstractionLimits'
                               -> '{collectionName}',
                           '[]'::jsonb
                       )
                   ) item
                   WHERE jsonb_array_length(
                       COALESCE(
                           item -> 'limits',
                           '[]'::jsonb
                       )
                   ) > 0
               )
             """);
    }
    
    private static void AddLinkedLicencesTypeFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string? linkedLicencesType)
    {
        if (string.IsNullOrWhiteSpace(linkedLicencesType))
        {
            return;
        }
        
        
        if (string.Equals(
                linkedLicencesType,
                "NoRecords",
                StringComparison.OrdinalIgnoreCase))
        {
            sql.AppendLine(
                """
                  AND jsonb_array_length(
                      COALESCE(
                          data::jsonb -> 'linkedLicences',
                          '[]'::jsonb
                      )
                  ) = 0
                """);

            return;
        }

        if (string.Equals(
                linkedLicencesType,
                "ImplicitBackLink",
                StringComparison.OrdinalIgnoreCase))
        {
            sql.AppendLine(
                """
                  AND EXISTS (
                      SELECT 1
                      FROM jsonb_array_elements(
                          COALESCE(
                              data::jsonb -> 'linkedLicences',
                              '[]'::jsonb
                          )
                      ) linked_licence
                      CROSS JOIN jsonb_array_elements(
                          COALESCE(
                              linked_licence -> 'containedIn',
                              '[]'::jsonb
                          )
                      ) contained
                      WHERE contained ->> 'direction' = 'Incoming'
                  )
                """);

            return;
        }

        sql.AppendLine(
            """
              AND EXISTS (
                  SELECT 1
                  FROM jsonb_array_elements(
                      COALESCE(
                          data::jsonb -> 'linkedLicences',
                          '[]'::jsonb
                      )
                  ) linked_licence
                  CROSS JOIN jsonb_array_elements(
                      COALESCE(
                          linked_licence -> 'containedIn',
                          '[]'::jsonb
                      )
                  ) contained
                  WHERE contained ->> 'direction' = 'Outgoing'
                    AND contained ->> 'sectionName' = @LinkedLicencesType
              )
            """);

        parameters.Add(
            "LinkedLicencesType",
            linkedLicencesType.Trim());
    }
    
    private static void AddIssueYearFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        int? issueYear)
    {
        if (!issueYear.HasValue)
        {
            return;
        }

        sql.AppendLine(
            """
              AND data::jsonb
                  -> 'licenceVersion'
                  ->> 'issueDate'
                  LIKE @IssueYearPattern
            """);

        parameters.Add(
            "IssueYearPattern",
            $"{issueYear.Value}%");
    }

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

    private readonly bool _showAllLogs = false;
}