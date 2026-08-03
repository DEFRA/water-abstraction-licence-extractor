using System.Text.Json;
using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay.DTOs;

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
    
    
    public async Task<long> UpsertLicenceListItemAsync(
        UpsertLicenceListItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        ValidateItem(item);

        await using var connection = GetPostgresConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var licenceListItemId =
                await UpsertLicenceListItemInternalAsync(
                    connection,
                    transaction,
                    item,
                    cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return licenceListItemId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    
    public async Task<IReadOnlyCollection<long>>
        UpsertLicenceListItemManyAsync(
            IReadOnlyCollection<UpsertLicenceListItem> items,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return Array.Empty<long>();
        }

        foreach (var item in items)
        {
            ValidateItem(item);
        }

        await using var connection = GetPostgresConnection();

        await connection.OpenAsync(cancellationToken);
        
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        var savedIds = new List<long>(items.Count);

        try
        {
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var licenceListItemId =
                    await UpsertLicenceListItemInternalAsync(
                        connection,
                        transaction,
                        item,
                        cancellationToken);

                savedIds.Add(licenceListItemId);
            }

            await transaction.CommitAsync(cancellationToken);

            return savedIds;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    
    private static async Task<long>
        UpsertLicenceListItemInternalAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            UpsertLicenceListItem item,
            CancellationToken cancellationToken)
    {
        var summary = CreateSummary(item);

        var licenceListItemId =
            await UpsertParentAsync(
                connection,
                transaction,
                item,
                summary,
                cancellationToken);
        

        await ReplaceLinkedLicencesAsync(
            connection,
            transaction,
            licenceListItemId,
            item.LinkedLicences,
            cancellationToken);

        await ReplaceLicenceSetsAsync(
            connection,
            transaction,
            item.ProcessRunId,
            licenceListItemId,
            item.LicenceSets,
            cancellationToken);

        await UpsertVerificationsAsync(
            connection,
            transaction,
            item.ProcessRunId,
            licenceListItemId,
            item.LicenceSectionVerifications,
            cancellationToken);


        return licenceListItemId;
    }

    private static async Task<long> UpsertParentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        UpsertLicenceListItem item,
        LicenceListItemSummary summary,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO licence_list_item
            (
                process_run_id,
                file_id,
                filename,
                licence_number,
                licence_holder,
                limits_count,
                aggregates_count,
                ocr,
                issue_date,
                issue_year,
                issuer,
                means_found,
                status,
                purposes_count,
                points_count,
                purposes,
               points,
                linked_licences_count,
                licence_sets_count,
                verification_sections_count,
                verification_items_count,
                has_verifications,
                search_text,
                source_data,
                created_date_time_utc,
                updated_date_time_utc
            )
            VALUES
            (
                @ProcessRunId,
                @FileId,
                @Filename,
                @LicenceNumber,
                @LicenceHolder,
                @LimitsCount,
                @AggregatesCount,
                @Ocr,
                @IssueDate,
                @IssueYear,
                @Issuer,
                @MeansFound,
                @Status,
                @PurposesCount,
                @PointsCount,
             @Purposes,
             @Points,
                @LinkedLicencesCount,
                @LicenceSetsCount,
                @VerificationSectionsCount,
                @VerificationItemsCount,
                @HasVerifications,
                @SearchText,
                CAST(@SourceData AS jsonb),
                NOW(),
                NOW()
            )
            ON CONFLICT
            (
                process_run_id,
                file_id,
                licence_number
            )
            DO UPDATE SET
                filename = EXCLUDED.filename,
                licence_holder = EXCLUDED.licence_holder,
                limits_count = EXCLUDED.limits_count,
                aggregates_count = EXCLUDED.aggregates_count,
                ocr = EXCLUDED.ocr,
                issue_date = EXCLUDED.issue_date,
                issue_year = EXCLUDED.issue_year,
                issuer = EXCLUDED.issuer,
                means_found = EXCLUDED.means_found,
                status = EXCLUDED.status,
                purposes_count = EXCLUDED.purposes_count,
                points_count = EXCLUDED.points_count,
                purposes = EXCLUDED.purposes,
               points = EXCLUDED.points,
                linked_licences_count =
                    EXCLUDED.linked_licences_count,
                licence_sets_count =
                    EXCLUDED.licence_sets_count,
                verification_sections_count =
                    EXCLUDED.verification_sections_count,
                verification_items_count =
                    EXCLUDED.verification_items_count,
                has_verifications =
                    EXCLUDED.has_verifications,
                search_text = EXCLUDED.search_text,
                source_data = EXCLUDED.source_data,
                updated_date_time_utc = NOW()
            RETURNING licence_list_item_id;
            """;

        var parameters = new
        {
            item.ProcessRunId,
            item.FileId,
            item.Filename,
            item.LicenceNumber,
            item.LicenceHolder,
            item.LimitsCount,
            item.AggregatesCount,
            item.Ocr,
            IssueDate = ToDateTime(item.IssueDate),
            IssueYear = item.IssueDate?.Year,
            item.Issuer,
            item.MeansFound,
            item.Status,
            summary.PurposesCount,
            summary.PointsCount,
            item.Purposes,
            item.Points,
            summary.LinkedLicencesCount,
            summary.LicenceSetsCount,
            summary.VerificationSectionsCount,
            summary.VerificationItemsCount,
            summary.HasVerifications,
            summary.SearchText,
            SourceData = NullIfWhiteSpace(item.SourceData)
        };

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                sql,
                parameters,
                transaction,
                cancellationToken: cancellationToken));
    }
    
    private static DateTime? ToDateTime(DateOnly? value)
    {
        return value?.ToDateTime(TimeOnly.MinValue);
    }

    private static DateTime ToDateTime(DateOnly value)
    {
        return value.ToDateTime(TimeOnly.MinValue);
    }
    
    private static async Task ReplaceLinkedLicencesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long licenceListItemId,
        IReadOnlyCollection<UpsertLinkedLicenceItem> linkedLicences,
        CancellationToken cancellationToken)
    {
        const string deleteSql =
            """
            DELETE FROM licence_list_item_linked_licence
            WHERE licence_list_item_id = @LicenceListItemId;
            """;

        await connection.ExecuteAsync(
            new CommandDefinition(
                deleteSql,
                new
                {
                    LicenceListItemId = licenceListItemId
                },
                transaction,
                cancellationToken: cancellationToken));

        foreach (var linkedLicence in linkedLicences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var linkedLicenceId =
                await InsertLinkedLicenceAsync(
                    connection,
                    transaction,
                    licenceListItemId,
                    linkedLicence,
                    cancellationToken);

            await InsertLinkLocationsAsync(
                connection,
                transaction,
                linkedLicenceId,
                linkedLicence.ContainedIn,
                cancellationToken);
        }
    }

    private static async Task<long> InsertLinkedLicenceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long licenceListItemId,
        UpsertLinkedLicenceItem linkedLicence,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO licence_list_item_linked_licence
            (
                licence_list_item_id,
                licence_number,
                raw_scraped_licence_number,
                dms_permit_number,
                dms_path,
                dms_file_id,
                filename,
                licence_version_id,
                effective_date,
                expiry_date,
                issue_date,
                original_issue_date,
                issuer,
                nald_status,
                licence_type,
                region_id,
                condition_data,
                nald_revocation_date,
                nald_expiry_date,
                nald_orig_effective_date,
                nald_orig_signature_date,
                nald_signature_date,
                nald_effective_start_date,
                nald_effective_end_date,
                nald_issue_number,
                nald_increment_number,
                nald_update_reason,
                dms_file_id_status,
                dms_file_id_status_date_utc,
                licence_version_nald_status,
                source_data
            )
            VALUES
            (
                @LicenceListItemId,
                @LicenceNumber,
                @RawScrapedLicenceNumber,
                @DmsPermitNumber,
                @DmsPath,
                @DmsFileId,
                @Filename,
                @LicenceVersionId,
                @EffectiveDate,
                @ExpiryDate,
                @IssueDate,
                @OriginalIssueDate,
                @Issuer,
                @NaldStatus,
                @LicenceType,
                @RegionId,
                CAST(@ConditionData AS jsonb),
                @NaldRevocationDate,
                @NaldExpiryDate,
                @NaldOrigEffectiveDate,
                @NaldOrigSignatureDate,
                @NaldSignatureDate,
                @NaldEffectiveStartDate,
                @NaldEffectiveEndDate,
                @NaldIssueNumber,
                @NaldIncrementNumber,
                @NaldUpdateReason,
              @DmsFileIdStatus,
             @DmsFileIdStatusDateUtc,
             @LicenceVersionNaldStatus,
               CAST(@SourceData AS jsonb)
            )
            RETURNING linked_licence_id;
            """;

        var parameters = new
        {
            LicenceListItemId = licenceListItemId,
            linkedLicence.LicenceNumber,
            linkedLicence.RawScrapedLicenceNumber,
            linkedLicence.DmsPermitNumber,
            linkedLicence.DmsFileId,
            linkedLicence.Filename,
            linkedLicence.DmsPath,
            linkedLicence.LicenceVersionId,
            EffectiveDate = ToDateTime(linkedLicence.EffectiveDate),
            ExpiryDate = ToDateTime(linkedLicence.ExpiryDate),
            IssueDate = ToDateTime(linkedLicence.IssueDate),
            OriginalIssueDate = ToDateTime(linkedLicence.OriginalIssueDate),
            linkedLicence.Issuer,
            linkedLicence.NaldStatus,
            linkedLicence.LicenceType,
            linkedLicence.RegionId,
            ConditionData =
                NullIfWhiteSpace(linkedLicence.ConditionData),
            linkedLicence.NaldRevocationDate,
            linkedLicence.NaldExpiryDate,
            linkedLicence.NaldOrigEffectiveDate,
            linkedLicence.NaldOrigSignatureDate,
            linkedLicence.NaldSignatureDate,
            linkedLicence.NaldEffectiveStartDate,
            linkedLicence.NaldEffectiveEndDate,
            linkedLicence.NaldIssueNumber,
            linkedLicence.NaldIncrementNumber,
            linkedLicence.NaldUpdateReason,
            linkedLicence.DmsFileIdStatus,
            linkedLicence.DmsFileIdStatusDateUtc,
            linkedLicence.LicenceVersionNaldStatus,
            SourceData =
                NullIfWhiteSpace(linkedLicence.SourceData)
        };

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                sql,
                parameters,
                transaction,
                cancellationToken: cancellationToken));
    }

    private static async Task InsertLinkLocationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long linkedLicenceId,
        IReadOnlyCollection<UpsertContainedInInformation> locations,
        CancellationToken cancellationToken)
    {
        var values = locations
            .Select(location => new
            {
                LinkedLicenceId = linkedLicenceId,
                location.Source,
                location.Direction,
                location.SectionName,
                location.LinkReason,
                location.IsBecauseOfAggregate,
                location.LineNumber,
                location.PageNumber
            })
            .ToArray();

        if (values.Length == 0)
        {
            return;
        }

        const string sql =
            """
            INSERT INTO licence_list_item_link_location
            (
                linked_licence_id,
                source,
                direction,
                section_name,
                link_reason,
                is_because_of_aggregate,
                line_number,
                page_number
            )
            VALUES
            (
                @LinkedLicenceId,
                @Source,
                @Direction,
                @SectionName,
                @LinkReason,
                @IsBecauseOfAggregate,
                @LineNumber,
                @PageNumber
            );
            """;

        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                values,
                transaction,
                cancellationToken: cancellationToken));
    }

    private static async Task ReplaceLicenceSetsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int processRunId,
        long licenceListItemId,
        IReadOnlyCollection<UpsertLicenceSetItem> licenceSets,
        CancellationToken cancellationToken)
    {
        const string deleteSql =
            """
            DELETE FROM licence_list_item_licence_set
            WHERE licence_list_item_id = @LicenceListItemId;
            """;

        await connection.ExecuteAsync(
            new CommandDefinition(
                deleteSql,
                new
                {
                    LicenceListItemId = licenceListItemId
                },
                transaction,
                cancellationToken: cancellationToken));

        var distinctLicenceSets = licenceSets
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.LicenceSetId))
            .GroupBy(
                x => x.LicenceSetId?.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Key != null)
            .Select(MergeLicenceSets!)
            .ToArray();

        foreach (var licenceSet in distinctLicenceSets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var licenceSetRowId =
                await InsertLicenceSetAsync(
                    connection,
                    transaction,
                    processRunId,
                    licenceListItemId,
                    licenceSet,
                    cancellationToken);

            await InsertLicenceSetTypesAsync(
                connection,
                transaction,
                licenceSetRowId,
                licenceSet.LicenceSetTypes,
                cancellationToken);
        }
    }
    private static UpsertLicenceSetItem MergeLicenceSets(
        IGrouping<string, UpsertLicenceSetItem> group)
    {
        var first = group.First();

        return new UpsertLicenceSetItem
        {
            LicenceSetId = group.Key,

            ShortLicenceSetId = group
                .Select(x => x.ShortLicenceSetId)
                .FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x)),

            LicenceSetType = group
                .Select(x => x.LicenceSetType)
                .FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x)),

            LicenceSetTypes = group
                .SelectMany(x => x.LicenceSetTypes ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }
    
    
    private static async Task<long> InsertLicenceSetAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int processRunId,
        long licenceListItemId,
        UpsertLicenceSetItem licenceSet,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO licence_list_item_licence_set
            (
                process_run_id,
                licence_list_item_id,
                licence_set_id,
                short_licence_set_id,
                licence_set_type
            )
            VALUES
            (
                @ProcessRunId,
                @LicenceListItemId,
                @LicenceSetId,
                @ShortLicenceSetId,
                @LicenceSetType
            )
            RETURNING licence_list_item_licence_set_id;
            """;

        return await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                sql,
                new
                {
                    ProcessRunId = processRunId,
                    LicenceListItemId = licenceListItemId,
                    licenceSet.LicenceSetId,
                    licenceSet.ShortLicenceSetId,
                    licenceSet.LicenceSetType
                },
                transaction,
                cancellationToken: cancellationToken));
    }

    private static async Task InsertLicenceSetTypesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long licenceSetRowId,
        IReadOnlyCollection<string> licenceSetTypes,
        CancellationToken cancellationToken)
    {
        var values = licenceSetTypes
            .Distinct()
            .Select(type => new
            {
                LicenceListItemLicenceSetId = licenceSetRowId,
                LicenceSetType = type
            })
            .ToArray();

        if (values.Length == 0)
        {
            return;
        }

        const string sql =
            """
            INSERT INTO licence_list_item_licence_set_type
            (
                licence_list_item_licence_set_id,
                licence_set_type
            )
            VALUES
            (
                @LicenceListItemLicenceSetId,
                @LicenceSetType
            );
            """;

        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                values,
                transaction,
                cancellationToken: cancellationToken));
    }
    
    private static async Task UpsertVerificationsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int processRunId,
        long licenceListItemId,
        IReadOnlyCollection<LicenceSectionVerificationSummary> sections,
        CancellationToken cancellationToken)
    {
        foreach (var section in sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(section.LicenceSectionName))
            {
                continue;
            }

            var verificationSectionId =
                await UpsertVerificationSectionAsync(
                    connection,
                    transaction,
                    processRunId,
                    licenceListItemId,
                    section.LicenceSectionName,
                    cancellationToken);

            foreach (var item in section.LicenceSectionItems)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(item.LicenceSectionItemId))
                {
                    continue;
                }

                await UpsertVerificationItemAsync(
                    connection,
                    transaction,
                    verificationSectionId,
                    item,
                    cancellationToken);
            }
        }
    }
    private static Task<long> UpsertVerificationSectionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int processRunId,
        long licenceListItemId,
        string licenceSectionName,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO licence_list_item_verification_section
            (
                process_run_id,
                licence_list_item_id,
                licence_section_name
            )
            VALUES
            (
                @ProcessRunId,
                @LicenceListItemId,
                @LicenceSectionName
            )
            ON CONFLICT
            (
                licence_list_item_id,
                licence_section_name
            )
            DO UPDATE SET
                process_run_id = EXCLUDED.process_run_id
            RETURNING verification_section_id;
            """;

        return connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                sql,
                new
                {
                    ProcessRunId = processRunId,
                    LicenceListItemId = licenceListItemId,
                    LicenceSectionName =
                        licenceSectionName.Trim()
                },
                transaction,
                cancellationToken: cancellationToken));
    }
    
    private static Task UpsertVerificationItemAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long verificationSectionId,
        LicenceSectionItemSummary item,
        CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO licence_list_item_verification_item
            (
                verification_section_id,
                licence_section_item_id,
                verification_types,
                scraped_data_is_different
            )
            VALUES
            (
                @VerificationSectionId,
                @LicenceSectionItemId,
                @VerificationTypes,
                @ScrapedDataIsDifferent
            )
            ON CONFLICT
            (
                verification_section_id,
                licence_section_item_id
            )
            DO UPDATE SET
                verification_types =
                    EXCLUDED.verification_types,
                scraped_data_is_different =
                    EXCLUDED.scraped_data_is_different;
            """;

        var verificationTypes =
            item.VerificationTypes
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    VerificationSectionId =
                        verificationSectionId,

                    LicenceSectionItemId =
                        item.LicenceSectionItemId.Trim(),

                    VerificationTypes =
                        verificationTypes,

                    item.ScrapedDataIsDifferent
                },
                transaction,
                cancellationToken: cancellationToken));
    }

    private static LicenceListItemSummary CreateSummary(
        UpsertLicenceListItem item)
    {
        var verificationSections = item
            .LicenceSectionVerifications
            .Where(x =>
                !string.IsNullOrWhiteSpace(
                    x.LicenceSectionName))
            .ToArray();

        var verificationItemsCount =
            verificationSections.Sum(
                section => section.LicenceSectionItems.Count(
                    itemSummary =>
                        !string.IsNullOrWhiteSpace(
                            itemSummary.LicenceSectionItemId)));

        var searchParts = new List<string?>
        {
            item.LicenceNumber,
        };
        
        searchParts.AddRange(
            item.LinkedLicences.SelectMany(linked => new[]
            {
                linked.LicenceNumber,
                linked.RawScrapedLicenceNumber,
            }));

        var searchText = string.Join(
            " ",
            searchParts
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));

        return new LicenceListItemSummary(
            PurposesCount: item.Purposes.Length,
            PointsCount: item.Points.Length,
            LinkedLicencesCount: item.LinkedLicences.Length,
            LicenceSetsCount: item.LicenceSets.Length,
            VerificationSectionsCount:
                verificationSections.Length,
            VerificationItemsCount:
                verificationItemsCount,
            HasVerifications:
                verificationItemsCount > 0,
            SearchText:
                searchText);
    }

    private static void ValidateItem(
        UpsertLicenceListItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.ProcessRunId <= 0)
        {
            throw new ArgumentException(
                "ProcessRunId must be greater than zero.",
                nameof(item));
        }

        if (item.FileId == Guid.Empty)
        {
            throw new ArgumentException(
                "FileId must not be empty.",
                nameof(item));
        }

        if (string.IsNullOrWhiteSpace(item.Filename))
        {
            throw new ArgumentException(
                "Filename must not be empty.",
                nameof(item));
        }

        if (!string.IsNullOrWhiteSpace(item.SourceData))
        {
            try
            {
                using var document =
                    JsonDocument.Parse(item.SourceData);
            }
            catch (JsonException exception)
            {
                throw new ArgumentException(
                    "SourceData must contain valid JSON.",
                    nameof(item),
                    exception);
            }
        }
    }

    private static string? NullIfWhiteSpace(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    private sealed record LicenceListItemSummary(
        int PurposesCount,
        int PointsCount,
        int LinkedLicencesCount,
        int LicenceSetsCount,
        int VerificationSectionsCount,
        int VerificationItemsCount,
        bool HasVerifications,
        string SearchText);
    
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