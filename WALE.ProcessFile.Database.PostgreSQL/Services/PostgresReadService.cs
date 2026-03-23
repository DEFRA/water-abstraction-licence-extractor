using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.OutputSchema.Table;
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
            Filename = request.Filename,
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
                Filename = request.Filename,
                request.PageNumber,
                request.NoOcrServiceName
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
                               filename = @Filename
                               AND no_ocr_service_name = @NoOcrServiceName;
                           """;

        var results = await QueryAsync<(int, string)>(
            connection,
            sql,
            0,
            new
            {
                Filename = request.Filename,
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
                ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresReadService)} - Page number {pageNumber} is duplicated in {request.Filename}");
            }
        }
        
        return returnDict;
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
                Filename = request.Filename,
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
                           ORDER BY date_time_utc desc
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                Filename = request.Filename,
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
                           ORDER BY date_time_utc desc
                           LIMIT 1;
                           """;

        return await QuerySingleOrDefaultAsync<string>(
            connection,
            sql,
            0,
            new
            {
                Filename = request.Filename,
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
                Filename = request.Filename,
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
                Filename = request.Filename,
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
                Filename = request.Filename,
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
                               (filename = @Filename or @Filename is null)
                                AND (page_number = @PageNumber or @PageNumber is null)
                           """;

        var results = await QueryAsync<ImageDetails>(
            connection,
            sql,
            0,
            new
            {
                Filename = request.Filename,
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
        /*const string sql = """
                           SELECT 
                               aggregate_set_id,
                               schema_aggregate_set_id,
                               data 
                           FROM aggregate_set 
                           WHERE licence_set_id = @LicenceSetId
                           """;*/

        // This was not fully implemented in the SqlServerReadService, so I am skipping it for now.
        return [];
    }

    public async Task<List<(int LicenceSetId, AggregateSet AggregateSet)>> GetAggregateSetsForProcessRun(
        int processRunId)
    {
        await using var connection = GetPostgresConnection();
            /*const string sql = """
                                SELECT 
                                    aggregate_set_id,
                                    schema_aggregate_set_id,
                                    data 
                                FROM aggregate_set 
                                WHERE process_run_id = @ProcessRunId
                                """;*/
        
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

    public async Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           select distinct
                               lic."LIC_NO" AS LicenceNumber,
                               lc."PARAM1" AS Param1,
                               lc."PARAM2" AS Param2,
                               lc."TEXT" AS Text,
                               NULL AS Notes,
                               lic."FGAC_REGION_CODE" AS RegionCode
                           from nald."NALD_ABS_LICENCES" lic
                           join nald."NALD_ABS_LIC_VERSIONS" ver
                               ON lic."ID" = ver."AABL_ID"
                               AND lic."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                               AND ver."ISSUE_NO" = (
                                   SELECT MAX(LIC_VER_SUBQUERY."ISSUE_NO")
                                   FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY
                                   WHERE LIC_VER_SUBQUERY."AABL_ID" = ver."AABL_ID"
                                       AND LIC_VER_SUBQUERY."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                                       AND LIC_VER_SUBQUERY."EFF_ST_DATE" <= CURRENT_DATE
                                       AND (LIC_VER_SUBQUERY."EFF_END_DATE" >= CURRENT_DATE OR ver."EFF_END_DATE" IS NULL)
                                       AND LIC_VER_SUBQUERY."STATUS" <> 'DRAFT'
                                   )
                               AND ver."INCR_NO" = (
                                   SELECT MAX(LIC_VER_SUBQUERY_2."INCR_NO")
                                   FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_2
                                   WHERE LIC_VER_SUBQUERY_2."AABL_ID" = ver."AABL_ID"
                                       AND LIC_VER_SUBQUERY_2."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                                       AND LIC_VER_SUBQUERY_2."EFF_ST_DATE" <= CURRENT_DATE
                                       AND (LIC_VER_SUBQUERY_2."EFF_END_DATE" >= CURRENT_DATE OR ver."EFF_END_DATE" IS NULL)
                                       AND LIC_VER_SUBQUERY_2."STATUS" <> 'DRAFT'
                                   )    
                           join nald."NALD_ABS_LIC_PURPOSES" pu
                               ON ver."ISSUE_NO" = pu."AABV_ISSUE_NO"
                               AND ver."INCR_NO" = pu."AABV_INCR_NO"
                               AND ver."AABL_ID" = pu."AABV_AABL_ID"
                               AND ver."FGAC_REGION_CODE" = pu."FGAC_REGION_CODE"
                           join nald."NALD_LIC_CONDITIONS" lc
                               ON pu."ID" = lc."AABP_ID"
                               AND pu."FGAC_REGION_CODE" = lc."FGAC_REGION_CODE"
                               AND lc."ACIN_CODE" = 'AGG'
                               AND (lc."PARAM1" IS NOT NULL 
                                   OR lc."PARAM2" IS NOT NULL 
                                   OR lc."TEXT" IS NOT NULL)    
                           WHERE
                               (lic."EXPIRY_DATE" IS NULL OR lic."EXPIRY_DATE" >= CURRENT_DATE)
                               AND (lic."LAPSED_DATE" IS NULL OR lic."LAPSED_DATE" >= CURRENT_DATE)
                               AND (lic."REV_DATE" IS NULL OR lic."REV_DATE" >= CURRENT_DATE)
                           
                           UNION
                           
                           select distinct
                               lic."LIC_NO" AS LicenceNumber,
                               null AS Param1,
                               null AS Param2,
                               null AS Text,
                               lic."NOTES" AS Notes,
                               lic."FGAC_REGION_CODE" AS RegionCode
                           from nald."NALD_ABS_LICENCES" lic
                           join nald."NALD_ABS_LIC_VERSIONS" ver
                               ON lic."ID" = ver."AABL_ID"
                               AND lic."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                               AND ver."ISSUE_NO" = (
                                   SELECT MAX(LIC_VER_SUBQUERY."ISSUE_NO")
                                   FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY
                                   WHERE LIC_VER_SUBQUERY."AABL_ID" = ver."AABL_ID"
                                       AND LIC_VER_SUBQUERY."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                                       AND LIC_VER_SUBQUERY."EFF_ST_DATE" <= CURRENT_DATE
                                       AND (LIC_VER_SUBQUERY."EFF_END_DATE" >= CURRENT_DATE OR ver."EFF_END_DATE" IS NULL)
                                       AND LIC_VER_SUBQUERY."STATUS" <> 'DRAFT'
                                   )
                               AND ver."INCR_NO" = (
                                   SELECT MAX(LIC_VER_SUBQUERY_2."INCR_NO")
                                   FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_2
                                   WHERE LIC_VER_SUBQUERY_2."AABL_ID" = ver."AABL_ID"
                                       AND LIC_VER_SUBQUERY_2."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                                       AND LIC_VER_SUBQUERY_2."EFF_ST_DATE" <= CURRENT_DATE
                                       AND (LIC_VER_SUBQUERY_2."EFF_END_DATE" >= CURRENT_DATE OR ver."EFF_END_DATE" IS NULL)
                                       AND LIC_VER_SUBQUERY_2."STATUS" <> 'DRAFT'
                                   )
                           WHERE
                               (lic."EXPIRY_DATE" IS NULL OR lic."EXPIRY_DATE" >= CURRENT_DATE)
                               AND (lic."LAPSED_DATE" IS NULL OR lic."LAPSED_DATE" >= CURRENT_DATE)
                               AND (lic."REV_DATE" IS NULL OR lic."REV_DATE" >= CURRENT_DATE)
                               AND lic."NOTES" IS NOT NULL
                           """;
        
        var result = await QueryAsync<NaldLinkedLicenceRawData>(
            connection,
            sql,
            0);
        
        return result.ToList();
    }

    public async Task<List<NaldLicence>> GetNaldImpoundmentAndAbstractionLicencesAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               "LIC_NO" AS LicenceNumber,
                               "FGAC_REGION_CODE" AS RegionCode,
                               "ID" AS Id,
                               0 AS Type
                           FROM nald."NALD_ABS_LICENCES"
                           UNION ALL
                           SELECT 
                               "LIC_NO" AS LicenceNumber,
                               "FGAC_REGION_CODE" AS RegionCode,
                               "ID" AS Id,
                               1 AS Type
                           FROM nald."NALD_IMP_LICENCES";
                           """;
        
        var result = await QueryAsync<NaldLicence>(
            connection,
            sql,
            0);
        
        return result.ToList();
    }

    public async Task<(
            HashSet<(string, int)> Live,
            HashSet<(string, int)> Lapsed,
            HashSet<(string, int)> Expired,
            HashSet<(string, int)> Revoked,
            HashSet<(string, int)> Impoundment)>
        GetNaldLicenceNumbersAsync(short? regionCode)
    {
        // Run the 5 lookups concurrently.
        var liveTask = RunWithNewConnectionAsync(c => GetLiveLicenceNumbersAsync(c, regionCode));
        var lapsedTask = RunWithNewConnectionAsync(c => GetLapsedLicenceNumbersAsync(c, regionCode));
        var revokedTask = RunWithNewConnectionAsync(c => GetRevokedLicenceNumbersAsync(c, regionCode));
        var expiredTask = RunWithNewConnectionAsync(c => GetExpiredLicenceNumbersAsync(c, regionCode));
        var impoundmentTask = RunWithNewConnectionAsync(c => GetImpoundmentLicenceNumbersAsync(c, regionCode));

        return (
            await liveTask,
            await lapsedTask,
            await expiredTask,
            await revokedTask,
            await impoundmentTask);
        
        // Important: NpgsqlConnection is not safe for concurrent commands, so each task gets its own connection.
        async Task<HashSet<(string, int)>> RunWithNewConnectionAsync(Func<NpgsqlConnection, Task<HashSet<(string, int)>>> query)
        {
            await using var connection = GetPostgresConnection();
            return await query(connection);
        }
    }

    private async Task<HashSet<(string, int)>> GetLiveLicenceNumbersAsync(NpgsqlConnection connection, short? regionCode)
    {
        const string sql = """
                           SELECT
                             NALD_ABS_LICENCES."LIC_NO",
                             NALD_ABS_LICENCES."FGAC_REGION_CODE"
                           FROM nald."NALD_ABS_LICENCES" NALD_ABS_LICENCES
                           INNER JOIN nald."NALD_ABS_LIC_VERSIONS" NALD_ABS_LIC_VERSIONS
                             ON NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE" = NALD_ABS_LICENCES."FGAC_REGION_CODE"
                             AND NALD_ABS_LIC_VERSIONS."AABL_ID" = NALD_ABS_LICENCES."ID"
                           WHERE
                             (
                               (NALD_ABS_LICENCES."EXPIRY_DATE" IS NULL OR NALD_ABS_LICENCES."EXPIRY_DATE" >= CURRENT_DATE)
                               AND (NALD_ABS_LICENCES."LAPSED_DATE" IS NULL OR NALD_ABS_LICENCES."LAPSED_DATE" >= CURRENT_DATE)
                               AND (NALD_ABS_LICENCES."REV_DATE" IS NULL OR NALD_ABS_LICENCES."REV_DATE" >= CURRENT_DATE)
                             )
                             AND NALD_ABS_LIC_VERSIONS."ISSUE_NO" = (
                               SELECT MAX(LIC_VER_SUBQUERY."ISSUE_NO")
                               FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY
                               WHERE LIC_VER_SUBQUERY."AABL_ID" = NALD_ABS_LIC_VERSIONS."AABL_ID"
                                 AND LIC_VER_SUBQUERY."FGAC_REGION_CODE" = NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE"
                                 AND LIC_VER_SUBQUERY."EFF_ST_DATE" <= CURRENT_DATE
                                 AND (LIC_VER_SUBQUERY."EFF_END_DATE" >= CURRENT_DATE OR LIC_VER_SUBQUERY."EFF_END_DATE" IS NULL)
                                 AND LIC_VER_SUBQUERY."STATUS" <> 'DRAFT'
                             )
                             AND NALD_ABS_LIC_VERSIONS."INCR_NO" = (
                               SELECT MAX(LIC_VER_SUBQUERY_2."INCR_NO")
                               FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_2
                               WHERE LIC_VER_SUBQUERY_2."AABL_ID" = NALD_ABS_LIC_VERSIONS."AABL_ID"
                                 AND LIC_VER_SUBQUERY_2."FGAC_REGION_CODE" = NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE"
                                 AND LIC_VER_SUBQUERY_2."EFF_ST_DATE" <= CURRENT_DATE
                                 AND (LIC_VER_SUBQUERY_2."EFF_END_DATE" >= CURRENT_DATE OR LIC_VER_SUBQUERY_2."EFF_END_DATE" IS NULL)
                                 AND LIC_VER_SUBQUERY_2."STATUS" <> 'DRAFT'
                             )
                             AND NALD_ABS_LIC_VERSIONS."WA_ALTY_CODE" IN ('FULL', 'NA', 'TEMP', 'TRAN')
                             AND (@RegionCode IS NULL OR NALD_ABS_LICENCES."FGAC_REGION_CODE" = @RegionCode);
                           """;
        
        var results = await QueryAsync<(string LicenceNumber, short RegionCode)>(
            connection,
            sql,
            0,
            new { RegionCode = regionCode });
        
        return results
            .Select(r => (r.LicenceNumber, (int)r.RegionCode))
            .ToHashSet();
    }
    
    private async Task<HashSet<(string, int)>> GetRevokedLicenceNumbersAsync(NpgsqlConnection connection, short? regionCode)
    {
        const string sql = """
                           SELECT
                             NALD_ABS_LICENCES."LIC_NO",
                             NALD_ABS_LICENCES."FGAC_REGION_CODE"
                           FROM nald."NALD_ABS_LICENCES" NALD_ABS_LICENCES
                           INNER JOIN nald."NALD_ABS_LIC_VERSIONS" NALD_ABS_LIC_VERSIONS
                             ON NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE" = NALD_ABS_LICENCES."FGAC_REGION_CODE"
                             AND NALD_ABS_LIC_VERSIONS."AABL_ID" = NALD_ABS_LICENCES."ID"
                           WHERE
                               NALD_ABS_LICENCES."REV_DATE" < CURRENT_DATE
                             AND NALD_ABS_LIC_VERSIONS."ISSUE_NO" = (
                               SELECT MAX(LIC_VER_SUBQUERY."ISSUE_NO")
                               FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY
                               WHERE LIC_VER_SUBQUERY."AABL_ID" = NALD_ABS_LIC_VERSIONS."AABL_ID"
                                 AND LIC_VER_SUBQUERY."FGAC_REGION_CODE" = NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE"
                                 AND LIC_VER_SUBQUERY."STATUS" <> 'DRAFT'
                             )
                             AND NALD_ABS_LIC_VERSIONS."INCR_NO" = (
                               SELECT MAX(LIC_VER_SUBQUERY_2."INCR_NO")
                               FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_2
                               WHERE LIC_VER_SUBQUERY_2."AABL_ID" = NALD_ABS_LIC_VERSIONS."AABL_ID"
                                 AND LIC_VER_SUBQUERY_2."FGAC_REGION_CODE" = NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE"
                                 AND LIC_VER_SUBQUERY_2."STATUS" <> 'DRAFT'
                                 AND LIC_VER_SUBQUERY_2."ISSUE_NO" = (
                                   SELECT MAX(LIC_VER_SUBQUERY_3."ISSUE_NO")
                                   FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_3
                                   WHERE LIC_VER_SUBQUERY_3."AABL_ID" = NALD_ABS_LIC_VERSIONS."AABL_ID"
                                     AND LIC_VER_SUBQUERY_3."FGAC_REGION_CODE" = NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE"
                                     AND LIC_VER_SUBQUERY_3."STATUS" <> 'DRAFT'
                                 )
                             )
                             AND (@RegionCode IS NULL OR NALD_ABS_LICENCES."FGAC_REGION_CODE" = @RegionCode);
                           """;
        
        var results = await QueryAsync<(string LicenceNumber, short RegionCode)>(
            connection,
            sql,
            0,
            new { RegionCode = regionCode });
        
        return results
            .Select(r => (r.LicenceNumber, (int)r.RegionCode))
            .ToHashSet();
    }
    
    private async Task<HashSet<(string, int)>> GetLapsedLicenceNumbersAsync(NpgsqlConnection connection, short? regionCode)
    {
        const string sql = """
                           SELECT
                             NALD_ABS_LICENCES."LIC_NO",
                             NALD_ABS_LICENCES."FGAC_REGION_CODE"
                           FROM nald."NALD_ABS_LICENCES" NALD_ABS_LICENCES
                           INNER JOIN nald."NALD_ABS_LIC_VERSIONS" NALD_ABS_LIC_VERSIONS
                             ON NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE" = NALD_ABS_LICENCES."FGAC_REGION_CODE"
                             AND NALD_ABS_LIC_VERSIONS."AABL_ID" = NALD_ABS_LICENCES."ID"
                           WHERE
                             NALD_ABS_LICENCES."LAPSED_DATE" < CURRENT_DATE
                             AND NALD_ABS_LIC_VERSIONS."ISSUE_NO" = (
                               SELECT MAX(LIC_VER_SUBQUERY."ISSUE_NO")
                               FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY
                               WHERE LIC_VER_SUBQUERY."AABL_ID" = NALD_ABS_LIC_VERSIONS."AABL_ID"
                                 AND LIC_VER_SUBQUERY."FGAC_REGION_CODE" = NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE"
                                 AND LIC_VER_SUBQUERY."STATUS" <> 'DRAFT'
                             )
                             AND NALD_ABS_LIC_VERSIONS."INCR_NO" = (
                               SELECT MAX(LIC_VER_SUBQUERY_2."INCR_NO")
                               FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_2
                               WHERE LIC_VER_SUBQUERY_2."AABL_ID" = NALD_ABS_LIC_VERSIONS."AABL_ID"
                                 AND LIC_VER_SUBQUERY_2."FGAC_REGION_CODE" = NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE"
                                 AND LIC_VER_SUBQUERY_2."STATUS" <> 'DRAFT'
                                 AND LIC_VER_SUBQUERY_2."ISSUE_NO" = (
                                   SELECT MAX(LIC_VER_SUBQUERY_3."ISSUE_NO")
                                   FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_3
                                   WHERE LIC_VER_SUBQUERY_3."AABL_ID" = NALD_ABS_LIC_VERSIONS."AABL_ID"
                                     AND LIC_VER_SUBQUERY_3."FGAC_REGION_CODE" = NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE"
                                     AND LIC_VER_SUBQUERY_3."STATUS" <> 'DRAFT'
                                 )
                             )
                             AND (@RegionCode IS NULL OR NALD_ABS_LICENCES."FGAC_REGION_CODE" = @RegionCode);
                           """;
        
        var results = await QueryAsync<(string LicenceNumber, short RegionCode)>(
            connection,
            sql,
            0,
            new { RegionCode = regionCode });
        
        return results
            .Select(r => (r.LicenceNumber, (int)r.RegionCode))
            .ToHashSet();
    }

    private async Task<HashSet<(string, int)>> GetExpiredLicenceNumbersAsync(NpgsqlConnection connection, short? regionCode)
    {
        const string sql = """
                           SELECT
                             NALD_ABS_LICENCES."LIC_NO",
                             NALD_ABS_LICENCES."FGAC_REGION_CODE"
                           FROM nald."NALD_ABS_LICENCES" NALD_ABS_LICENCES
                           INNER JOIN nald."NALD_ABS_LIC_VERSIONS" NALD_ABS_LIC_VERSIONS
                             ON NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE" = NALD_ABS_LICENCES."FGAC_REGION_CODE"
                             AND NALD_ABS_LIC_VERSIONS."AABL_ID" = NALD_ABS_LICENCES."ID"
                           WHERE
                               NALD_ABS_LICENCES."EXPIRY_DATE" < CURRENT_DATE
                             AND NALD_ABS_LIC_VERSIONS."ISSUE_NO" = (
                               SELECT MAX(LIC_VER_SUBQUERY."ISSUE_NO")
                               FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY
                               WHERE LIC_VER_SUBQUERY."AABL_ID" = NALD_ABS_LIC_VERSIONS."AABL_ID"
                                 AND LIC_VER_SUBQUERY."FGAC_REGION_CODE" = NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE"
                                 AND LIC_VER_SUBQUERY."STATUS" <> 'DRAFT'
                             )
                             AND NALD_ABS_LIC_VERSIONS."INCR_NO" = (
                               SELECT MAX(LIC_VER_SUBQUERY_2."INCR_NO")
                               FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_2
                               WHERE LIC_VER_SUBQUERY_2."AABL_ID" = NALD_ABS_LIC_VERSIONS."AABL_ID"
                                 AND LIC_VER_SUBQUERY_2."FGAC_REGION_CODE" = NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE"
                                 AND LIC_VER_SUBQUERY_2."STATUS" <> 'DRAFT'
                                 AND LIC_VER_SUBQUERY_2."ISSUE_NO" = (
                                   SELECT MAX(LIC_VER_SUBQUERY_3."ISSUE_NO")
                                   FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_3
                                   WHERE LIC_VER_SUBQUERY_3."AABL_ID" = NALD_ABS_LIC_VERSIONS."AABL_ID"
                                     AND LIC_VER_SUBQUERY_3."FGAC_REGION_CODE" = NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE"
                                     AND LIC_VER_SUBQUERY_3."STATUS" <> 'DRAFT'
                                 )
                             )
                             AND (@RegionCode IS NULL OR NALD_ABS_LICENCES."FGAC_REGION_CODE" = @RegionCode);
                           """;
        
        var results = await QueryAsync<(string LicenceNumber, short RegionCode)>(
            connection,
            sql,
            0,
            new { RegionCode = regionCode });
        
        return results
            .Select(r => (r.LicenceNumber, (int)r.RegionCode))
            .ToHashSet();
    }

    private async Task<HashSet<(string, int)>> GetImpoundmentLicenceNumbersAsync(NpgsqlConnection connection, short? regionCode)
    {
        const string sql = """
                           SELECT
                             NALD_IMP_LICENCES."LIC_NO",
                             NALD_IMP_LICENCES."FGAC_REGION_CODE"
                           FROM
                             nald."NALD_IMP_LICENCES" NALD_IMP_LICENCES
                           WHERE
                             (@RegionCode IS NULL OR NALD_IMP_LICENCES."FGAC_REGION_CODE" = @RegionCode);
                           """;

        var results = await QueryAsync<(string LicenceNumber, short RegionCode)>(
            connection,
            sql,
            0,
            new { RegionCode = regionCode });

        return results
            .Select(r => (r.LicenceNumber, (int)r.RegionCode))
            .ToHashSet();
    }

    public async Task<List<NaldAbstractionLicenceDataLine>> GetNaldAbsLicencesAsync(short? regionCode)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               "ID" AS Id,
                               "LIC_NO" AS LicenceNo,
                               "AREP_SUC_CODE" AS ArepSucCode,
                               "AREP_AREA_CODE" AS ArepAreaCode,
                               "SUSP_FROM_BILLING" AS SuspFromBilling,
                               "AREP_LEAP_CODE" AS ArepLeapCode,
                               "EXPIRY_DATE" AS ExpiryDate,
                               "ORIG_EFF_DATE" AS OrigEffectiveDate,
                               "ORIG_SIG_DATE" AS OrigSignatureDate,
                               "ORIG_APP_NO" AS OrigAppNo,
                               "ORIG_LIC_NO" AS OrigLicNo,
                               "NOTES" AS Notes,
                               "REV_DATE" AS RevDate,
                               "LAPSED_DATE" AS LapsedDate,
                               "SUSP_FROM_RETURNS" AS SuspFromReturns,
                               "AREP_CAMS_CODE" AS ArepCamsCode,
                               "X_REG_IND" AS XRegInd,
                               "PREV_LIC_NO" AS PrevLicNo,
                               "FOLL_LIC_NO" AS FollLicNo,
                               "AREP_EIUC_CODE" AS ArepEiucCode,
                               "FGAC_REGION_CODE" AS FgacRegionCode
                           FROM nald."NALD_ABS_LICENCES"
                           WHERE @RegionCode is null or "FGAC_REGION_CODE" = @RegionCode
                           """;

        // TODO check if this should  filter out to only none-revoked etc...
        
        return (await QueryAsync<NaldAbstractionLicenceDataLine>(
            connection,
            sql,
            0,
            new { RegionCode = regionCode })).ToList();
    }

    public async Task<List<NaldLicenceVersionDataLine>> GetNaldLicenceVersionsAsync(short? regionCode)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               "AABL_ID" AS AablId,
                               "ISSUE_NO" AS IssueNo,
                               "INCR_NO" AS IncrNo,
                               "AABV_TYPE" AS AabvType,
                               "EFF_ST_DATE" AS EffStDate,
                               "STATUS" AS Status,
                               "RETURNS_REQ" AS ReturnsReq,
                               "CHARGEABLE" AS Chargeable,
                               "ASRC_CODE" AS AsrcCode,
                               "ACON_APAR_ID" AS AconAparId,
                               "ACON_AADD_ID" AS AconAaddId,
                               "ALTY_CODE" AS AltyCode,
                               "ACCL_CODE" AS AcclCode,
                               "MULTIPLE_LH" AS MultipleLh,
                               "LIC_SIG_DATE" AS LicSigDate,
                               "APP_NO" AS AppNo,
                               "LIC_DOC_FLAG" AS LicDocFlag,
                               "EFF_END_DATE" AS EffEndDate,
                               "EXPIRY_DATE1" AS ExpiryDate1,
                               "WA_ALTY_CODE" AS WaAltyCode,
                               "VOL_CONV" AS VolConv,
                               "WRT_CODE" AS WrtCode,
                               "DEREG_CODE" AS DeregCode,
                               "FGAC_REGION_CODE" AS FgacRegionCode
                           FROM nald."NALD_ABS_LIC_VERSIONS"
                           WHERE (@RegionCode is null or "FGAC_REGION_CODE" = @RegionCode)
                             AND "ISSUE_NO" = (
                                 SELECT max(lic_ver_subquery."ISSUE_NO")
                                 FROM nald."NALD_ABS_LIC_VERSIONS" lic_ver_subquery
                                 WHERE lic_ver_subquery."AABL_ID" = "NALD_ABS_LIC_VERSIONS"."AABL_ID"
                                   AND lic_ver_subquery."FGAC_REGION_CODE" = "NALD_ABS_LIC_VERSIONS"."FGAC_REGION_CODE"
                                   AND lic_ver_subquery."EFF_ST_DATE" <= CURRENT_TIMESTAMP
                                   AND (lic_ver_subquery."EFF_END_DATE" >= CURRENT_TIMESTAMP OR lic_ver_subquery."EFF_END_DATE" IS NULL)
                                   AND lic_ver_subquery."STATUS" <> 'DRAFT'
                             )
                             AND "INCR_NO" = (
                                 SELECT max(lic_ver_subquery_2."INCR_NO")
                                 FROM nald."NALD_ABS_LIC_VERSIONS" lic_ver_subquery_2
                                 WHERE lic_ver_subquery_2."AABL_ID" = "NALD_ABS_LIC_VERSIONS"."AABL_ID"
                                   AND lic_ver_subquery_2."FGAC_REGION_CODE" = "NALD_ABS_LIC_VERSIONS"."FGAC_REGION_CODE"
                                   AND lic_ver_subquery_2."EFF_ST_DATE" <= CURRENT_TIMESTAMP
                                   AND (lic_ver_subquery_2."EFF_END_DATE" >= CURRENT_TIMESTAMP OR lic_ver_subquery_2."EFF_END_DATE" IS NULL)
                                   AND lic_ver_subquery_2."STATUS" <> 'DRAFT'
                             )
                             AND "WA_ALTY_CODE" IN ('FULL', 'NA', 'TEMP', 'TRAN')
                           """;

        return (await QueryAsync<NaldLicenceVersionDataLine>(
            connection,
            sql,
            0,
            new { RegionCode = regionCode })).ToList();
    }

    public async Task<List<NaldLicencePurposeDataLine>> GetNaldLicencePurposesAsync(short? regionCode)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               p."ID" AS Id,
                               p."AABV_AABL_ID" AS AabvAablId,
                               p."AABV_ISSUE_NO" AS AabvIssueNo,
                               p."AABV_INCR_NO" AS AabvIncrNo,
                               p."APUR_APPR_CODE" AS ApurApprCode,
                               p."APUR_APSE_CODE" AS ApurApseCode,
                               p."APUR_APUS_CODE" AS ApurApusCode,
                               p."PERIOD_ST_DAY" AS PeriodStartDay,
                               p."PERIOD_ST_MONTH" AS PeriodStartMonth,
                               p."PERIOD_END_DAY" AS PeriodEndDay,
                               p."PERIOD_END_MONTH" AS PeriodEndMonth,
                               p."AMOM_CODE" AS AmomCode,
                               p."ANNUAL_QTY" AS AnnualQty,
                               p."ANNUAL_QTY_USABILITY" AS AnnualQtyUsability,
                               p."DAILY_QTY" AS DailyQty,
                               p."DAILY_QTY_USABILITY" AS DailyQtyUsability,
                               p."HOURLY_QTY" AS HourlyQty,
                               p."HOURLY_QTY_USABILITY" AS HourlyQtyUsability,
                               p."INST_QTY" AS InstQty,
                               p."INST_QTY_USABILITY" AS InstQtyUsability,
                               p."TIMELTD_ST_DATE" AS TimeLtdStartDate,
                               p."TIMELTD_END_DATE" AS TimeLtdEndDate,
                               p."LANDS" AS Lands,
                               p."AREC_CODE" AS ArecCode,
                               p."DISP_ORD" AS DispOrd,
                               p."NOTES" AS Notes,
                               p."FGAC_REGION_CODE" AS FgacRegionCode,
                               pp."DESCR" AS PurpPrimDescr,
                               ps."DESCR" AS PurpSecDescr,
                               pu."DESCR" AS PurpUseDescr
                           FROM nald."NALD_ABS_LIC_PURPOSES" p
                           JOIN nald."NALD_PURP_PRIMS" pp
                               ON p."APUR_APPR_CODE" = pp."CODE"
                           JOIN nald."NALD_PURP_SECS" ps
                               ON p."APUR_APSE_CODE" = ps."CODE"
                           JOIN nald."NALD_PURP_USES" pu
                               ON p."APUR_APUS_CODE" = pu."CODE"
                           WHERE @RegionCode is null or p."FGAC_REGION_CODE" = @RegionCode
                           """;

        return (await QueryAsync<NaldLicencePurposeDataLine>(
            connection,
            sql,
            0,
            new { RegionCode = regionCode })).ToList();
    }

    public async Task<List<NaldLicencePointDataLine>> GetNaldLicencePointsAsync(short? regionCode)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               pp."AABP_ID" AS AabpId,
                               pp."AAIP_ID" AS AaipId,
                               pp."AMOA_CODE" AS AmoaCode,
                               pp."NOTES" AS Notes,
                               pp."FGAC_REGION_CODE" AS FgacRegionCode,
                               p."NGR1_SHEET" AS Ngr1Sheet,
                               p."NGR1_EAST" AS Ngr1East,
                               p."NGR1_NORTH" AS Ngr1North,
                               p."CART1_EAST" AS Cart1East,
                               p."CART1_NORTH" AS Cart1North,
                               p."LOCAL_NAME" AS LocalName,
                               p."ASRC_CODE" AS AsrcCode,
                               p."DISABLED" AS Disabled,
                               p."LOCAL_NAME_WELSH" AS LocalNameWelsh,
                               p."NGR2_SHEET" AS Ngr2Sheet,
                               p."NGR2_EAST" AS Ngr2East,
                               p."NGR2_NORTH" AS Ngr2North,
                               p."CART2_EAST" AS Cart2East,
                               p."CART2_NORTH" AS Cart2North,
                               p."NGR3_SHEET" AS Ngr3Sheet,
                               p."NGR3_EAST" AS Ngr3East,
                               p."NGR3_NORTH" AS Ngr3North,
                               p."CART3_EAST" AS Cart3East,
                               p."CART3_NORTH" AS Cart3North,
                               p."NGR4_SHEET" AS Ngr4Sheet,
                               p."NGR4_EAST" AS Ngr4East,
                               p."NGR4_NORTH" AS Ngr4North,
                               p."CART4_EAST" AS Cart4East,
                               p."CART4_NORTH" AS Cart4North,
                               p."AAPC_CODE" AS AapcCode,
                               p."AAPT_APTP_CODE" AS AaptAptpCode,
                               p."AAPT_APTS_CODE" AS AaptAptsCode,
                               p."ABAN_CODE" AS AbanCode,
                               p."LOCATION_TEXT" AS LocationText,
                               p."AADD_ID" AS AaddId,
                               p."DEPTH" AS Depth,
                               p."WRB_NO" AS WrbNo,
                               p."BGS_NO" AS BgsNo,
                               p."REG_WELL_INDEX_REF" AS RegWellIndexRef,
                               p."HYDRO_REF" AS HydroRef,
                               p."HYDRO_INTERCEPT_DIST" AS HydroInterceptDist,
                               p."HYDRO_GW_OFFSET_DIST" AS HydroGwOffsetDist,
                               p."NOTES" AS PointNotes
                           FROM nald."NALD_ABS_PURP_POINTS" pp
                           JOIN nald."NALD_POINTS" p
                               ON pp."AAIP_ID" = p."ID"
                               AND pp."FGAC_REGION_CODE" = p."FGAC_REGION_CODE"
                           WHERE @RegionCode is null or pp."FGAC_REGION_CODE" = @RegionCode
                           """;

        return (await QueryAsync<NaldLicencePointDataLine>(
            connection,
            sql,
            0,
            new { RegionCode = regionCode })).ToList();
    }

    public async Task<List<NaldLicenceQuantitiesDataLine>> GetNaldLicenceQuantitiesAsync(short? regionCode)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               "ID" AS Id,
                               "AABV_AABL_ID" AS AabvAablId,
                               "AABV_ISSUE_NO" AS AabvIssueNo,
                               "AABV_INCR_NO" AS AabvIncrNo,
                               "MAX_ANNUAL_QTY" AS MaxAnnualQty,
                               "MAX_DAILY_QTY" AS MaxDailyQty,
                               "AGGREGATED_IND" AS AggregatedInd,
                               "PURP_POINTS_IND" AS PurpPointsInd,
                               "USER_VALID_IND" AS UserValidInd,
                               "FGAC_REGION_CODE" AS FgacRegionCode
                           FROM nald."NALD_ABS_LIC_QUANTITIES"
                           WHERE @RegionCode is null or "FGAC_REGION_CODE" = @RegionCode
                           """;

        return (await QueryAsync<NaldLicenceQuantitiesDataLine>(
            connection,
            sql,
            0,
            new { RegionCode = regionCode })).ToList();
    }

    public async Task<Licence?> GetNewestLicenceAsync(string permitNumber)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               data,
                               licence_id 
                           FROM licence
                           WHERE permit_number = @PermitNumber
                           ORDER BY process_run_id DESC
                           LIMIT 1;
                           """;
        
        var result =  await QuerySingleOrDefaultAsync<(string Data, int LicenceId)?>(
            connection,
            sql,
            0,
            new { PermitNumber = permitNumber });
        if (result == null)
        {
            return null;
        }
        
        var data = JsonSerializer.Deserialize<Licence>(result.Value.Data, GetSerializerOptions())!;
        data.NoneSchemaData.TryAdd("licenceId", result.Value.LicenceId);
        return data;
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
            NpgsqlDataSourceProvider.Queries.Add((thisQueryNumber, sql));
            
            var result = await connection.QuerySingleOrDefaultAsync<T>(sql, param);
            var duration =  DateTime.Now - dtStart;

            if (duration.TotalSeconds > 1)
            {
                ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresReadService)} - Query {thisQueryNumber} - {sql.Replace("\n", " ")} took {duration.TotalMilliseconds}ms");
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
            return await QuerySingleOrDefaultAsync<T>(GetPostgresConnection(), sql, retryNumber + 1, param);
        }
    }
    
    private async Task<IEnumerable<T>> QueryAsync<T>(NpgsqlConnection connection, string sql, int retryNumber, object? param = null)
    {
        try
        {
            var dtStart = DateTime.Now;
            var thisQueryNumber = NpgsqlDataSourceProvider.QueryNumber++;
            NpgsqlDataSourceProvider.Queries.Add((thisQueryNumber, sql));
            
            var result = await connection.QueryAsync<T>(sql, param);
            var duration =  DateTime.Now - dtStart;

            if (duration.TotalSeconds > 1)
            {
                ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresReadService)} - Query {thisQueryNumber} - {sql.Replace("\n", " ")} took {duration.TotalMilliseconds}ms");
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
            return await QueryAsync<T>(GetPostgresConnection(), sql, retryNumber + 1, param);
        }
    }

    private NpgsqlConnection GetPostgresConnection()
    {
        var dtStart = DateTime.Now;
        
        var conn = dataSourceProvider.DataSource.OpenConnection();
        var duration =  DateTime.Now - dtStart;

        if (duration.TotalSeconds > 1)
        {
            ConsoleHelper.WriteLine($"WARNING - {nameof(PostgresReadService)} - OpenConnection took {duration.TotalMilliseconds}ms");
        }

        return conn;
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
}