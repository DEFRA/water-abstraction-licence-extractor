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

    public async Task<List<LicenceListItemAggregate>>
        GetLicencesListSearchAsync(
            int processRunId,
            ProcessRunQuery query,
            CancellationToken cancellationToken = default)
    {
        await using var connection = GetPostgresConnection();

        var parents = await GetLicenceListItemParentsAsync(
            connection,
            processRunId,
            query,
            cancellationToken);

        if (parents.Count == 0)
        {
            return [];
        }

        var itemIds = parents
            .Select(x => x.LicenceListItemId)
            .ToArray();

        var linkedLicences = await GetLinkedLicencesAsync(
            connection,
            processRunId,
            itemIds,
            cancellationToken);

        var licenceSets = await GetLicenceSetsAsync(
            connection,
            processRunId,
            itemIds,
            cancellationToken);
        
        var verificationSections =
            await GetLicenceVerificationSectionsAsync(
                connection,
                processRunId,
                itemIds,
                cancellationToken);
        
        return parents
            .Select(parent => new LicenceListItemAggregate
            {
                Licence = parent,
                
                LinkedLicences = linkedLicences.TryGetValue(
                    parent.LicenceListItemId,
                    out var itemLinkedLicences)
                    ? itemLinkedLicences.ToList()
                    : [],

                LicenceSets = licenceSets.TryGetValue(
                    parent.LicenceListItemId,
                    out var itemLicenceSets)
                    ? itemLicenceSets.ToList()
                    : [],
                
                VerificationSections =
                    verificationSections.TryGetValue(
                        parent.LicenceListItemId,
                        out var itemVerificationSections)
                        ? itemVerificationSections.ToList()
                        : []
            })
            .ToList();
    }

    public async Task<List<string>> GetLicenceListDistinctIssuersAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql =
            """
            SELECT DISTINCT issuer
            FROM licence_list_item
            WHERE process_run_id = @ProcessRunId
              AND NULLIF(trim(issuer), '') IS NOT NULL
            ORDER BY issuer;
            """;

        var results=  await QueryAsync<string>(
            connection,
            sql,
            0,
            new
            {
                @ProcessRunId =  processRunId
            });
        
        return results.ToList();
    }

    public async Task<List<string>> GetLicenceListLicenceSetIdsAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql =
            """
            SELECT DISTINCT 
                   short_licence_set_id        
            FROM licence_list_item_licence_set
            where process_run_id = @ProcessRunId
            order by short_licence_set_id
            """;

        var results=  await QueryAsync<string>(
            connection,
            sql,
            0,
            new
            {
                @ProcessRunId =  processRunId
            });
        
        return results.ToList();
    }

    public async Task<List<string>> GetLicenceListIssueYearsAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql =
            """
            SELECT DISTINCT issue_year
            FROM licence_list_item
            WHERE process_run_id = @ProcessRunId
            and  issue_year IS NOT NULL
            ORDER BY issue_year;
            """;

        var results=  await QueryAsync<string>(
            connection,
            sql,
            0,
            new
            {
                @ProcessRunId =  processRunId
            });
        
        return results.ToList();
    }

