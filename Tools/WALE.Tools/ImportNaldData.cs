using System.Globalization;
using System.Text;
using Npgsql;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.Tools.Config;

namespace WALE.Tools;

public static class ImportNaldData
{
    private static Dictionary<string, Dictionary<string, NpgsqlTypes.NpgsqlDbType>>? _schemaCache;

    public static async Task ImportAsync()
    {
        Console.WriteLine("Starting NALD data import...");
        var dumpFolder = KeyConfig.NaldDataDumpFolder;

        if (!Directory.Exists(dumpFolder))
        {
            Console.WriteLine($"Error: NALD dump folder not found at {dumpFolder}");
            return;
        }

        var files = Directory.GetFiles(dumpFolder, "NALD_*.txt");
        Console.WriteLine($"Found {files.Length} files to import.");

        NpgsqlDataSourceProvider npgsqlDataSourceProvider = new(
            KeyConfig.PostgresHost,
            KeyConfig.PostgresPort,
            KeyConfig.PostgresDbName,
            KeyConfig.PostgresUsername,
            KeyConfig.PostgresPassword);

        await using var dataSource = npgsqlDataSourceProvider.DataSource;

        // Cache the schema information once
        await LoadSchemaAsync(dataSource);

        await using var fkConnection = await dataSource.OpenConnectionAsync();
        await DropForeignKeysAsync(fkConnection);

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            Console.WriteLine($"Truncating {fileName}...");

            try
            {
                await TruncateTableAsync(dataSource, fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error truncating {fileName}: {ex.Message}");
            }
        }

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            Console.WriteLine($"Importing {fileName}...");

            try
            {
                await ImportFileAsync(dataSource, filePath, fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error importing {fileName}: {ex.Message}");
            }
        }

        await RecreateForeignKeysAsync(fkConnection);

        Console.WriteLine("NALD data import completed.");
    }

