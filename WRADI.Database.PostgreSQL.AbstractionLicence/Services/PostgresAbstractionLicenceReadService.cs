using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay.DTOs;
using WRADI.Core.AbstractionLicence.Models.Table;

namespace WRADI.Database.PostgreSQL.AbstractionLicence.Services;

public class PostgresAbstractionLicenceReadService(INpgsqlDataSourceProvider dataSourceProvider)
    : IAbstractionLicenceDatabaseReadService
{
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

    public async Task<NaldAbstractionData?> GetNaldAbstractionLicenceAsync(string licenceNumber)
    {
        await using var connection = GetPostgresConnection();
        var licenceNumbers = new List<string> { licenceNumber };
        
        var sql = """
               SELECT
                   "ID" AS Id,
                   "LIC_NO" AS LicenceNo,
                   "AREP_SUC_CODE" AS ArepSucCode,
                   "AREP_AREA_CODE" AS ArepAreaCode,
                   "SUSP_FROM_BILLING" AS SuspFromBilling,
                   "AREP_LEAP_CODE" AS ArepLeapCode,
                   "EXPIRY_DATE" AS ExpiryDate,
                   "LAPSED_DATE" AS LapsedDate,
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
                   "FGAC_REGION_CODE" AS FgacRegionCode,
                   (
                        select
                           cast(1 as bit) HasAggCondition
                       from nald."NALD_ABS_LIC_VERSIONS" ver
                       join nald."NALD_ABS_LIC_PURPOSES" pu
                           ON ver."ISSUE_NO" = pu."AABV_ISSUE_NO"
                           AND ver."INCR_NO" = pu."AABV_INCR_NO"
                           AND ver."AABL_ID" = pu."AABV_AABL_ID"
                           AND ver."FGAC_REGION_CODE" = pu."FGAC_REGION_CODE"
                       join nald."NALD_LIC_CONDITIONS" lc
                           ON pu."ID" = lc."AABP_ID"
                           AND pu."FGAC_REGION_CODE" = lc."FGAC_REGION_CODE"
                           AND lc."ACIN_CODE" = 'AGG'
                       where
                           nald."NALD_ABS_LICENCES"."ID" = ver."AABL_ID"
                           AND nald."NALD_ABS_LICENCES"."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                           AND ver."ISSUE_NO" = (
                               SELECT MAX(LIC_VER_SUBQUERY."ISSUE_NO")
                               FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY
                               WHERE LIC_VER_SUBQUERY."AABL_ID" = ver."AABL_ID"
                                   AND LIC_VER_SUBQUERY."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                                   AND LIC_VER_SUBQUERY."EFF_ST_DATE" <= CURRENT_DATE
                                   AND (LIC_VER_SUBQUERY."EFF_END_DATE" >= CURRENT_DATE OR LIC_VER_SUBQUERY."EFF_END_DATE" IS NULL)
                                   AND LIC_VER_SUBQUERY."STATUS" <> 'DRAFT'
                               )
                           AND ver."INCR_NO" = (
                               SELECT MAX(LIC_VER_SUBQUERY_2."INCR_NO")
                               FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_2
                               WHERE LIC_VER_SUBQUERY_2."AABL_ID" = ver."AABL_ID"
                                   AND LIC_VER_SUBQUERY_2."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                                   AND LIC_VER_SUBQUERY_2."EFF_ST_DATE" <= CURRENT_DATE
                                   AND (LIC_VER_SUBQUERY_2."EFF_END_DATE" >= CURRENT_DATE OR LIC_VER_SUBQUERY_2."EFF_END_DATE" IS NULL)
                                   AND LIC_VER_SUBQUERY_2."STATUS" <> 'DRAFT'
                               )
                       LIMIT 1) AS HasAggCondition
               FROM nald."NALD_ABS_LICENCES"
               WHERE (
               """;

        var idx = 0;
        foreach (var _ in licenceNumbers)
        {
            if (idx > 0)
            {
                sql += " OR ";
            }
            
            sql += $"\"LIC_NO\" = @LicNo{idx++}\n";
        }

        sql += """
               )
               ORDER BY
                   "ID",
                   "FGAC_REGION_CODE"
               LIMIT 1
               """;

        var paramsObj = new DynamicParameters();
        idx = 0;
        
        foreach (var licenceNumberLoop in licenceNumbers)
        {
            paramsObj.Add($"@LicNo{idx++}", licenceNumberLoop);
        }
        
        var dataLine = await QueryFirstOrDefaultAsync<NaldAbstractionLicenceDataLine>(
            connection,
            sql,
            0,
            paramsObj);

        if (dataLine == null)
        {
            return null;
        }
        
        var naldData = NaldHelper.NaldAbstractionLicenceDataLineToNaldData(dataLine);

        var versions = await GetNaldLicenceVersionsAsync(dataLine.Id, dataLine.FgacRegionCode);
        versions = versions
            .OrderByDescending(v => v.IssueNo)
            .ThenBy(v => v.IncrNo)
            .ToList();
        
        foreach (var version in versions)
        {
            NaldHelper.AddNaldAbstractionLicenceVersionData(version, naldData);
            break;
        }

        var purposesTask = GetNaldLicencePurposesAsync(
            naldData!.Id,
            naldData.IssueNo!.Value,
            naldData.IncrNo!.Value,
            dataLine.FgacRegionCode);
        
        var quantitiesTask = GetNaldLicenceQuantitiesAsync(
            naldData.Id,
            naldData.IssueNo!.Value,
            naldData.IncrNo!.Value,
            dataLine.FgacRegionCode);
        
        var purposes = await purposesTask;
        
        foreach (var purpose in purposes)
        {
            NaldHelper.AddNaldAbstractionLicencePurposeData(purpose, naldData);
            
            var points = await GetNaldLicencePointsAsync(purpose.Id!, dataLine.FgacRegionCode);
            
            foreach (var point in points)
            {
                NaldHelper.AddNaldAbstractionLicencePointsData(point, naldData);               
            }
        }
        
        var quantities = await quantitiesTask;
        
        foreach (var quantity in quantities)
        {
            NaldHelper.AddNaldAbstractionLicenceQuantitiesData(quantity, naldData);               
        }
        
        return naldData;
    }
    
    public async Task<LicenceFinderResult> GetLicenceFinderResultAsync(Guid fileId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
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
                               we_have_downloaded
                           FROM public.licence_finder_result
                           WHERE file_id = @FileId
                           """;

        var result = await QueryFirstOrDefaultAsync<LicenceFinderResult>(
            connection,
            sql,
            0,
            new { FileId = fileId.ToString() });

        return result!;
    }

    private async Task<List<NaldLicenceQuantitiesDataLine>> GetNaldLicenceQuantitiesAsync(
        int abvAablId,
        int issueNumber,
        int incrementNumber,
        int regionCode)
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
                           WHERE "FGAC_REGION_CODE" = @RegionCode
                                AND "AABV_AABL_ID" = @AabvAablId
                                AND "AABV_ISSUE_NO" = @AabvIssueNo
                                AND "AABV_INCR_NO" = @AabvIncrNo     
                           """;

        return (await QueryAsync<NaldLicenceQuantitiesDataLine>(
            connection,
            sql,
            0,
            new
            {
                RegionCode = regionCode,
                AabvAablId = abvAablId,
                AabvIssueNo = issueNumber,
                AabvIncrNo = incrementNumber
            })).ToList();
    }
    
    public async Task<List<string>> GetDistinctIssueDatesAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();

        const string sql =
            """
            SELECT DISTINCT
                   LEFT(data::jsonb -> 'licenceVersion' ->> 'issueDate', 4) AS issue_year
            FROM licence
            WHERE process_run_id = @ProcessRunId
              AND data::jsonb ->> 'status' = 'Ok'
              AND NULLIF(
                    trim(data::jsonb -> 'licenceVersion' ->> 'issueDate'),
                    ''
                  ) IS NOT NULL
            ORDER BY issue_year;
            """;

        var issueDate = await QueryAsync<string>(
            connection,
            sql,
            0,
            new
            {
                ProcessRunId = processRunId
            });
        
        return issueDate.ToList();
    }

    private async Task<List<NaldLicencePointDataLine>> GetNaldLicencePointsAsync(
        string purposeId,
        int regionCode)
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
                           WHERE pp."FGAC_REGION_CODE" = @RegionCode
                                AND pp."AABP_ID" = @PurposeId;
                           """;

        return (await QueryAsync<NaldLicencePointDataLine>(
            connection,
            sql,
            0,
            new
            {
                RegionCode = regionCode,
                PurposeId = int.Parse(purposeId)
            })).ToList();
    }

    private async Task<List<NaldLicencePurposeDataLine>> GetNaldLicencePurposesAsync(
        int abvAablId,
        int issueNumber,
        int incrementNumber,
        int regionCode)
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
                           WHERE p."FGAC_REGION_CODE" = @RegionCode
                                AND p."AABV_AABL_ID" = @AabvAablId
                                AND p."AABV_ISSUE_NO" = @AabvIssueNo
                                AND p."AABV_INCR_NO" = @AabvIncrNo
                           """;

        return (await QueryAsync<NaldLicencePurposeDataLine>(
            connection,
            sql,
            0,
            new
            {
                RegionCode = regionCode,
                AabvAablId = abvAablId,
                AabvIssueNo = issueNumber,
                AabvIncrNo = incrementNumber
            })).ToList();
    }

    private async Task<List<NaldLicenceVersionDataLine>> GetNaldLicenceVersionsAsync(
        int aablId,
        int regionCode)
    {
        await using var connection = GetPostgresConnection();
        var sql = """
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
                           WHERE "FGAC_REGION_CODE" = @RegionCode
                                 AND "AABL_ID" = @AablId
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
                    """;
            
        sql += """
            AND "WA_ALTY_CODE" IN ('FULL', 'NA', 'TEMP', 'TRAN');
        """;

        return (await QueryAsync<NaldLicenceVersionDataLine>(
            connection,
            sql,
            0,
            new
            {
                RegionCode = regionCode,
                AablId = aablId
            })).ToList();
    }

    public async Task<int> GetTotalLicenceCountAsync(
        int processRunId,
        ProcessRunQuery query)
    {
        await using var connection = GetPostgresConnection();

        var sql = new StringBuilder(
            """
            SELECT count(*)
            FROM licence
            WHERE process_run_id = @ProcessRunId
              AND data::jsonb ->> 'status' = 'Ok'
            """);

        var parameters = new DynamicParameters();
        parameters.Add("ProcessRunId", processRunId);
        AddGenericParameters(query, sql, parameters);
        
        return await QuerySingleOrDefaultAsync<int>(
            connection,
            sql.ToString(),
            0,
            parameters);
    }
    
    private static void AddGenericParameters(ProcessRunQuery query, StringBuilder sql, DynamicParameters parameters)
    {
        AddLicenceSearchTermFilter(sql, parameters, query.SearchTermClean);
        AddLicenceNumbersFilter(sql, parameters, query.LicenceNumbers);
        AddIssuerFilter(sql, parameters, query.Issuer);
        AddOcrScanFilter(sql, parameters, query.OcrScan);
        AddMeansFoundFilter(sql, parameters, query.MeansFound);

        AddArrayEmptyFilter(
            sql,
            query.PointsEmpty,
            "points");

        AddArrayEmptyFilter(
            sql,
            query.PurposesEmpty,
            "purposes");

        AddNestedLimitsFilter(
            sql,
            query.LimitsEmpty,
            "individual");

        AddAggregatesFilter(sql, query.AggregatesFilter);

        AddIssueYearFilter(
            sql,
            parameters,
            query.IssueYear);

        AddLinkedLicencesTypeFilter(
            sql,
            parameters,
            query.LinkedLicencesType);
    }
    
    public async Task<Dictionary<Guid, string>> GetLicenceFileIdsAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();

        const string sql = """
                           SELECT max(licence_number) AS licence_number, file_id
                           FROM licence
                           WHERE process_run_id = @ProcessRunId
                             AND licence_number IS NOT NULL
                             AND file_id IS NOT NULL
                           GROUP BY file_id;
                           """;

        var results = await QueryAsync<(string LicenceNumber, Guid FileId)>(
            connection,
            sql,
            0,
            new { ProcessRunId = processRunId });

        return results.ToDictionary(x => x.FileId, x => x.LicenceNumber);
    }
    
    public async Task<int> GetLicencesListSearchCountAsync(int processRunId, ProcessRunQuery query)
    {
        await using var connection = GetPostgresConnection();

        return await GetLicenceListItemParentsCountAsync(
            connection,
            processRunId,
            query,
            CancellationToken.None);
    }

    public async Task<List<NaldLicenceNumberHistoryDb>> GetNaldLicenceNumberHistoryAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               "LIC_NO" as "LicenceNumber",
                               "FOLL_LIC_NO" as "FollowOnLicenceNumber",
                               case MIN(
                                   case "SOURCE"
                                       when 'FOLL_LIC_NO' THEN 0
                                       when 'PREV_LIC_NO' THEN 1
                                       when 'CONVENTION' THEN 2
                                       when 'NOTES' THEN 3
                                   END)
                                   when 0 THEN 'FOLL_LIC_NO'
                                   when 1 THEN 'PREV_LIC_NO'
                                   when 2 THEN 'CONVENTION' 
                                   when 3 THEN 'NOTES'
                               END as "Source"
                           FROM (
                               -- Straight forward follow on licences (NOTE - 5 licences that are live have a follow on anyway)
                               SELECT
                                   "LIC_NO",
                                   "FOLL_LIC_NO",
                                   'FOLL_LIC_NO' as "SOURCE"
                               FROM nald."NALD_ABS_LICENCES"
                               WHERE
                                   "FOLL_LIC_NO" IS NOT NULL
                                   AND "FOLL_LIC_NO" <> 'N/A'
                                   AND "FOLL_LIC_NO" NOT LIKE '%AND%'
                                   and (
                                       "EXPIRY_DATE" IS NOT NULL
                                       OR "REV_DATE" IS NOT NULL
                                       OR "LAPSED_DATE" IS NOT NULL)
                           
                               UNION
                           
                               -- Predecessor licences, reversed
                               SELECT
                                   "PREV_LIC_NO" as "LIC_NO",
                                   "LIC_NO" as "FOLL_LIC_NO",
                                   'PREV_LIC_NO' as "SOURCE"
                               FROM nald."NALD_ABS_LICENCES"
                               WHERE
                                   "PREV_LIC_NO" IS NOT NULL
                           
                               UNION
                           
                               -- Look at notes to get successors
                               SELECT
                                   lic."LIC_NO",
                                   REPLACE(SPLIT_PART(SUBSTRING(REPLACE(lic."NOTES", 'LIC ', ''), POSITION('REPLACED BY' IN lic."NOTES") + 12, 16), ' ', 1), '*', '') as "FOLL_LIC_NO",
                                   'NOTES' as "SOURCE"
                               FROM nald."NALD_ABS_LICENCES" as lic
                               WHERE
                                   lic."FOLL_LIC_NO" is null
                                   and lic."NOTES" is not null
                                   and lic."NOTES" LIKE '%REPLACED BY%'
                                   and SPLIT_PART(SUBSTRING(REPLACE(lic."NOTES", 'LIC ', ''), POSITION('REPLACED BY' IN lic."NOTES") + 12, 16), ' ', 1) like '%/%'
                           
                               UNION
                           
                               -- Trial and error to get next one
                               SELECT
                                   "LIC_NO",
                                   "SUB_LIC_NO" AS "FOLL_LIC_NO",
                                   'CONVENTION' as "SOURCE"
                               FROM (
                                   -- Add R1
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = CONCAT(lic."LIC_NO", 'R1')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                           
                                   UNION
                           
                                   -- Add /R1
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = CONCAT(lic."LIC_NO", '/R1')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                           
                                   UNION
                           
                                   -- Add R01
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = CONCAT(lic."LIC_NO", 'R01')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                           
                                   UNION
                           
                                   -- Add /R01
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = CONCAT(lic."LIC_NO", '/R01')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                           
                                   UNION
                           
                                   -- Swap R01 to R02
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", 'R01', 'R02')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%R01'
                           
                                   UNION
                           
                                   -- Swap R02 to R03
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", 'R02', 'R03')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%R02'
                           
                                       UNION
                           
                                   -- Swap R03 to R04
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", 'R03', 'R04')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%R03'
                           
                                   UNION
                           
                                   -- Swap R04 to R05 (zero found)
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", 'R04', 'R05')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%R04'
                           
                                   UNION
                           
                                   -- Add A
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = CONCAT(lic."LIC_NO", 'A')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                           
                                   UNION
                           
                                   -- Add /A
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = CONCAT(lic."LIC_NO", '/A')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                           
                           
                                   UNION
                           
                                   -- Swap A to B
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", 'A', 'B')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%Add'
                           
                                   UNION
                           
                                   -- Swap B to C
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", 'B', 'C')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%B'
                           
                                   UNION
                           
                                   -- Swap C to D
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", 'C', 'D')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%C'
                           
                                   UNION
                           
                                   -- Swap D to E
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", 'D', 'E')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%D'        
                           
                                   UNION
                           
                                   -- Swap E to F
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", 'E', 'F')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%E'     
                           
                                   UNION
                           
                                   -- Swap F to G
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", 'F', 'G')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%F'       
                           
                                   UNION
                           
                                   -- Add /1
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = CONCAT(lic."LIC_NO", '/1')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                           
                               UNION
                           
                                   -- Swap /1 to /2
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", '/1', '/2')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%/1'
                           
                                   UNION
                           
                                   -- Swap /2 to /3
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", '/2', '/3')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%/2'
                           
                                   UNION
                           
                                   -- Swap /3 to /4
                                   SELECT
                                       lic."LIC_NO",
                                       sublic."LIC_NO" as "SUB_LIC_NO"
                                   FROM nald."NALD_ABS_LICENCES" as lic
                                   JOIN nald."NALD_ABS_LICENCES" as sublic ON
                                       sublic."LIC_NO" = REPLACE(lic."LIC_NO", '/3', '/4')
                                   WHERE
                                       lic."FOLL_LIC_NO" IS NULL
                                       and (
                                           lic."EXPIRY_DATE" IS NOT NULL
                                           OR lic."REV_DATE" IS NOT NULL
                                           OR lic."LAPSED_DATE" IS NOT NULL)
                                       and lic."LIC_NO" like '%/3'
                               )
                           )
                           WHERE
                               LENGTH("LIC_NO") > 3
                               AND "LIC_NO" <> "FOLL_LIC_NO"
                           GROUP BY
                               "LIC_NO",
                               "FOLL_LIC_NO"
                           """;

        return (await QueryAsync<NaldLicenceNumberHistoryDb>(
            connection,
            sql,
            0,
            new {})
            ).ToList();
    }

    public async Task<NaldImpoundmentData?> GetNaldImpoundmentLicenceAsync(string licenceNumber)
    {
        await using var connection = GetPostgresConnection();

        var sql = """
                  SELECT 
                        "ID" as Id,
                        "LIC_NO" as LicenceNumber,
                        "REV_DATE" as RevocationDate,
                        "FGAC_REGION_CODE" as FgacRegionCode
                  FROM nald."NALD_IMP_LICENCES"
                  WHERE
                      "LIC_NO" = @LicNo
                  """;
        
        return await QueryFirstOrDefaultAsync<NaldImpoundmentData>(
            connection,
            sql,
            0,
            new { LicNo = licenceNumber });
    }

    public async Task<List<Licence>> GetLicencesSearchAsync(
        int processRunId,
        ProcessRunQuery query)
    {
        await using var connection = GetPostgresConnection();

        var sql = new StringBuilder(
            """
            SELECT data, licence_id
            FROM licence
            WHERE process_run_id = @ProcessRunId
              AND data::jsonb ->> 'status' = 'Ok'
            """);

        var parameters = new DynamicParameters();

        parameters.Add("ProcessRunId", processRunId);
        parameters.Add("Skip", query.Skip);
        parameters.Add("Take", query.Take);

        AddGenericParameters(query, sql, parameters);

        sql.AppendLine(
            """
            ORDER BY licence_id
            LIMIT @Take
            OFFSET @Skip;
            """);

        var results = await QueryAsync<(string Data, int LicenceId)>(
            connection,
            sql.ToString(),
            0,
            parameters);

        return results
            .Select(result =>
            {
                var licence = JsonSerializer.Deserialize<Licence>(
                    result.Data,
                    GetSerializerOptions())!;

                licence.NoneSchemaData.TryAdd(
                    "licenceId",
                    result.LicenceId);

                return licence;
            })
            .ToList();
    }

    public async Task<List<Licence>> GetLicencesAsync(int processRunId, int skip, int take)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT data, licence_id 
                           FROM licence 
                           WHERE process_run_id = @ProcessRunId
                           ORDER BY licence_id
                           LIMIT @take
                           OFFSET @skip;
                           """;

        var results = await QueryAsync<(string Data, int LicenceId)>(
            connection,
            sql,
            0,
            new
            {
                ProcessRunId = processRunId,
                Skip = skip,
                Take = take
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

    public async Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(Guid fileId, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT DISTINCT
                               ls.licence_set_id,
                               ls.short_licence_set_id, 
                               ls.schema_licence_set_id 
                           FROM licence_set ls
                           JOIN licence_set_licence lsl on lsl.licence_set_id = ls.licence_set_id
                           JOIN licence l on l.licence_id = lsl.licence_id AND l.file_id = @FileId
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
                FileId = fileId
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
                                process_run_id,
                                l."FGAC_REGION_CODE" as region_id
                           FROM licence_set_licence as lsl
                           LEFT JOIN nald."NALD_ABS_LICENCES" as l on licence_number = l."LIC_NO"
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

    public async Task<Licence?> GetLicenceAsync(Guid fileId, int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT 
                               data,
                               licence_id 
                           FROM licence
                           WHERE file_id = @FileId
                           AND process_run_id = @ProcessRunId;
                           """;

        var result = await QueryFirstOrDefaultAsync<(string Data, int LicenceId)?>(
            connection,
            sql,
            0,
            new
            {
                FileId = fileId,
                ProcessRunId = processRunId
            });
        if (result == null)
        {
            return null;
        }

        var data = JsonSerializer.Deserialize<Licence>(result.Value.Data, GetSerializerOptions())!;
        data.NoneSchemaData.TryAdd("licenceId", result.Value.LicenceId);
        data.ProcessRunId = processRunId;
        
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
                             AND process_run_id = @ProcessRunId;
                           """;

        var result = await QuerySingleOrDefaultAsync<(string Data, int LicenceId)?>(
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
        data.ProcessRunId = processRunId;
        return data;
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
                               lic."FGAC_REGION_CODE" AS RegionCode,
                               lc."ACIN_CODE" as AcinCode
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
                                       AND (LIC_VER_SUBQUERY."EFF_END_DATE" >= CURRENT_DATE OR LIC_VER_SUBQUERY."EFF_END_DATE" IS NULL)
                                       AND LIC_VER_SUBQUERY."STATUS" <> 'DRAFT'
                                   )
                               AND ver."INCR_NO" = (
                                   SELECT MAX(LIC_VER_SUBQUERY_2."INCR_NO")
                                   FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_2
                                   WHERE LIC_VER_SUBQUERY_2."AABL_ID" = ver."AABL_ID"
                                       AND LIC_VER_SUBQUERY_2."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                                       AND LIC_VER_SUBQUERY_2."EFF_ST_DATE" <= CURRENT_DATE
                                       AND (LIC_VER_SUBQUERY_2."EFF_END_DATE" >= CURRENT_DATE OR LIC_VER_SUBQUERY_2."EFF_END_DATE" IS NULL)
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
                               lic."FGAC_REGION_CODE" AS RegionCode,
                               null as AcinCode
                           from nald."NALD_ABS_LICENCES" lic
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

    public async Task<List<NaldLicence>> GetNaldImpoundmentAndAbstractionLicencesAsync(int skip, int take)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           (
                               SELECT 
                                   "LIC_NO" AS LicenceNumber,
                                   "FGAC_REGION_CODE" AS RegionCode,
                                   "ID" AS Id,
                                   3 AS Type
                               FROM nald."NALD_ABS_LICENCES"
                               
                               UNION ALL
                               
                               SELECT 
                                   "LIC_NO" AS LicenceNumber,
                                   "FGAC_REGION_CODE" AS RegionCode,
                                   "ID" AS Id,
                                   4 AS Type
                               FROM nald."NALD_IMP_LICENCES"
                            )
                           ORDER BY
                                LicenceNumber,
                                RegionCode
                           LIMIT @take
                           OFFSET @skip;
                           """;

        var result = await QueryAsync<NaldLicence>(
            connection,
            sql,
            0,
            new { Skip = skip, Take = take });

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
        async Task<HashSet<(string, int)>> RunWithNewConnectionAsync(
            Func<NpgsqlConnection, Task<HashSet<(string, int)>>> query)
        {
            await using var connection = GetPostgresConnection();
            return await query(connection);
        }
    }

    private async Task<HashSet<(string, int)>> GetLiveLicenceNumbersAsync(
        NpgsqlConnection connection,
        short? regionCode)
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

    private async Task<HashSet<(string, int)>> GetRevokedLicenceNumbersAsync(NpgsqlConnection connection,
        short? regionCode)
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

    private async Task<HashSet<(string, int)>> GetLapsedLicenceNumbersAsync(NpgsqlConnection connection,
        short? regionCode)
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

    private async Task<HashSet<(string, int)>> GetExpiredLicenceNumbersAsync(NpgsqlConnection connection,
        short? regionCode)
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

    private async Task<HashSet<(string, int)>> GetImpoundmentLicenceNumbersAsync(NpgsqlConnection connection,
        short? regionCode)
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

    public async Task<List<NaldAbstractionLicenceDataLine>> GetNaldAbsLicencesAsync(short? regionCode, int skip, int take)
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
                               "LAPSED_DATE" AS LapsedDate,
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
                               "FGAC_REGION_CODE" AS FgacRegionCode,
                               (
                                    select
                                       cast(1 as bit) HasAggCondition
                                   from nald."NALD_ABS_LIC_VERSIONS" ver
                                   join nald."NALD_ABS_LIC_PURPOSES" pu
                                       ON ver."ISSUE_NO" = pu."AABV_ISSUE_NO"
                                       AND ver."INCR_NO" = pu."AABV_INCR_NO"
                                       AND ver."AABL_ID" = pu."AABV_AABL_ID"
                                       AND ver."FGAC_REGION_CODE" = pu."FGAC_REGION_CODE"
                                   join nald."NALD_LIC_CONDITIONS" lc
                                       ON pu."ID" = lc."AABP_ID"
                                       AND pu."FGAC_REGION_CODE" = lc."FGAC_REGION_CODE"
                                       AND lc."ACIN_CODE" = 'AGG'
                                   where
                                       nald."NALD_ABS_LICENCES"."ID" = ver."AABL_ID"
                                       AND nald."NALD_ABS_LICENCES"."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                                       AND ver."ISSUE_NO" = (
                                           SELECT MAX(LIC_VER_SUBQUERY."ISSUE_NO")
                                           FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY
                                           WHERE LIC_VER_SUBQUERY."AABL_ID" = ver."AABL_ID"
                                               AND LIC_VER_SUBQUERY."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                                               AND LIC_VER_SUBQUERY."EFF_ST_DATE" <= CURRENT_DATE
                                               AND (LIC_VER_SUBQUERY."EFF_END_DATE" >= CURRENT_DATE OR LIC_VER_SUBQUERY."EFF_END_DATE" IS NULL)
                                               AND LIC_VER_SUBQUERY."STATUS" <> 'DRAFT'
                                           )
                                       AND ver."INCR_NO" = (
                                           SELECT MAX(LIC_VER_SUBQUERY_2."INCR_NO")
                                           FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_2
                                           WHERE LIC_VER_SUBQUERY_2."AABL_ID" = ver."AABL_ID"
                                               AND LIC_VER_SUBQUERY_2."FGAC_REGION_CODE" = ver."FGAC_REGION_CODE"
                                               AND LIC_VER_SUBQUERY_2."EFF_ST_DATE" <= CURRENT_DATE
                                               AND (LIC_VER_SUBQUERY_2."EFF_END_DATE" >= CURRENT_DATE OR LIC_VER_SUBQUERY_2."EFF_END_DATE" IS NULL)
                                               AND LIC_VER_SUBQUERY_2."STATUS" <> 'DRAFT'
                                       )
                               LIMIT 1) AS HasAggCondition
                           FROM nald."NALD_ABS_LICENCES"
                           WHERE @RegionCode is null or "FGAC_REGION_CODE" = @RegionCode
                           ORDER BY
                               "ID",
                               "FGAC_REGION_CODE"
                           LIMIT @take
                           OFFSET @skip;
                           """;

        // TODO check if this should  filter out to only none-revoked etc...

        return (await QueryAsync<NaldAbstractionLicenceDataLine>(
            connection,
            sql,
            0,
            new
            {
                RegionCode = regionCode,
                Skip = skip,
                Take = take
            })).ToList();
    }

    public async Task<List<NaldLicenceVersionDataLine>> GetNaldLicenceVersionsAsync(short? regionCode, bool allVersions, int skip, int take)
    {
        await using var connection = GetPostgresConnection();
        var sql = """
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
                           """;

        if (!allVersions)
        {
            sql += """
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
                    """;
        }
            
        sql += """
            AND "WA_ALTY_CODE" IN ('FULL', 'NA', 'TEMP', 'TRAN')
            ORDER BY
                "AABL_ID",
                "ISSUE_NO",
                "INCR_NO"
            LIMIT @take
            OFFSET @skip;
        """;

        return (await QueryAsync<NaldLicenceVersionDataLine>(
            connection,
            sql,
            0,
            new
            {
                RegionCode = regionCode,
                Skip = skip,
                Take = take
            })).ToList();
    }

    public async Task<List<NaldLicencePurposeDataLine>> GetNaldLicencePurposesAsync(short? regionCode, int skip, int take)
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
                           ORDER BY
                                p."ID"
                           LIMIT @take
                           OFFSET @skip;
                           """;

        return (await QueryAsync<NaldLicencePurposeDataLine>(
            connection,
            sql,
            0,
            new
            {
                RegionCode = regionCode,
                Skip = skip,
                Take = take
            })).ToList();
    }

    public async Task<List<NaldLicencePointDataLine>> GetNaldLicencePointsAsync(short? regionCode, int skip, int take)
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
                           ORDER BY
                                pp."AABP_ID",
                                pp."AAIP_ID"
                           LIMIT @take
                           OFFSET @skip;
                           """;

        return (await QueryAsync<NaldLicencePointDataLine>(
            connection,
            sql,
            0,
            new
            {
                RegionCode = regionCode,
                Skip = skip,
                Take = take
            })).ToList();
    }

    public async Task<List<NaldLicenceQuantitiesDataLine>> GetNaldLicenceQuantitiesAsync(short? regionCode, int skip, int take)
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
                           ORDER BY
                                "ID"
                           LIMIT @take
                           OFFSET @skip;
                           """;

        return (await QueryAsync<NaldLicenceQuantitiesDataLine>(
            connection,
            sql,
            0,
            new
            {
                RegionCode = regionCode,
                Skip = skip,
                Take = take
            })).ToList();
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

        var result = await QuerySingleOrDefaultAsync<(string Data, int LicenceId)?>(
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

    public async Task<int> GetNaldLicenceIncrementNumberAsync(string permitNumber, int issueNumber)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT DISTINCT
                               (SELECT MAX(LIC_VER_SUBQUERY_2."INCR_NO")
                               FROM nald."NALD_ABS_LIC_VERSIONS" LIC_VER_SUBQUERY_2
                               WHERE LIC_VER_SUBQUERY_2."AABL_ID" = NALD_ABS_LIC_VERSIONS."AABL_ID"
                                   AND LIC_VER_SUBQUERY_2."FGAC_REGION_CODE" = NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE"
                                   AND LIC_VER_SUBQUERY_2."EFF_ST_DATE" <= CURRENT_DATE
                                   AND (LIC_VER_SUBQUERY_2."EFF_END_DATE" >= CURRENT_DATE OR LIC_VER_SUBQUERY_2."EFF_END_DATE" IS NULL)
                                   AND LIC_VER_SUBQUERY_2."STATUS" <> 'DRAFT'
                               ) as "INCR_NO"
                           FROM nald."NALD_ABS_LICENCES" NALD_ABS_LICENCES
                           INNER JOIN nald."NALD_ABS_LIC_VERSIONS" NALD_ABS_LIC_VERSIONS
                               ON NALD_ABS_LIC_VERSIONS."FGAC_REGION_CODE" = NALD_ABS_LICENCES."FGAC_REGION_CODE"
                               AND NALD_ABS_LIC_VERSIONS."AABL_ID" = NALD_ABS_LICENCES."ID"
                           WHERE
                               REPLACE(REPLACE(REPLACE(REPLACE(NALD_ABS_LICENCES."LIC_NO", '/', ''), '*', ''), '.', ''), ' ', '') = @PermitNumber
                               AND NALD_ABS_LIC_VERSIONS."ISSUE_NO" = @IssueNumber
                               AND NALD_ABS_LIC_VERSIONS."WA_ALTY_CODE" IN ('FULL', 'NA', 'TEMP', 'TRAN');
                           """;

        var result = await QuerySingleOrDefaultAsync<int?>(
            connection,
            sql,
            0,
            new
            {
                PermitNumber = permitNumber,
                IssueNumber = issueNumber
            });

        if (result == null)
        {
            return -1;
        }

        return result.Value;
    }

    public async Task<IEnumerable<LicenceSectionVerification>> GetLicenceSectionVerificationsAsync(Guid licenceFileId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               licence_section_verification_id AS LicenceSectionVerificationId,
                               licence_file_id AS LicenceFileId,
                               process_run_id AS ProcessRunId,
                               licence_section_name AS LicenceSectionName,
                               licence_section_scraped_value AS LicenceSectionScrapedValue,
                               licence_section_snapshot_value AS LicenceSectionSnapshotValue,
                               licence_section_override_value AS LicenceSectionOverrideValue,
                               verification_type AS VerificationType,
                               licence_section_item_id AS LicenceSectionItemId,
                               notes AS Notes,
                               created_date_time_utc AS CreatedDateTimeUtc
                           FROM licence_section_verification
                           WHERE licence_file_id = @LicenceFileId
                           ORDER BY created_date_time_utc DESC
                           """;

        return await QueryAsync<LicenceSectionVerification>(
            connection,
            sql,
            0,
            new { LicenceFileId = licenceFileId });
    }

    public async Task<IEnumerable<LicenceSectionVerification>> GetAllVerificationsAsync(int maxProcessRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                               licence_section_verification_id AS LicenceSectionVerificationId,
                               licence_file_id AS LicenceFileId,
                               process_run_id AS ProcessRunId,
                               licence_section_name AS LicenceSectionName,
                               licence_section_scraped_value AS LicenceSectionScrapedValue,
                               licence_section_snapshot_value AS LicenceSectionSnapshotValue,
                               licence_section_override_value AS LicenceSectionOverrideValue,
                               verification_type AS VerificationType,
                               licence_section_item_id AS LicenceSectionItemId,
                               notes AS Notes,
                               created_date_time_utc AS CreatedDateTimeUtc
                           FROM licence_section_verification
                           WHERE process_run_id <= @MaxProcessRunId
                           ORDER BY 
                               licence_file_id, 
                               licence_section_name, 
                               created_date_time_utc DESC,
                               licence_section_verification_id DESC
                           """;

        return await QueryAsync<LicenceSectionVerification>(
            connection,
            sql,
            0,
            new { MaxProcessRunId = maxProcessRunId });
    }
    
    public async Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync(int skip, int take)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
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
                               we_have_downloaded
                           FROM public.licence_finder_result
                           ORDER BY
                               permit_number,
                               file_url,
                               rule_used
                           LIMIT @take
                           OFFSET @skip;
                           """;

        var results = await QueryAsync<LicenceFinderResult>(
            connection,
            sql,
            0,
            new { Skip = skip, Take = take });

        return results.ToList();
    }
    
    public async Task<List<LicenceSetTable>> GetProcessRunLicenceSetsAsync(int processRunId)
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT DISTINCT
                               ls.licence_set_id as LicenceSetTable,
                               ls.short_licence_set_id as SchemaLicenceSetId, 
                               ls.schema_licence_set_id as SchemaLicenceSetId,
                           FROM licence_set ls
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
            })).ToList();
    }

    public async Task<List<VersionFileToDownload>> GetVersionFilesToDownloadAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                                permit_number,
                                full_path,
                                site_path,
                                library_and_file_path
                           FROM public.version_files_to_download
                           """;

        var results = await QueryAsync<VersionFileToDownload>(
            connection,
            sql,
            0);

        return results.ToList();
    }

    public async Task<List<VersionFile>> GetVersionFilesAsync()
    {
        await using var connection = GetPostgresConnection();
        const string sql = """
                           SELECT
                                permit_number,
                                full_path,
                                site_path,
                                library_and_file_path,
                                region_id,
                                file_id,
                                file_name,
                                file_size
                           FROM public.version_files
                           """;

        var results = await QueryAsync<VersionFile>(
            connection,
            sql,
            0);

        return results.ToList();
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
    
    private static void AddLicenceNumbersFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string[]? licenceNumbers)
    {
        var filteredLicenceNumbers = licenceNumbers?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (filteredLicenceNumbers is not { Length: > 0 })
        {
            return;
        }

        sql.AppendLine(
            """
              AND lower(
                  data::jsonb
                      -> 'licenceNumber'
                      ->> 'value'
              ) = ANY(@LicenceNumbers)
            """);

        parameters.Add(
            "LicenceNumbers",
            filteredLicenceNumbers
                .Select(x => x.ToLowerInvariant())
                .ToArray());
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
    
    private static void AddAggregatesFilter(StringBuilder sql, string? aggregatesState)
    {
        if (string.IsNullOrEmpty(aggregatesState))
        {
            return;
        }

        var parts = aggregatesState.Split('_');
        var docAggregatesText = parts[0];
        var docAggregatesTextIsNull = docAggregatesText.Equals("null", StringComparison.OrdinalIgnoreCase);

        if (!docAggregatesTextIsNull)
        {
            var docAggregates = bool.Parse(docAggregatesText);
            var docAggregatesEmpty = !docAggregates;
            
            AddNestedLimitsFilter(sql, docAggregatesEmpty, "aggregates");
        }

        var naldAggregatesText = parts[1];
        var naldAggregatesTextIsNull = naldAggregatesText.Equals("null", StringComparison.OrdinalIgnoreCase);

        if (!naldAggregatesTextIsNull)
        {
            var naldAggregates = bool.Parse(naldAggregatesText);
            
            sql.AppendLine(
                naldAggregates
                    ? "  AND data::jsonb ->> 'naldHasAggregateCondition' = 'true'"
                    : """
                          AND (data::jsonb ->> 'naldHasAggregateCondition' = 'false'
                              or data::jsonb ->> 'naldHasAggregateCondition' is null)
                      """);
        }
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
            
            verification_item.current_verification_type
        AS CurrentVerificationType,

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
                                    
                                    CurrentVerificationType =  
                                        row.CurrentVerificationType,

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
            long[] itemIds,
            CancellationToken cancellationToken)
    {
        const string linkedSql =
            """
            SELECT
                linked_licence_id AS LinkedLicenceId,
                licence_list_item_id AS LicenceListItemId,
                licence_number AS LicenceNumber,
                raw_scraped_licence_number AS RawScrapedLicenceNumber,
                dms_permit_number AS DmsPermitNumber,
                dms_path AS DmsPath,
                dms_file_id AS DmsFileId,
                filename AS Filename,
                licence_version_id AS LicenceVersionId,
                issue_date AS IssueDate,
                expiry_date AS ExpiryDate,
                original_issue_date AS OriginalIssueDate,
                issuer,
                nald_status AS NaldStatus,
                licence_type As LicenceType,
                region_id As RegionId,
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
                licence_version_nald_status AS LicenceVersionNaldStatus,
                is_because_of_aggregate AS IsBecauseOfAggregate
            FROM licence_list_item_linked_licence
            where
                licence_list_item_id = ANY(@ItemIds)
            ORDER BY
                licence_list_item_id,
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
                linked_licence_id AS LinkedLicenceId,
                direction AS Direction,
                section_name AS SectionName,
                link_reason AS LinkReason,
                page_number AS PageNumber,
                line_number AS LineNumber,
                source AS Source,
                link_location_id AS LinkLocationId,
                acin_code AS AcinCode,
                source_fields as SourceFields
            FROM licence_list_item_link_location
            where
                linked_licence_id =
                ANY(@LinkedLicenceIds)
            ORDER BY
                linked_licence_id;
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
                        Source = x.Source,
                        LinkReason = x.LinkReason,
                        LinkedLicenceId = x.LinkedLicenceId,
                        LinkLocationId = x.LinkLocationId,
                        AcinCode = x.AcinCode,
                        SourceFields = x.SourceFields
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
            aggregate_ids AS AggregateIds,
            nald_aggregate as NaldAggregate,
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

    if (!string.IsNullOrEmpty(query.SearchTermClean) &&
        string.IsNullOrWhiteSpace(query.SortField))
    {
        sql.AppendLine(
            """
            ORDER BY
                array_position(
                    string_to_array(lower(search_text), ' '),
                    lower(@SearchTerm)
                ) ASC NULLS LAST
            """);
    }
    else
    {
        ReadSqlHelper.AddOrdering(
            sql,
            query.SortField,
            query.SortAscending);  
    }
 

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

    ReadSqlHelper.AddAggregatesFilter(
        sql,
        parameters,
        query.AggregatesFilter);

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

    ReadSqlHelper.AddLicenceNumbersFilter(
        sql,
        parameters,
        query.LicenceNumbers);
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