private static async Task<
        Dictionary<long, List<LicenceSectionVerificationSummary>>>
    GetLicenceVerificationSectionsAsync(
        NpgsqlConnection connection,
        int processRunId,
        IReadOnlyCollection<long> licenceListItemIds,
        CancellationToken cancellationToken)
{
    if (licenceListItemIds.Count == 0)
    {
        return [];
    }

    const string sql =
        """
        SELECT
            verification_section.licence_list_item_id
                AS LicenceListItemId,

            verification_section.verification_section_id
                AS VerificationSectionId,

            verification_section.licence_section_name
                AS LicenceSectionName,

            verification_item.verification_item_id
                AS VerificationItemId,

            verification_item.licence_section_item_id
                AS LicenceSectionItemId,

            verification_item.verification_types
                AS VerificationTypes,

            verification_item.scraped_data_is_different
                AS ScrapedDataIsDifferent

        FROM licence_list_item_verification_section
            AS verification_section

        LEFT JOIN licence_list_item_verification_item
            AS verification_item
            ON verification_item.verification_section_id =
               verification_section.verification_section_id

        WHERE verification_section.process_run_id = @ProcessRunId
          AND verification_section.licence_list_item_id =
              ANY(@LicenceListItemIds)

        ORDER BY
            verification_section.licence_list_item_id,
            verification_section.verification_section_id,
            verification_item.verification_item_id;
        """;

    var rows = await connection.QueryAsync<VerificationSectionRow>(
        new CommandDefinition(
            sql,
            new
            {
                ProcessRunId = processRunId,
                LicenceListItemIds = licenceListItemIds.ToArray()
            },
            cancellationToken: cancellationToken));

    return rows
        .GroupBy(row => row.LicenceListItemId)
        .ToDictionary(
            itemGroup => itemGroup.Key,
            itemGroup => itemGroup
                .GroupBy(row => new
                {
                    row.VerificationSectionId,
                    row.LicenceSectionName
                })
                .Select(sectionGroup =>
                    new LicenceSectionVerificationSummary
                    {
                        LicenceSectionName =
                            sectionGroup.Key.LicenceSectionName,

                        LicenceSectionItems = sectionGroup
                            .Where(row =>
                                row.VerificationItemId.HasValue &&
                                !string.IsNullOrWhiteSpace(
                                    row.LicenceSectionItemId))
                            .Select(row =>
                                new LicenceSectionItemSummary
                                {
                                    LicenceSectionItemId =
                                        row.LicenceSectionItemId!,

                                    VerificationTypes =
                                        row.VerificationTypes ?? [],

                                    ScrapedDataIsDifferent =
                                        row.ScrapedDataIsDifferent
                                })
                            .ToArray()
                    })
                .ToList());
}


    private static async Task<
            Dictionary<long, IReadOnlyList<LicenceListItemLicenceSet>>>
        GetLicenceSetsAsync(
            NpgsqlConnection connection,
            int processRunId,
            long[] itemIds,
            CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT
                s.licence_list_item_licence_set_id
                    AS LicenceListItemLicenceSetId,
                s.licence_list_item_id
                    AS LicenceListItemId,
                s.licence_set_id
                    AS LicenceSetId,
                s.process_run_id
                    AS ProcessRunId,
                s.short_licence_set_id
                    AS ShortLicenceSetId,
                s.licence_set_type
                    AS LicenceSetType,
                t.licence_list_item_licence_set_id
                    AS LicenceListItemLicenceSetId,
                t.licence_set_type
                    AS LicenceSetType
            FROM licence_list_item_licence_set s
            LEFT JOIN licence_list_item_licence_set_type t
                ON t.licence_list_item_licence_set_id = s.licence_list_item_licence_set_id
            WHERE s.process_run_id = @ProcessRunId
              AND s.licence_list_item_id = ANY(@ItemIds)
            ORDER BY s.licence_list_item_id,
                     s.licence_list_item_licence_set_id;
            """;

        var lookup = new Dictionary<long, LicenceListItemLicenceSet>();

        await connection
            .QueryAsync<LicenceListItemLicenceSet, LicenceListItemLicenceSetType, LicenceListItemLicenceSet>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        ProcessRunId = processRunId,
                        ItemIds = itemIds
                    },
                    cancellationToken: cancellationToken),
                (set, type) =>
                {
                    if (!lookup.TryGetValue(set.LicenceListItemLicenceSetId, out var existing))
                    {
                        existing = set;
                        lookup[set.LicenceListItemLicenceSetId] = existing;
                    }

                    if (type != null)
                    {
                        existing.LicenceSetTypes.Add(type);
                    }

                    return existing;
                },
                splitOn: "LicenceListItemLicenceSetId");

        return lookup.Values
            .GroupBy(x => x.LicenceListItemId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IReadOnlyList<LicenceListItemLicenceSet>)
                    group.ToArray());
    }

    private static async Task<
            Dictionary<long, IReadOnlyList<LicenceListItemLinkedLicence>>>
        GetLinkedLicencesAsync(
            NpgsqlConnection connection,
            int processRunId,
            long[] itemIds,
            CancellationToken cancellationToken)
    {
        const string linkedSql =
            """
            SELECT
                linked_licence_id
                    AS LinkedLicenceId,
                licence_list_item_id
                    AS LicenceListItemId,
                licence_number
                    AS LicenceNumber,
                raw_scraped_licence_number
                    AS RawScrapedLicenceNumber,
                dms_permit_number
                    AS DmsPermitNumber,
                dms_path
                    AS DmsPath,
                dms_file_id
                    AS DmsFileId,
                filename
                    AS Filename,
                licence_version_id
            AS LicenceVersionId,
                issue_date
            AS IssueDate,
                expiry_date
            AS ExpiryDate,
            original_issue_date
            AS OriginalIssueDate,
            issuer,
            nald_status
            AS NaldStatus,
                licence_type
            As LicenceType,
                region_id
            As RegionId,
                condition_data AS ConditionData,
            nald_revocation_date AS NaldRevocationDate,
            nald_expiry_date AS NaldExpiryDate,
            nald_orig_effective_date AS NaldOrigEffectiveDate,
            nald_orig_signature_date AS NaldOrigSignatureDate,
            nald_signature_date AS NaldSignatureDate,
            nald_effective_start_date AS NaldEffectiveStartDate,
            nald_effective_end_date AS NaldEffectiveEndDate,
            nald_issue_number AS NaldIssueNumber,
            nald_increment_number AS NaldIncrementNumber,
            nald_update_reason AS NaldUpdateReason,
            dms_file_id_status_date_utc As DmsFileIdStatusDateUtc,
            dms_file_id_status As DmsFileIdStatus,
            effective_date As EffectiveDate,
            source_data AS SourceData,
            licence_version_nald_status AS  LicenceVersionNaldStatus
            FROM licence_list_item_linked_licence
              where licence_list_item_id = ANY(@ItemIds)
            ORDER BY licence_list_item_id,
                     linked_licence_id;
            """;

        var linkedRows =
            (await connection.QueryAsync<LicenceListItemLinkedLicence>(
                new CommandDefinition(
                    linkedSql,
                    new
                    {
                        ItemIds = itemIds
                    },
                    cancellationToken: cancellationToken)))
            .ToList();

        if (linkedRows.Count == 0)
        {
            return [];
        }

        var linkedLicenceIds = linkedRows
            .Select(x => x.LinkedLicenceId)
            .ToArray();

        const string locationSql =
            """
            SELECT
                linked_licence_id
                    AS LinkedLicenceId,
                direction
                    AS Direction,
                section_name
                    AS SectionName,
                link_reason
                   AS LinkReason,
                page_number
                    AS PageNumber,
                line_number
                    AS LineNumber,
                source
            AS Source,
                link_location_id
            AS LinkLocationId,
                is_because_of_aggregate
            AS IsBecauseOfAggregate
            FROM licence_list_item_link_location
              where linked_licence_id =
                  ANY(@LinkedLicenceIds)
            ORDER BY linked_licence_id;
            """;

        var locations =
            await connection.QueryAsync<LicenceListItemLinkLocation>(
                new CommandDefinition(
                    locationSql,
                    new
                    {
                        LinkedLicenceIds = linkedLicenceIds
                    },
                    cancellationToken: cancellationToken));

        var locationsByLinkedLicenceId = locations
            .GroupBy(x => x.LinkedLicenceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(x => new LicenceListItemLinkLocation
                    {
                        Direction = x.Direction,
                        SectionName = x.SectionName,
                        PageNumber = x.PageNumber,
                        LineNumber = x.LineNumber,
                        IsBecauseOfAggregate =  x.IsBecauseOfAggregate,
                        Source =  x.Source,
                        LinkReason = x.LinkReason,
                        LinkedLicenceId =  x.LinkedLicenceId,
                        LinkLocationId =   x.LinkLocationId,
                    })
                    .ToArray());

        foreach (var linkedLicence in linkedRows)
        {
            linkedLicence.Locations =
                locationsByLinkedLicenceId.TryGetValue(
                    linkedLicence.LinkedLicenceId,
                    out var linkedLocations)
                    ? linkedLocations.ToList()
                    : [];
        }

        return linkedRows
            .GroupBy(x => x.LicenceListItemId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IReadOnlyList<LicenceListItemLinkedLicence>)
                    group.ToArray());
    }
    
    
    private static async Task<List<LicenceListItem>>
    GetLicenceListItemParentsAsync(
        NpgsqlConnection connection,
        int processRunId,
        ProcessRunQuery query,
        CancellationToken cancellationToken)
{
    var sql = new StringBuilder(
        """
        SELECT
            licence_list_item_id AS LicenceListItemId,
            process_run_id AS ProcessRunId,
            file_id AS FileId,
            filename AS Filename,
            licence_number AS LicenceNumber,
            licence_holder AS LicenceHolder,
            limits_count AS LimitsCount,
            aggregates_count AS AggregatesCount,
            ocr AS Ocr,
            issue_date AS IssueDate,
            issue_year AS IssueYear,
            issuer AS Issuer,
            means_found AS MeansFound,
            status AS Status,
            purposes_count AS PurposesCount,
            points_count AS PointsCount,
            purposes AS Purposes,
            points AS Points,
            linked_licences_count AS LinkedLicencesCount,
            licence_sets_count AS LicenceSetsCount,
            verification_sections_count AS VerificationSectionsCount,
            verification_items_count AS VerificationItemsCount,
            has_verifications AS HasVerifications,
            created_date_time_utc AS CreatedDateTimeUtc,
            updated_date_time_utc AS UpdatedDateTimeUtc
        FROM licence_list_item
        WHERE process_run_id = @ProcessRunId
          AND status = 'Live'
        """);

    var parameters = new DynamicParameters();

    parameters.Add("ProcessRunId", processRunId);
    parameters.Add("Skip", query.Skip);
    parameters.Add("Take", query.Take);

    AddLicenceListItemFilters(
        sql,
        parameters,
        query);

    ReadSqlHelper.AddOrdering(
        sql,
        query.SortField,
        query.SortAscending);

    sql.AppendLine(
        """
        LIMIT @Take
        OFFSET @Skip;
        """);

    var results = await connection.QueryAsync<LicenceListItem>(
        new CommandDefinition(
            sql.ToString(),
            parameters,
            cancellationToken: cancellationToken));

    return results.ToList();
}

private static async Task<int>
    GetLicenceListItemParentsCountAsync(
        NpgsqlConnection connection,
        int processRunId,
        ProcessRunQuery query,
        CancellationToken cancellationToken)
{
    var sql = new StringBuilder(
        """
        SELECT COUNT(*)
        FROM licence_list_item
        WHERE process_run_id = @ProcessRunId
          AND status = 'Live'
        """);

    var parameters = new DynamicParameters();

    parameters.Add("ProcessRunId", processRunId);

    AddLicenceListItemFilters(
        sql,
        parameters,
        query);

    return await connection.ExecuteScalarAsync<int>(
        new CommandDefinition(
            sql.ToString(),
            parameters,
            cancellationToken: cancellationToken));
}

private static void AddLicenceListItemFilters(
    StringBuilder sql,
    DynamicParameters parameters,
    ProcessRunQuery query)
{
    ReadSqlHelper.AddSearchTermFilter(
        sql,
        parameters,
        query.SearchTermClean);

    ReadSqlHelper.AddStringFilter(
        sql,
        parameters,
        "issuer",
        "Issuer",
        query.Issuer);

    ReadSqlHelper.AddBooleanFilter(
        sql,
        parameters,
        "ocr",
        "OcrScan",
        query.OcrScan);

    ReadSqlHelper.AddBooleanFilter(
        sql,
        parameters,
        "means_found",
        "MeansFound",
        query.MeansFound);

    ReadSqlHelper.AddCountEmptyFilter(
        sql,
        query.PointsEmpty,
        "points_count");

    ReadSqlHelper.AddCountEmptyFilter(
        sql,
        query.PurposesEmpty,
        "purposes_count");

    ReadSqlHelper.AddCountEmptyFilter(
        sql,
        query.LimitsEmpty,
        "limits_count");

    ReadSqlHelper.AddCountEmptyFilter(
        sql,
        query.AggregatesEmpty,
        "aggregates_count");

    ReadSqlHelper.AddIssueYearFilter(
        sql,
        parameters,
        query.IssueYear?.ToString());

    ReadSqlHelper.AddLinkedLicencesTypeFilter(
        sql,
        parameters,
        query.LinkedLicencesType);

    ReadSqlHelper.AddLicenceSetFilter(
        sql,
        parameters,
        query.ShortLicenceSetId);

    ReadSqlHelper.AddVerificationTypeFilter(
        sql,
        parameters,
        query.VerificationType);
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