    private static async Task LoadSchemaAsync(NpgsqlDataSource dataSource)
    {
        _schemaCache = new Dictionary<string, Dictionary<string, NpgsqlTypes.NpgsqlDbType>>();

        await using var connection = await dataSource.OpenConnectionAsync();

        var sql = @"
            SELECT 
                table_name,
                column_name,
                data_type,
                udt_name
            FROM information_schema.columns
            WHERE table_schema = 'nald'
            ORDER BY table_name, ordinal_position";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(0);
            var columnName = reader.GetString(1);
            var dataType = reader.GetString(2);
            var udtName = reader.GetString(3);

            if (!_schemaCache.ContainsKey(tableName))
            {
                _schemaCache[tableName] = new Dictionary<string, NpgsqlTypes.NpgsqlDbType>();
            }

            _schemaCache[tableName][columnName] = MapPostgresTypeToNpgsqlDbType(dataType, udtName);
        }
    }

    private static NpgsqlTypes.NpgsqlDbType MapPostgresTypeToNpgsqlDbType(string dataType, string udtName)
    {
        return dataType.ToLower() switch
        {
            "smallint" => NpgsqlTypes.NpgsqlDbType.Smallint,
            "integer" => NpgsqlTypes.NpgsqlDbType.Integer,
            "bigint" => NpgsqlTypes.NpgsqlDbType.Bigint,
            "numeric" => NpgsqlTypes.NpgsqlDbType.Numeric,
            "real" => NpgsqlTypes.NpgsqlDbType.Real,
            "double precision" => NpgsqlTypes.NpgsqlDbType.Double,
            "character varying" => NpgsqlTypes.NpgsqlDbType.Varchar,
            "character" => NpgsqlTypes.NpgsqlDbType.Char,
            "text" => NpgsqlTypes.NpgsqlDbType.Text,
            "timestamp without time zone" => NpgsqlTypes.NpgsqlDbType.Timestamp,
            "timestamp with time zone" => NpgsqlTypes.NpgsqlDbType.TimestampTz,
            "date" => NpgsqlTypes.NpgsqlDbType.Date,
            "boolean" => NpgsqlTypes.NpgsqlDbType.Boolean,
            _ => NpgsqlTypes.NpgsqlDbType.Text
        };
    }

    private static async Task WriteTypedValueAsync(NpgsqlBinaryImporter writer, string value,
        NpgsqlTypes.NpgsqlDbType type)
    {
        switch (type)
        {
            case NpgsqlTypes.NpgsqlDbType.Smallint:
                await writer.WriteAsync(short.Parse(value, CultureInfo.InvariantCulture), type);
                break;
            case NpgsqlTypes.NpgsqlDbType.Integer:
                await writer.WriteAsync(int.Parse(value, CultureInfo.InvariantCulture), type);
                break;
            case NpgsqlTypes.NpgsqlDbType.Bigint:
                await writer.WriteAsync(long.Parse(value, CultureInfo.InvariantCulture), type);
                break;
            case NpgsqlTypes.NpgsqlDbType.Numeric:
            case NpgsqlTypes.NpgsqlDbType.Real:
            case NpgsqlTypes.NpgsqlDbType.Double:
                await writer.WriteAsync(decimal.Parse(value, CultureInfo.InvariantCulture), type);
                break;
            case NpgsqlTypes.NpgsqlDbType.Timestamp:
            case NpgsqlTypes.NpgsqlDbType.TimestampTz:
            case NpgsqlTypes.NpgsqlDbType.Date:
                string[] formats =
                [
                    "dd/MM/yyyy",
                    "d/M/yyyy",
                    "yyyy-MM-dd",
                    "yyyy-MM-dd HH:mm:ss",
                    "yyyyMMddHHmmss",
                    "dd/MM/yyyy HH:mm:ss",
                    "d/M/yyyy HH:mm:ss"
                ];
                if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var result))
                {
                    await writer.WriteAsync(result, type);
                }
                else
                {
                    await writer.WriteAsync(DateTime.Parse(value, CultureInfo.InvariantCulture), type);
                }

                break;
            default:
                await writer.WriteAsync(value, type);
                break;
        }
    }

    private static async Task DropForeignKeysAsync(NpgsqlConnection connection)
    {
        Console.WriteLine("Dropping all foreign keys in nald schema...");
        var sql = @"
            DO $$ 
            DECLARE 
                r RECORD;
            BEGIN
                FOR r IN (SELECT constraint_name, table_name 
                          FROM information_schema.table_constraints 
                          WHERE constraint_type = 'FOREIGN KEY' AND table_schema = 'nald') 
                LOOP
                    EXECUTE 'ALTER TABLE nald.' || quote_ident(r.table_name) || ' DROP CONSTRAINT ' || quote_ident(r.constraint_name);
                END LOOP;
            END $$;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteSqlAsync(NpgsqlConnection connection, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task RecreateForeignKeysAsync(NpgsqlConnection connection)
    {
        Console.WriteLine("Re-creating foreign keys...");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_EXCEPTIONS\" ADD CONSTRAINT \"AABE_AABL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABL_ID\") REFERENCES nald.\"NALD_ABS_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_EXCEPTIONS\" ADD CONSTRAINT \"AABE_AABV_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABV_ID\", \"AABV_ISSUE_NO\", \"AABV_INCR_NO\") REFERENCES nald.\"NALD_ABS_LIC_VERSIONS\" (\"FGAC_REGION_CODE\", \"AABL_ID\", \"ISSUE_NO\", \"INCR_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_EXCEPTIONS\" ADD CONSTRAINT \"AABE_AAYR_FK\" FOREIGN KEY (\"AAYR_ARYR_CODE\", \"AAYR_YEAR\") REFERENCES nald.\"NALD_ABSTAT_YEARS\" (\"ARYR_CODE\", \"YEAR\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_EXCEPTIONS\" ADD CONSTRAINT \"AABE_APUR_FK\" FOREIGN KEY (\"APUR_APPR_CODE\", \"APUR_APSE_CODE\", \"APUR_APUS_CODE\") REFERENCES nald.\"NALD_PURPOSES\" (\"APPR_CODE\", \"APSE_CODE\", \"APUS_CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_EXCEPTIONS\" ADD CONSTRAINT \"AABE_ARTY_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ARTY_ID\") REFERENCES nald.\"NALD_RET_FORMATS\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_EXCEPTIONS\" ADD CONSTRAINT \"AABE_NMES_FK\" FOREIGN KEY (\"NMES_MESSAGE_NUMBER\") REFERENCES nald.\"NALD_MESSAGES\" (\"MESSAGE_NUMBER\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LICENCES\" ADD CONSTRAINT \"AABL_AREP_FK1\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AREP_SUC_CODE\") REFERENCES nald.\"NALD_REP_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LICENCES\" ADD CONSTRAINT \"AABL_AREP_FK2\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AREP_LEAP_CODE\") REFERENCES nald.\"NALD_REP_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LICENCES\" ADD CONSTRAINT \"AABL_AREP_FK3\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AREP_AREA_CODE\") REFERENCES nald.\"NALD_REP_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LICENCES\" ADD CONSTRAINT \"AABL_AREP_FK4\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AREP_CAMS_CODE\") REFERENCES nald.\"NALD_REP_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LIC_PURPOSES\" ADD CONSTRAINT \"AABP_AABV_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABV_AABL_ID\", \"AABV_ISSUE_NO\", \"AABV_INCR_NO\") REFERENCES nald.\"NALD_ABS_LIC_VERSIONS\" (\"FGAC_REGION_CODE\", \"AABL_ID\", \"ISSUE_NO\", \"INCR_NO\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LIC_PURPOSES\" ADD CONSTRAINT \"AABP_AMOM_FK\" FOREIGN KEY (\"AMOM_CODE\") REFERENCES nald.\"NALD_MEANS_OF_MEASURE\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LIC_PURPOSES\" ADD CONSTRAINT \"AABP_APUR_FK\" FOREIGN KEY (\"APUR_APPR_CODE\", \"APUR_APSE_CODE\", \"APUR_APUS_CODE\") REFERENCES nald.\"NALD_PURPOSES\" (\"APPR_CODE\", \"APSE_CODE\", \"APUS_CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LIC_PURPOSES\" ADD CONSTRAINT \"AABP_AREC_FK\" FOREIGN KEY (\"AREC_CODE\") REFERENCES nald.\"NALD_LH_REC_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LIC_VERSIONS\" ADD CONSTRAINT \"AABV_AABL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABL_ID\") REFERENCES nald.\"NALD_ABS_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LIC_VERSIONS\" ADD CONSTRAINT \"AABV_ACCL_FK\" FOREIGN KEY (\"ACCL_CODE\") REFERENCES nald.\"NALD_CRIT_CLASSES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LIC_VERSIONS\" ADD CONSTRAINT \"AABV_ACON_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACON_APAR_ID\", \"ACON_AADD_ID\") REFERENCES nald.\"NALD_CONTACTS\" (\"FGAC_REGION_CODE\", \"APAR_ID\", \"AADD_ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LIC_VERSIONS\" ADD CONSTRAINT \"AABV_ALTY_FK\" FOREIGN KEY (\"ALTY_CODE\") REFERENCES nald.\"NALD_LIC_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LIC_VERSIONS\" ADD CONSTRAINT \"AABV_ALWA_FK\" FOREIGN KEY (\"WA_ALTY_CODE\") REFERENCES nald.\"NALD_WA_LIC_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LIC_VERSIONS\" ADD CONSTRAINT \"AABV_ASRC_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ASRC_CODE\") REFERENCES nald.\"NALD_SOURCES\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LIC_VERSIONS\" ADD CONSTRAINT \"AABV_DEDE_FK\" FOREIGN KEY (\"DEREG_CODE\") REFERENCES nald.\"NALD_DEREG_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ADDRESSES\" ADD CONSTRAINT \"AADD_APCO_FK\" FOREIGN KEY (\"APCO_CODE\") REFERENCES nald.\"NALD_POSTAL_COUNTIES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_POINTS\" ADD CONSTRAINT \"AAIP_AADD_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AADD_ID\") REFERENCES nald.\"NALD_ADDRESSES\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_POINTS\" ADD CONSTRAINT \"AAIP_AAPC_FK\" FOREIGN KEY (\"AAPC_CODE\") REFERENCES nald.\"NALD_POINT_CATEGORIES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_POINTS\" ADD CONSTRAINT \"AAIP_AAPT_FK\" FOREIGN KEY (\"AAPT_APTP_CODE\", \"AAPT_APTS_CODE\") REFERENCES nald.\"NALD_POINT_TYPES\" (\"APTP_CODE\", \"APTS_CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_POINTS\" ADD CONSTRAINT \"AAIP_ABAN_FK\" FOREIGN KEY (\"ABAN_CODE\") REFERENCES nald.\"NALD_BANK_CODES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_POINTS\" ADD CONSTRAINT \"AAIP_ASRC_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ASRC_CODE\") REFERENCES nald.\"NALD_SOURCES\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_LIC_QUANTITIES\" ADD CONSTRAINT \"AALQ_AABV_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABV_AABL_ID\", \"AABV_ISSUE_NO\", \"AABV_INCR_NO\") REFERENCES nald.\"NALD_ABS_LIC_VERSIONS\" (\"FGAC_REGION_CODE\", \"AABL_ID\", \"ISSUE_NO\", \"INCR_NO\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_PURP_POINTS\" ADD CONSTRAINT \"AAPO_AABP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABP_ID\") REFERENCES nald.\"NALD_ABS_LIC_PURPOSES\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_PURP_POINTS\" ADD CONSTRAINT \"AAPO_AAIP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AAIP_ID\") REFERENCES nald.\"NALD_POINTS\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABS_PURP_POINTS\" ADD CONSTRAINT \"AAPO_AMOA_FK\" FOREIGN KEY (\"AMOA_CODE\") REFERENCES nald.\"NALD_MEANS_OF_ABS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_POINT_TYPES\" ADD CONSTRAINT \"AAPT_APTP_FK\" FOREIGN KEY (\"APTP_CODE\") REFERENCES nald.\"NALD_POINT_TYPE_PRIMS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_POINT_TYPES\" ADD CONSTRAINT \"AAPT_APTS_FK\" FOREIGN KEY (\"APTS_CODE\") REFERENCES nald.\"NALD_POINT_TYPE_SECS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_YEARS\" ADD CONSTRAINT \"AAYR_ARYR_FK\" FOREIGN KEY (\"ARYR_CODE\") REFERENCES nald.\"NALD_YEAR_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_CHGVERSIONS\" ADD CONSTRAINT \"ABCV_ABRN_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ABRN_FIN_YEAR\", \"ABRN_BILL_RUN_NO\") REFERENCES nald.\"NALD_BILL_RUNS\" (\"FGAC_REGION_CODE\", \"FIN_YEAR\", \"BILL_RUN_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_CHGVERSIONS\" ADD CONSTRAINT \"ABCV_ACVR_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACVR_AABL_ID\", \"ACVR_VERS_NO\") REFERENCES nald.\"NALD_CHG_VERSIONS\" (\"FGAC_REGION_CODE\", \"AABL_ID\", \"VERS_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_ERRORS\" ADD CONSTRAINT \"ABER_ABRN_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ABRN_FIN_YEAR\", \"ABRN_BILL_RUN_NO\") REFERENCES nald.\"NALD_BILL_RUNS\" (\"FGAC_REGION_CODE\", \"FIN_YEAR\", \"BILL_RUN_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_ERRORS\" ADD CONSTRAINT \"ABER_NMES_FK\" FOREIGN KEY (\"NMES_MESSAGE_NUMBER\") REFERENCES nald.\"NALD_MESSAGES\" (\"MESSAGE_NUMBER\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_HEADERS\" ADD CONSTRAINT \"ABHD_ABHD_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ABHD_ID\") REFERENCES nald.\"NALD_BILL_HEADERS\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_HEADERS\" ADD CONSTRAINT \"ABHD_ABRN_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ABRN_FIN_YEAR\", \"ABRN_BILL_RUN_NO\") REFERENCES nald.\"NALD_BILL_RUNS\" (\"FGAC_REGION_CODE\", \"FIN_YEAR\", \"BILL_RUN_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_HEADERS\" ADD CONSTRAINT \"ABHD_AIIA_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"LH_ACC_NO\", \"IAS_CUST_REF\") REFERENCES nald.\"NALD_IAS_INVOICE_ACCS\" (\"FGAC_REGION_CODE\", \"ALHA_ACC_NO\", \"IAS_CUST_REF\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_HEADERS\" ADD CONSTRAINT \"ABHD_ALHA_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"LH_ACC_NO\") REFERENCES nald.\"NALD_LH_ACCS\" (\"FGAC_REGION_CODE\", \"ACC_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_PROCESSES\" ADD CONSTRAINT \"ABPR_ABRN_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ABRN_FIN_YEAR\", \"ABRN_BILL_RUN_NO\") REFERENCES nald.\"NALD_BILL_RUNS\" (\"FGAC_REGION_CODE\", \"FIN_YEAR\", \"BILL_RUN_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_TRANS\" ADD CONSTRAINT \"ABTN_AABL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"LIC_ID\") REFERENCES nald.\"NALD_ABS_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_TRANS\" ADD CONSTRAINT \"ABTN_ABHD_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ABHD_ID\") REFERENCES nald.\"NALD_BILL_HEADERS\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_TRANS\" ADD CONSTRAINT \"ABTN_ABRN_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ABRN_FIN_YEAR\", \"ABRN_BILL_RUN_NO\") REFERENCES nald.\"NALD_BILL_RUNS\" (\"FGAC_REGION_CODE\", \"FIN_YEAR\", \"BILL_RUN_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_TRANS\" ADD CONSTRAINT \"ABTN_ACEL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACEL_ID\") REFERENCES nald.\"NALD_CHG_ELEMENTS\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_TRANS\" ADD CONSTRAINT \"ABTN_ACVR_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"LIC_ID\", \"VERS_NO\") REFERENCES nald.\"NALD_CHG_VERSIONS\" (\"FGAC_REGION_CODE\", \"AABL_ID\", \"VERS_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_TRANS\" ADD CONSTRAINT \"ABTN_AVAT_FK\" FOREIGN KEY (\"VAT_CODE\") REFERENCES nald.\"NALD_VAT_CODES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_TPT_RETURNS\" ADD CONSTRAINT \"ABTP_ABRN_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ABRN_FIN_YEAR\", \"ABRN_BILL_RUN_NO\") REFERENCES nald.\"NALD_BILL_RUNS\" (\"FGAC_REGION_CODE\", \"FIN_YEAR\", \"BILL_RUN_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_TPT_RETURNS\" ADD CONSTRAINT \"ABTP_ACEL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACEL_ID\") REFERENCES nald.\"NALD_CHG_ELEMENTS\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_BILL_YEARS\" ADD CONSTRAINT \"ABYR_ABCV_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ABCV_ABRN_FIN_YEAR\", \"ABCV_ABRN_BILL_RUN_NO\", \"ABCV_ACVR_AABL_ID\", \"ABCV_ACVR_VERS_NO\") REFERENCES nald.\"NALD_BILL_CHGVERSIONS\" (\"FGAC_REGION_CODE\", \"ABRN_FIN_YEAR\", \"ABRN_BILL_RUN_NO\", \"ACVR_AABL_ID\", \"ACVR_VERS_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CHG_ELEMENTS\" ADD CONSTRAINT \"ACEL_ACVR_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACVR_AABL_ID\", \"ACVR_VERS_NO\") REFERENCES nald.\"NALD_CHG_VERSIONS\" (\"FGAC_REGION_CODE\", \"AABL_ID\", \"VERS_NO\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CHG_ELEMENTS\" ADD CONSTRAINT \"ACEL_ALSF_FK\" FOREIGN KEY (\"ALSF_CODE\") REFERENCES nald.\"NALD_LOSS_FACTORS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CHG_ELEMENTS\" ADD CONSTRAINT \"ACEL_APUR_FK\" FOREIGN KEY (\"APUR_APPR_CODE\", \"APUR_APSE_CODE\", \"APUR_APUS_CODE\") REFERENCES nald.\"NALD_PURPOSES\" (\"APPR_CODE\", \"APSE_CODE\", \"APUS_CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CHG_ELEMENTS\" ADD CONSTRAINT \"ACEL_ASFT_FK1\" FOREIGN KEY (\"ASFT_CODE\") REFERENCES nald.\"NALD_SEAS_FACTORS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CHG_ELEMENTS\" ADD CONSTRAINT \"ACEL_ASFT_FK2\" FOREIGN KEY (\"ASFT_CODE_DERIVED\") REFERENCES nald.\"NALD_SEAS_FACTORS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CHG_ELEMENTS\" ADD CONSTRAINT \"ACEL_ASRF_FK\" FOREIGN KEY (\"ASRF_CODE\") REFERENCES nald.\"NALD_SRC_FACTORS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CTRL_FLOWS\" ADD CONSTRAINT \"ACFL_AMAN_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AMAN_CODE\") REFERENCES nald.\"NALD_MAN_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CTRL_LEVELS\" ADD CONSTRAINT \"ACLE_AMAN_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AMAN_CODE\") REFERENCES nald.\"NALD_MAN_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CONT_NOS\" ADD CONSTRAINT \"ACNO_ACNT_FK\" FOREIGN KEY (\"ACNT_CODE\") REFERENCES nald.\"NALD_CONT_NO_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CONT_NOS\" ADD CONSTRAINT \"ACNO_ACON_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACON_APAR_ID\", \"ACON_AADD_ID\") REFERENCES nald.\"NALD_CONTACTS\" (\"FGAC_REGION_CODE\", \"APAR_ID\", \"AADD_ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CONTACTS\" ADD CONSTRAINT \"ACON_AADD_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AADD_ID\") REFERENCES nald.\"NALD_ADDRESSES\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CONTACTS\" ADD CONSTRAINT \"ACON_APAR_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"APAR_ID\") REFERENCES nald.\"NALD_PARTIES\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CHG_AGRMNTS\" ADD CONSTRAINT \"ACSA_ACEL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACEL_ID\") REFERENCES nald.\"NALD_CHG_ELEMENTS\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CHG_AGRMNTS\" ADD CONSTRAINT \"ACSA_AFSA_FK\" FOREIGN KEY (\"AFSA_CODE\") REFERENCES nald.\"NALD_FIN_AGRMNT_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_CAT_USES\" ADD CONSTRAINT \"ACUR_AARC_FK\" FOREIGN KEY (\"AARC_STAT_REF\") REFERENCES nald.\"NALD_ABSTAT_CATGRIES\" (\"STAT_REF\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_CAT_USES\" ADD CONSTRAINT \"ACUR_APUS_FK1\" FOREIGN KEY (\"APUS_CODE_FROM\") REFERENCES nald.\"NALD_PURP_USES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_CAT_USES\" ADD CONSTRAINT \"ACUR_APUS_FK2\" FOREIGN KEY (\"APUS_CODE_TO\") REFERENCES nald.\"NALD_PURP_USES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CHG_VERSIONS\" ADD CONSTRAINT \"ACVR_AABL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABL_ID\") REFERENCES nald.\"NALD_ABS_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_CHG_VERSIONS\" ADD CONSTRAINT \"ACVR_AIIA_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AIIA_ALHA_ACC_NO\", \"AIIA_IAS_CUST_REF\") REFERENCES nald.\"NALD_IAS_INVOICE_ACCS\" (\"FGAC_REGION_CODE\", \"ALHA_ACC_NO\", \"IAS_CUST_REF\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_DOCUMENT_REFS\" ADD CONSTRAINT \"ADRF_AABL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABL_ID\") REFERENCES nald.\"NALD_ABS_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_DOCUMENT_REFS\" ADD CONSTRAINT \"ADRF_AIMP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AIMP_ID\") REFERENCES nald.\"NALD_IMP_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_EIUC_VALS\" ADD CONSTRAINT \"AEIUV_AREP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AREP_CODE\") REFERENCES nald.\"NALD_REP_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_GROUP_LH_ACCS\" ADD CONSTRAINT \"AGCA_ACON_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACON_APAR_ID\", \"ACON_AADD_ID\") REFERENCES nald.\"NALD_CONTACTS\" (\"FGAC_REGION_CODE\", \"APAR_ID\", \"AADD_ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IAS_INVOICE_ACCS\" ADD CONSTRAINT \"AIIA_ACON_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACON_APAR_ID\", \"ACON_AADD_ID\") REFERENCES nald.\"NALD_CONTACTS\" (\"FGAC_REGION_CODE\", \"APAR_ID\", \"AADD_ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IAS_INVOICE_ACCS\" ADD CONSTRAINT \"AIIA_ALHA_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ALHA_ACC_NO\") REFERENCES nald.\"NALD_LH_ACCS\" (\"FGAC_REGION_CODE\", \"ACC_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_LICENCES\" ADD CONSTRAINT \"AIMP_AREP_FK1\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"LEAP\") REFERENCES nald.\"NALD_REP_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_LICENCES\" ADD CONSTRAINT \"AIMP_AREP_FK2\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AREA\") REFERENCES nald.\"NALD_REP_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_LICENCES\" ADD CONSTRAINT \"AIMP_AREP_FK3\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"CAMS\") REFERENCES nald.\"NALD_REP_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_LIC_VERSIONS\" ADD CONSTRAINT \"AIMV_ACCL_FK\" FOREIGN KEY (\"ACCL_CODE\") REFERENCES nald.\"NALD_CRIT_CLASSES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_LIC_VERSIONS\" ADD CONSTRAINT \"AIMV_ACON_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACON_APAR_ID\", \"ACON_AADD_ID\") REFERENCES nald.\"NALD_CONTACTS\" (\"FGAC_REGION_CODE\", \"APAR_ID\", \"AADD_ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_LIC_VERSIONS\" ADD CONSTRAINT \"AIMV_AIMP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AIMP_ID\") REFERENCES nald.\"NALD_IMP_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_LIC_VERSIONS\" ADD CONSTRAINT \"AIMV_ASRC_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ASRC_CODE\") REFERENCES nald.\"NALD_SOURCES\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_PURP_POINTS\" ADD CONSTRAINT \"AIPO_AAIP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AAIP_ID\") REFERENCES nald.\"NALD_POINTS\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_PURP_POINTS\" ADD CONSTRAINT \"AIPO_AIPU_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AIPU_ID\") REFERENCES nald.\"NALD_IMP_LIC_PURPOSES\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_PURP_POINTS\" ADD CONSTRAINT \"AIPO_IMOI_FK\" FOREIGN KEY (\"IMOI_CODE\") REFERENCES nald.\"NALD_MEANS_OF_IMP\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_LIC_PURPOSES\" ADD CONSTRAINT \"AIPU_AIMV_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AIMV_AIMP_ID\", \"AIMV_ISSUE_NO\", \"AIMV_INCR_NO\") REFERENCES nald.\"NALD_IMP_LIC_VERSIONS\" (\"FGAC_REGION_CODE\", \"AIMP_ID\", \"ISSUE_NO\", \"INCR_NO\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_LIC_PURPOSES\" ADD CONSTRAINT \"AIPU_AISI_FK\" FOREIGN KEY (\"AISI_CODE\") REFERENCES nald.\"NALD_IMP_SITE_STATUSES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_LIC_PURPOSES\" ADD CONSTRAINT \"AIPU_AMOI_FK\" FOREIGN KEY (\"AMOI_CODE\") REFERENCES nald.\"NALD_MEANS_OF_IMP\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_IMP_LIC_PURPOSES\" ADD CONSTRAINT \"AIPU_APUR_FK\" FOREIGN KEY (\"APUR_APPR_CODE\", \"APUR_APSE_CODE\", \"APUR_APUS_CODE\") REFERENCES nald.\"NALD_PURPOSES\" (\"APPR_CODE\", \"APSE_CODE\", \"APUS_CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LIC_AGRMNTS\" ADD CONSTRAINT \"ALAG_AABP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABP_ID\") REFERENCES nald.\"NALD_ABS_LIC_PURPOSES\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LIC_AGRMNTS\" ADD CONSTRAINT \"ALAG_AIPU_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AIPU_ID\") REFERENCES nald.\"NALD_IMP_LIC_PURPOSES\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LIC_AGRMNTS\" ADD CONSTRAINT \"ALAG_ALSA_FK\" FOREIGN KEY (\"ALSA_CODE\") REFERENCES nald.\"NALD_LIC_AGRMNT_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LIC_CONDITIONS\" ADD CONSTRAINT \"ALCO_AABP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABP_ID\") REFERENCES nald.\"NALD_ABS_LIC_PURPOSES\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LIC_CONDITIONS\" ADD CONSTRAINT \"ALCO_ACIN_FK\" FOREIGN KEY (\"ACIN_CODE\", \"ACIN_SUBCODE\") REFERENCES nald.\"NALD_LIC_COND_TYPES\" (\"CODE\", \"SUBCODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LIC_CONDITIONS\" ADD CONSTRAINT \"ALCO_AIPU_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AIPU_ID\") REFERENCES nald.\"NALD_IMP_LIC_PURPOSES\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LOSS_FACTOR_VALS\" ADD CONSTRAINT \"ALFV_ALSF_FK\" FOREIGN KEY (\"ALSF_CODE\") REFERENCES nald.\"NALD_LOSS_FACTORS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LH_ACCS\" ADD CONSTRAINT \"ALHA_ACON_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACON_APAR_ID\", \"ACON_AADD_ID\") REFERENCES nald.\"NALD_CONTACTS\" (\"FGAC_REGION_CODE\", \"APAR_ID\", \"AADD_ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LH_ACCS\" ADD CONSTRAINT \"ALHA_AGCA_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AGCA_ACC_NO\") REFERENCES nald.\"NALD_GROUP_LH_ACCS\" (\"FGAC_REGION_CODE\", \"ACC_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LH_AGRMNTS\" ADD CONSTRAINT \"ALHS_AFSA_FK\" FOREIGN KEY (\"AFSA_CODE\") REFERENCES nald.\"NALD_FIN_AGRMNT_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LH_AGRMNTS\" ADD CONSTRAINT \"ALHS_ALHA_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ALHA_ACC_NO\") REFERENCES nald.\"NALD_LH_ACCS\" (\"FGAC_REGION_CODE\", \"ACC_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LIC_ROLES\" ADD CONSTRAINT \"ALRO_AABL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABL_ID\") REFERENCES nald.\"NALD_ABS_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LIC_ROLES\" ADD CONSTRAINT \"ALRO_ACON_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACON_APAR_ID\", \"ACON_AADD_ID\") REFERENCES nald.\"NALD_CONTACTS\" (\"FGAC_REGION_CODE\", \"APAR_ID\", \"AADD_ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LIC_ROLES\" ADD CONSTRAINT \"ALRO_AIMP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AIMP_ID\") REFERENCES nald.\"NALD_IMP_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LIC_ROLES\" ADD CONSTRAINT \"ALRO_ALRT_FK\" FOREIGN KEY (\"ALRT_CODE\") REFERENCES nald.\"NALD_LIC_ROLE_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LH_SUSP_LOGS\" ADD CONSTRAINT \"ALSL_ALHA_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ALHA_ACC_NO\") REFERENCES nald.\"NALD_LH_ACCS\" (\"FGAC_REGION_CODE\", \"ACC_NO\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_LH_SUSP_LOGS\" ADD CONSTRAINT \"ALSL_AMRE_FK\" FOREIGN KEY (\"AMRE_AMRE_TYPE\", \"AMRE_CODE\") REFERENCES nald.\"NALD_MOD_REASONS\" (\"AMRE_TYPE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MAN_UNITS\" ADD CONSTRAINT \"AMAN_AMAN_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AMAN_CODE\") REFERENCES nald.\"NALD_MAN_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MAN_UNITS\" ADD CONSTRAINT \"AMAN_AMLA_FK\" FOREIGN KEY (\"AMLA_CODE\") REFERENCES nald.\"NALD_LIC_AVAILS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MAN_UNITS\" ADD CONSTRAINT \"AMAN_APFR_FK\" FOREIGN KEY (\"APFR_CODE\") REFERENCES nald.\"NALD_PRES_FLOW_RESTS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MAN_UNITS\" ADD CONSTRAINT \"AMAN_APTY_FK\" FOREIGN KEY (\"APTY_CODE\") REFERENCES nald.\"NALD_CTRL_POINT_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MAN_UNITS\" ADD CONSTRAINT \"AMAN_ASLA_FK\" FOREIGN KEY (\"ASLA_CODE\") REFERENCES nald.\"NALD_SEAS_LIC_AVAILS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MAN_UNITS\" ADD CONSTRAINT \"AMAN_ATLL_FK\" FOREIGN KEY (\"ATLL_CODE\") REFERENCES nald.\"NALD_TIMELTD_AVAILS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MOD_LOGS\" ADD CONSTRAINT \"AMOD_AABL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABL_ID\") REFERENCES nald.\"NALD_ABS_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MOD_LOGS\" ADD CONSTRAINT \"AMOD_AABV_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABV_AABL_ID\", \"AABV_ISSUE_NO\", \"AABV_INCR_NO\") REFERENCES nald.\"NALD_ABS_LIC_VERSIONS\" (\"FGAC_REGION_CODE\", \"AABL_ID\", \"ISSUE_NO\", \"INCR_NO\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MOD_LOGS\" ADD CONSTRAINT \"AMOD_ACVR_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACVR_AABL_ID\", \"ACVR_VERS_NO\") REFERENCES nald.\"NALD_CHG_VERSIONS\" (\"FGAC_REGION_CODE\", \"AABL_ID\", \"VERS_NO\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MOD_LOGS\" ADD CONSTRAINT \"AMOD_AIMP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AIMP_ID\") REFERENCES nald.\"NALD_IMP_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MOD_LOGS\" ADD CONSTRAINT \"AMOD_AIMV_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AIMV_AIMP_ID\", \"AIMV_ISSUE_NO\", \"AIMV_INCR_NO\") REFERENCES nald.\"NALD_IMP_LIC_VERSIONS\" (\"FGAC_REGION_CODE\", \"AIMP_ID\", \"ISSUE_NO\", \"INCR_NO\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MOD_LOGS\" ADD CONSTRAINT \"AMOD_AMRE_FK\" FOREIGN KEY (\"AMRE_AMRE_TYPE\", \"AMRE_CODE\") REFERENCES nald.\"NALD_MOD_REASONS\" (\"AMRE_TYPE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MOD_LOGS\" ADD CONSTRAINT \"AMOD_ARVN_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ARVN_AABL_ID\", \"ARVN_VERS_NO\") REFERENCES nald.\"NALD_RET_VERSIONS\" (\"FGAC_REGION_CODE\", \"AABL_ID\", \"VERS_NO\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MAN_UNIT_POINTS\" ADD CONSTRAINT \"AMUP_AAIP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AAIP_ID\") REFERENCES nald.\"NALD_POINTS\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_MAN_UNIT_POINTS\" ADD CONSTRAINT \"AMUP_AMAN_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AMAN_CODE\") REFERENCES nald.\"NALD_MAN_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_PARTIES\" ADD CONSTRAINT \"APAR_ASIC_FK\" FOREIGN KEY (\"ASIC_ASID_DIVISION\", \"ASIC_CLASS\") REFERENCES nald.\"NALD_STDIND_CLASSES\" (\"ASID_DIVISION\", \"CLASS\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_PROC_DETAILS\" ADD CONSTRAINT \"APRD_NMES_FK\" FOREIGN KEY (\"NMES_MESSAGE_NUMBER\") REFERENCES nald.\"NALD_MESSAGES\" (\"MESSAGE_NUMBER\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_CAT_PRIMS\" ADD CONSTRAINT \"APSC_AARC_FK\" FOREIGN KEY (\"AARC_STAT_REF\") REFERENCES nald.\"NALD_ABSTAT_CATGRIES\" (\"STAT_REF\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_CAT_PRIMS\" ADD CONSTRAINT \"APSC_APPR_FK\" FOREIGN KEY (\"APPR_CODE\") REFERENCES nald.\"NALD_PURP_PRIMS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_PURPOSES\" ADD CONSTRAINT \"APUR_APPR_FK\" FOREIGN KEY (\"APPR_CODE\") REFERENCES nald.\"NALD_PURP_PRIMS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_PURPOSES\" ADD CONSTRAINT \"APUR_APSE_FK\" FOREIGN KEY (\"APSE_CODE\") REFERENCES nald.\"NALD_PURP_SECS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_PURPOSES\" ADD CONSTRAINT \"APUR_APUS_FK\" FOREIGN KEY (\"APUS_CODE\") REFERENCES nald.\"NALD_PURP_USES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_PURP_USES\" ADD CONSTRAINT \"APUS_ALSF_FK\" FOREIGN KEY (\"ALSF_CODE\") REFERENCES nald.\"NALD_LOSS_FACTORS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_TOTALS\" ADD CONSTRAINT \"ARAB_AABL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABL_ID\") REFERENCES nald.\"NALD_ABS_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_TOTALS\" ADD CONSTRAINT \"ARAB_AAYR_FK\" FOREIGN KEY (\"AAYR_ARYR_CODE\", \"AAYR_YEAR\") REFERENCES nald.\"NALD_ABSTAT_YEARS\" (\"ARYR_CODE\", \"YEAR\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_TOTALS\" ADD CONSTRAINT \"ARAB_APUR_FK\" FOREIGN KEY (\"APUR_APPR_CODE\", \"APUR_APSE_CODE\", \"APUR_APUS_CODE\") REFERENCES nald.\"NALD_PURPOSES\" (\"APPR_CODE\", \"APSE_CODE\", \"APUS_CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_REPORT_DRIVERS\" ADD CONSTRAINT \"ARDR_APDR_FK\" FOREIGN KEY (\"APDR_NAME\") REFERENCES nald.\"NALD_PRINTER_DRIVERS\" (\"NAME\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_REPORT_DRIVERS\" ADD CONSTRAINT \"ARDR_ARTS_FK\" FOREIGN KEY (\"ARTS_NAME\") REFERENCES nald.\"NALD_REPORTS\" (\"NAME\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_REPORT_LICENCES\" ADD CONSTRAINT \"AREL_AABL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABL_ID\") REFERENCES nald.\"NALD_ABS_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_REP_UNITS\" ADD CONSTRAINT \"AREP_ACON_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACON_APAR_ID\", \"ACON_AADD_ID\") REFERENCES nald.\"NALD_CONTACTS\" (\"FGAC_REGION_CODE\", \"APAR_ID\", \"AADD_ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_REP_UNITS\" ADD CONSTRAINT \"AREP_AREP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AREP_CODE\") REFERENCES nald.\"NALD_REP_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_REP_UNITS\" ADD CONSTRAINT \"AREP_ARUT_FK\" FOREIGN KEY (\"ARUT_CODE\") REFERENCES nald.\"NALD_REP_UNIT_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_FORM_LOGS\" ADD CONSTRAINT \"ARFL_ACON_FK1\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACON_APAR_ID_TO\", \"ACON_AADD_ID_TO\") REFERENCES nald.\"NALD_CONTACTS\" (\"FGAC_REGION_CODE\", \"APAR_ID\", \"AADD_ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_FORM_LOGS\" ADD CONSTRAINT \"ARFL_ACON_FK2\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACON_APAR_ID_FROM\", \"ACON_AADD_ID_FROM\") REFERENCES nald.\"NALD_CONTACTS\" (\"FGAC_REGION_CODE\", \"APAR_ID\", \"AADD_ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_FORM_LOGS\" ADD CONSTRAINT \"ARFL_ALRO_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ALRO_ID\") REFERENCES nald.\"NALD_LIC_ROLES\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_FORM_LOGS\" ADD CONSTRAINT \"ARFL_ARTY_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ARTY_ID\") REFERENCES nald.\"NALD_RET_FORMATS\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_FMT_POINTS\" ADD CONSTRAINT \"ARFP_AAIP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AAIP_ID\") REFERENCES nald.\"NALD_POINTS\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_FMT_POINTS\" ADD CONSTRAINT \"ARFP_ARTY_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ARTY_ID\") REFERENCES nald.\"NALD_RET_FORMATS\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_LINES\" ADD CONSTRAINT \"ARLN_ARFL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ARFL_ARTY_ID\", \"ARFL_DATE_FROM\") REFERENCES nald.\"NALD_RET_FORM_LOGS\" (\"FGAC_REGION_CODE\", \"ARTY_ID\", \"DATE_FROM\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_LINES\" ADD CONSTRAINT \"ARLN_ATPT_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ATPT_ACEL_ID\", \"ATPT_FIN_YEAR\") REFERENCES nald.\"NALD_TPT_RETURNS\" (\"FGAC_REGION_CODE\", \"ACEL_ID\", \"FIN_YEAR\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_FMT_PURPOSES\" ADD CONSTRAINT \"ARPU_APUR_FK\" FOREIGN KEY (\"APUR_APPR_CODE\", \"APUR_APSE_CODE\", \"APUR_APUS_CODE\") REFERENCES nald.\"NALD_PURPOSES\" (\"APPR_CODE\", \"APSE_CODE\", \"APUS_CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_FMT_PURPOSES\" ADD CONSTRAINT \"ARPU_ARTY_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ARTY_ID\") REFERENCES nald.\"NALD_RET_FORMATS\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_FREQ_COMBS\" ADD CONSTRAINT \"ARTC_ARAF_FK\" FOREIGN KEY (\"ARAF_REC_FREQ_CODE\", \"ARAF_RET_FREQ_CODE\") REFERENCES nald.\"NALD_RET_AGENCY_FREQS\" (\"REC_FREQ_CODE\", \"RET_FREQ_CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_FREQ_COMBS\" ADD CONSTRAINT \"ARTC_ARCF_FK\" FOREIGN KEY (\"ARCF_CODE\") REFERENCES nald.\"NALD_RET_COL_FREQS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_FORMATS\" ADD CONSTRAINT \"ARTY_ARTC_FK\" FOREIGN KEY (\"ARTC_CODE\", \"ARTC_REC_FREQ_CODE\", \"ARTC_RET_FREQ_CODE\") REFERENCES nald.\"NALD_RET_FREQ_COMBS\" (\"ARCF_CODE\", \"ARAF_REC_FREQ_CODE\", \"ARAF_RET_FREQ_CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_FORMATS\" ADD CONSTRAINT \"ARTY_ARVN_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ARVN_AABL_ID\", \"ARVN_VERS_NO\") REFERENCES nald.\"NALD_RET_VERSIONS\" (\"FGAC_REGION_CODE\", \"AABL_ID\", \"VERS_NO\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_REP_UNIT_POINTS\" ADD CONSTRAINT \"ARUP_AAIP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AAIP_ID\") REFERENCES nald.\"NALD_POINTS\" (\"FGAC_REGION_CODE\", \"ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_REP_UNIT_POINTS\" ADD CONSTRAINT \"ARUP_AREP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AREP_CODE\") REFERENCES nald.\"NALD_REP_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_RET_VERSIONS\" ADD CONSTRAINT \"ARVN_AABL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AABL_ID\") REFERENCES nald.\"NALD_ABS_LICENCES\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_SEAS_FACTOR_VALS\" ADD CONSTRAINT \"ASFV_ASFT_FK\" FOREIGN KEY (\"ASFT_CODE\") REFERENCES nald.\"NALD_SEAS_FACTORS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_STDIND_CLASSES\" ADD CONSTRAINT \"ASIC_ASID_FK\" FOREIGN KEY (\"ASID_DIVISION\") REFERENCES nald.\"NALD_STDIND_DIVISIONS\" (\"DIVISION\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_FIN_AGRMNT_VALS\" ADD CONSTRAINT \"ASPV_AFSA_FK\" FOREIGN KEY (\"AFSA_CODE\") REFERENCES nald.\"NALD_FIN_AGRMNT_TYPES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_SRC_FACTOR_VALS\" ADD CONSTRAINT \"ASRV_ASRF_FK\" FOREIGN KEY (\"ASRF_CODE\") REFERENCES nald.\"NALD_SRC_FACTORS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_CAT_SECS\" ADD CONSTRAINT \"ASSC_AARC_FK\" FOREIGN KEY (\"AARC_STAT_REF\") REFERENCES nald.\"NALD_ABSTAT_CATGRIES\" (\"STAT_REF\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_ABSTAT_CAT_SECS\" ADD CONSTRAINT \"ASSC_APSE_FK\" FOREIGN KEY (\"APSE_CODE\") REFERENCES nald.\"NALD_PURP_SECS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_SUC_VALS\" ADD CONSTRAINT \"ASUV_AREP_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"AREP_CODE\") REFERENCES nald.\"NALD_REP_UNITS\" (\"FGAC_REGION_CODE\", \"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_TLP_FACTOR_VALS\" ADD CONSTRAINT \"ATLV_ASRF_FK\" FOREIGN KEY (\"ASRF_CODE\") REFERENCES nald.\"NALD_TLP_FACTORS\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_TPT_RETURNS\" ADD CONSTRAINT \"ATPT_ACEL_FK\" FOREIGN KEY (\"FGAC_REGION_CODE\", \"ACEL_ID\") REFERENCES nald.\"NALD_CHG_ELEMENTS\" (\"FGAC_REGION_CODE\", \"ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_VAT_RATES\" ADD CONSTRAINT \"AVCV_AVAT_FK\" FOREIGN KEY (\"AVAT_CODE\") REFERENCES nald.\"NALD_VAT_CODES\" (\"CODE\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_APP_FORM_HELP\" ADD CONSTRAINT \"NHLP_FK1\" FOREIGN KEY (\"HLP_MODTAB_NAME\") REFERENCES nald.\"NALD_SOFTWARE\" (\"SFT_ID\");");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_SOFT_BUTTON_PRIVS\" ADD CONSTRAINT \"NSBP_FK2\" FOREIGN KEY (\"SFT_ID\", \"BUTTON_NUMBER\") REFERENCES nald.\"NALD_SOFT_BUTTONS\" (\"SFT_ID\", \"BUTTON_NUMBER\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_SOFT_BUTTONS\" ADD CONSTRAINT \"NSBT_FK1\" FOREIGN KEY (\"SFT_ID\") REFERENCES nald.\"NALD_SOFTWARE\" (\"SFT_ID\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_SOFT_BUTTONS\" ADD CONSTRAINT \"NSBT_FK2\" FOREIGN KEY (\"BUTTON_NUMBER\") REFERENCES nald.\"NALD_BUTTONS\" (\"BUTTON_NUMBER\") ON DELETE CASCADE;");
        await ExecuteSqlAsync(connection,
            "ALTER TABLE nald.\"NALD_SOFTWARE_PRIVS\" ADD CONSTRAINT \"NSPR_FK1\" FOREIGN KEY (\"SFT_ID\") REFERENCES nald.\"NALD_SOFTWARE\" (\"SFT_ID\") ON DELETE CASCADE;");
    }

    private static async Task TruncateTableAsync(NpgsqlDataSource dataSource, string tableName)
    {
        await using var connection = await dataSource.OpenConnectionAsync();

        // Truncate table before import
        await using var truncateCmd = new NpgsqlCommand($"TRUNCATE TABLE nald.\"{tableName}\" CASCADE", connection);
        await truncateCmd.ExecuteNonQueryAsync();
    }

    private static async Task ImportFileAsync(NpgsqlDataSource dataSource, string filePath, string tableName)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        var headerLine = await reader.ReadLineAsync();

        if (headerLine == null)
        {
            return;
        }

        // Strip BOM if present
        if (headerLine.StartsWith('\uFEFF'))
        {
            headerLine = headerLine.Substring(1);
        }

        var columns = headerLine.Split(',').Select(c => c.Trim()).ToArray();

        // Filter out columns not in our schema (specifically SOURCE_CODE and BATCH_RUN_DATE)
        var columnsToImport = columns.Where(c => c != "SOURCE_CODE" && c != "BATCH_RUN_DATE").ToArray();
        var columnNames = string.Join(", ", columnsToImport.Select(c => $"\"{c}\""));

        await using var connection = await dataSource.OpenConnectionAsync();

        await using var writer = await connection.BeginBinaryImportAsync(
            $"COPY nald.\"{tableName}\" ({columnNames}) FROM STDIN (FORMAT BINARY)");

        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var charBuffer = new char[1];

        while (await reader.ReadAsync(charBuffer, 0, 1) > 0)
        {
            var c = charBuffer[0];
            if (c == '\"')
            {
                inQuotes = !inQuotes;
                // We keep the quotes for now to handle double-double quotes later, or we can handle them here.
                // Actually, let's handle them here for simplicity.
                // If we encounter a quote, and the next char is also a quote, it's an escaped quote.
                if (!inQuotes) // We just closed quotes, check if next is also quote
                {
                    int next = reader.Peek();
                    if (next == '\"')
                    {
                        current.Append('\"');
                        await reader.ReadAsync(charBuffer, 0, 1); // consume the second quote
                        inQuotes = true; // still in quotes
                    }
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else if ((c == '\n' || c == '\r') && !inQuotes)
            {
                if (c == '\r' && reader.Peek() == '\n')
                {
                    await reader.ReadAsync(charBuffer, 0, 1);
                }

                if (values.Count > 0 || current.Length > 0)
                {
                    values.Add(current.ToString());
                    await WriteRowAsync(writer, values, columns, columnsToImport, tableName);
                    values.Clear();
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (values.Count > 0 || current.Length > 0)
        {
            values.Add(current.ToString());
            await WriteRowAsync(writer, values, columns, columnsToImport, tableName);
        }

        await writer.CompleteAsync();
    }

    private static async Task WriteRowAsync(NpgsqlBinaryImporter writer, List<string> values,
        string[] allColumnsInFile, string[] columnsToImport, string tableName)
    {
        if (values.Count != allColumnsInFile.Length)
        {
            if (values.Count > 1 || !string.IsNullOrWhiteSpace(values[0]))
            {
                Console.WriteLine(
                    $"Warning: Line in {tableName} has {values.Count} columns, expected {allColumnsInFile.Length}. Skipping.");
            }

            return;
        }

        await writer.StartRowAsync();

        for (int i = 0; i < values.Count; i++)
        {
            var columnName = allColumnsInFile[i];
            if (columnName == "SOURCE_CODE" || columnName == "BATCH_RUN_DATE")
            {
                continue;
            }

            var value = values[i];

            if (string.IsNullOrEmpty(value) || value == "null")
            {
                await writer.WriteNullAsync();
                continue;
            }

            if (_schemaCache!.TryGetValue(tableName, out var tableSchema) &&
                tableSchema.TryGetValue(columnName, out var npgsqlType))
            {
                try
                {
                    await WriteTypedValueAsync(writer, value, npgsqlType);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Error parsing value '{value}' for column '{columnName}' in table '{tableName}' as type '{npgsqlType}': {ex.Message}. Writing as null.");
                    await writer.WriteNullAsync();
                }
            }
            else
            {
                await writer.WriteAsync(value, NpgsqlTypes.NpgsqlDbType.Text);
            }
        }
    }
}