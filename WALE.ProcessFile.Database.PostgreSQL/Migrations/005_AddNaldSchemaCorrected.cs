using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(5)]
public class AddNaldSchemaCorrected : Migration
{
    public override void Up()
    {
        Execute.Sql("DROP SCHEMA IF EXISTS nald CASCADE");
        Create.Schema("nald");

        CreateNaldTables();
        CreateForeignKeys();
    }

    public override void Down()
    {
        Execute.Sql("DROP SCHEMA IF EXISTS nald CASCADE");
    }

    private void CreateNaldTables()
    {
        Create.Table("NALD_ABSTAT_CATGRIES").InSchema("nald")
            .WithColumn("STAT_REF").AsDecimal(5, 2).NotNullable()
            .WithColumn("STAT_CATEGORY").AsString(200).NotNullable()
            .WithColumn("ALL_PRIMARY").AsString(1).NotNullable()
            .WithColumn("ALL_SECONDARY").AsString(1).NotNullable()
            .WithColumn("ALL_USES").AsString(1).NotNullable()
            .WithColumn("INCLUDE_IN_REPORT").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt16().Nullable()
            ;
        Create.PrimaryKey("AARC_PK").OnTable("NALD_ABSTAT_CATGRIES").WithSchema("nald").Columns("STAT_REF");

        Create.Table("NALD_ABSTAT_CAT_PRIMS").InSchema("nald")
            .WithColumn("AARC_STAT_REF").AsDecimal(5, 2).NotNullable()
            .WithColumn("APPR_CODE").AsString(1).NotNullable()
            ;
        Create.PrimaryKey("APSC_PK").OnTable("NALD_ABSTAT_CAT_PRIMS").WithSchema("nald").Columns("AARC_STAT_REF", "APPR_CODE");

        Create.Table("NALD_ABSTAT_CAT_SECS").InSchema("nald")
            .WithColumn("AARC_STAT_REF").AsDecimal(5, 2).NotNullable()
            .WithColumn("APSE_CODE").AsString(3).NotNullable()
            ;
        Create.PrimaryKey("ASSC_PK").OnTable("NALD_ABSTAT_CAT_SECS").WithSchema("nald").Columns("AARC_STAT_REF", "APSE_CODE");

        Create.Table("NALD_ABSTAT_CAT_USES").InSchema("nald")
            .WithColumn("AARC_STAT_REF").AsDecimal(5, 2).NotNullable()
            .WithColumn("APUS_CODE_FROM").AsInt16().NotNullable()
            .WithColumn("APUS_CODE_TO").AsInt16().Nullable()
            ;
        Create.PrimaryKey("ACUR_PK").OnTable("NALD_ABSTAT_CAT_USES").WithSchema("nald").Columns("AARC_STAT_REF", "APUS_CODE_FROM");

        Create.Table("NALD_ABSTAT_EXCEPTIONS").InSchema("nald")
            .WithColumn("AAYR_ARYR_CODE").AsString(5).NotNullable()
            .WithColumn("AAYR_YEAR").AsInt16().NotNullable()
            .WithColumn("AABL_ID").AsInt32().NotNullable()
            .WithColumn("NMES_MESSAGE_NUMBER").AsString(5).NotNullable()
            .WithColumn("LIC_NO").AsString(20).NotNullable()
            .WithColumn("AABV_ID").AsInt32().Nullable()
            .WithColumn("AABV_ISSUE_NO").AsInt16().Nullable()
            .WithColumn("AABV_INCR_NO").AsInt16().Nullable()
            .WithColumn("ARTY_ID").AsInt32().Nullable()
            .WithColumn("APUR_APPR_CODE").AsString(1).Nullable()
            .WithColumn("APUR_APSE_CODE").AsString(3).Nullable()
            .WithColumn("APUR_APUS_CODE").AsInt16().Nullable()
            .WithColumn("ANN_AUTH_QTY").AsDecimal(17, 3).Nullable()
            .WithColumn("ANN_ACT_QTY").AsDecimal(17, 3).Nullable()
            .WithColumn("DATESTAMP").AsDateTime().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AABE_PK").OnTable("NALD_ABSTAT_EXCEPTIONS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AAYR_YEAR", "AAYR_ARYR_CODE", "AABL_ID", "NMES_MESSAGE_NUMBER");

        Create.Table("NALD_ABSTAT_REPORT_DATA").InSchema("nald")
            .WithColumn("AABL_AREP_LEAP_CODE").AsString(5).NotNullable()
            .WithColumn("AARC_STAT_REF").AsDecimal(5, 2).NotNullable()
            .WithColumn("TW_TOT_AUTH_QTY").AsInt64().NotNullable()
            .WithColumn("SW_TOT_AUTH_QTY").AsInt64().NotNullable()
            .WithColumn("GW_TOT_AUTH_QTY").AsInt64().NotNullable()
            .WithColumn("TW_TOT_ACT_QTY").AsInt64().NotNullable()
            .WithColumn("SW_TOT_ACT_QTY").AsInt64().NotNullable()
            .WithColumn("GW_TOT_ACT_QTY").AsInt64().NotNullable()
            .WithColumn("TOT_LICENSED_RETURNED").AsInt64().NotNullable()
            .WithColumn("TOT_NO_LICENCES").AsInt64().NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ARDA_PK").OnTable("NALD_ABSTAT_REPORT_DATA").WithSchema("nald").Columns("FGAC_REGION_CODE", "AABL_AREP_LEAP_CODE", "AARC_STAT_REF");

        Create.Table("NALD_ABSTAT_TOTALS").InSchema("nald")
            .WithColumn("AAYR_ARYR_CODE").AsString(5).NotNullable()
            .WithColumn("AAYR_YEAR").AsInt16().NotNullable()
            .WithColumn("AABL_ID").AsInt32().NotNullable()
            .WithColumn("APUR_APPR_CODE").AsString(1).NotNullable()
            .WithColumn("APUR_APSE_CODE").AsString(3).NotNullable()
            .WithColumn("APUR_APUS_CODE").AsInt16().NotNullable()
            .WithColumn("ACT_OVERRIDDEN").AsString(1).NotNullable()
            .WithColumn("AUTH_CALC_FROM_DAILY").AsString(1).NotNullable()
            .WithColumn("AUTH_OVERRIDDEN").AsString(1).NotNullable()
            .WithColumn("PREV_YEAR_AUTH_USED").AsString(1).NotNullable()
            .WithColumn("SOURCE_TYPE").AsString(2).NotNullable()
            .WithColumn("ANN_ACT_QTY").AsDecimal(17, 3).Nullable()
            .WithColumn("ANN_ACT_USABILITY").AsString(1).Nullable()
            .WithColumn("ANN_AUTH_QTY").AsDecimal(17, 3).Nullable()
            .WithColumn("ANN_AUTH_USABILITY").AsString(1).Nullable()
            .WithColumn("USER_NOTES").AsString(70).Nullable()
            .WithColumn("DELETED").AsString(1).NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ARAB_PK").OnTable("NALD_ABSTAT_TOTALS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AAYR_ARYR_CODE", "AAYR_YEAR", "AABL_ID", "APUR_APPR_CODE", "APUR_APSE_CODE", "APUR_APUS_CODE");

        Create.Table("NALD_ABSTAT_YEARS").InSchema("nald")
            .WithColumn("ARYR_CODE").AsString(5).NotNullable()
            .WithColumn("YEAR").AsInt16().NotNullable()
            .WithColumn("SNAPSHOT_DATE").AsDateTime().NotNullable()
            ;
        Create.PrimaryKey("AAYR_PK").OnTable("NALD_ABSTAT_YEARS").WithSchema("nald").Columns("ARYR_CODE", "YEAR");

        Create.Table("NALD_ABS_LICENCES").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("LIC_NO").AsString(20).NotNullable()
            .WithColumn("AREP_SUC_CODE").AsString(5).NotNullable()
            .WithColumn("AREP_AREA_CODE").AsString(5).NotNullable()
            .WithColumn("SUSP_FROM_BILLING").AsString(1).NotNullable()
            .WithColumn("AREP_LEAP_CODE").AsString(5).Nullable()
            .WithColumn("EXPIRY_DATE").AsDateTime().Nullable()
            .WithColumn("ORIG_EFF_DATE").AsDateTime().Nullable()
            .WithColumn("ORIG_SIG_DATE").AsDateTime().Nullable()
            .WithColumn("ORIG_APP_NO").AsString(20).Nullable()
            .WithColumn("ORIG_LIC_NO").AsString(20).Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("REV_DATE").AsDateTime().Nullable()
            .WithColumn("LAPSED_DATE").AsDateTime().Nullable()
            .WithColumn("SUSP_FROM_RETURNS").AsString(1).NotNullable()
            .WithColumn("AREP_CAMS_CODE").AsString(5).Nullable()
            .WithColumn("X_REG_IND").AsString(1).NotNullable()
            .WithColumn("PREV_LIC_NO").AsString(20).Nullable()
            .WithColumn("FOLL_LIC_NO").AsString(20).Nullable()
            .WithColumn("AREP_EIUC_CODE").AsString(5).NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AABL_PK").OnTable("NALD_ABS_LICENCES").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_ABS_LIC_PURPOSES").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("AABV_AABL_ID").AsInt32().NotNullable()
            .WithColumn("AABV_ISSUE_NO").AsInt16().NotNullable()
            .WithColumn("AABV_INCR_NO").AsInt16().NotNullable()
            .WithColumn("APUR_APPR_CODE").AsString(1).NotNullable()
            .WithColumn("APUR_APSE_CODE").AsString(3).NotNullable()
            .WithColumn("APUR_APUS_CODE").AsInt16().NotNullable()
            .WithColumn("PERIOD_ST_DAY").AsInt16().NotNullable()
            .WithColumn("PERIOD_ST_MONTH").AsInt16().NotNullable()
            .WithColumn("PERIOD_END_DAY").AsInt16().NotNullable()
            .WithColumn("PERIOD_END_MONTH").AsInt16().NotNullable()
            .WithColumn("AMOM_CODE").AsString(5).NotNullable()
            .WithColumn("ANNUAL_QTY").AsDecimal(17, 3).Nullable()
            .WithColumn("ANNUAL_QTY_USABILITY").AsString(1).Nullable()
            .WithColumn("DAILY_QTY").AsDecimal(17, 3).Nullable()
            .WithColumn("DAILY_QTY_USABILITY").AsString(1).Nullable()
            .WithColumn("HOURLY_QTY").AsDecimal(17, 3).Nullable()
            .WithColumn("HOURLY_QTY_USABILITY").AsString(1).Nullable()
            .WithColumn("INST_QTY").AsDecimal(20, 6).Nullable()
            .WithColumn("INST_QTY_USABILITY").AsString(1).Nullable()
            .WithColumn("TIMELTD_ST_DATE").AsDateTime().Nullable()
            .WithColumn("TIMELTD_END_DATE").AsDateTime().Nullable()
            .WithColumn("LANDS").AsString(2000).Nullable()
            .WithColumn("AREC_CODE").AsString(5).Nullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AABP_PK").OnTable("NALD_ABS_LIC_PURPOSES").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_ABS_LIC_QUANTITIES").InSchema("nald")
            .WithColumn("AABV_ISSUE_NO").AsInt16().NotNullable()
            .WithColumn("AABV_INCR_NO").AsInt16().NotNullable()
            .WithColumn("MAX_ANNUAL_QTY").AsDecimal(17, 3).Nullable()
            .WithColumn("MAX_DAILY_QTY").AsDecimal(17, 3).Nullable()
            .WithColumn("AGGREGATED_IND").AsString(1).NotNullable()
            .WithColumn("PURP_POINTS_IND").AsString(1).NotNullable()
            .WithColumn("USER_VALID_IND").AsString(1).NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("AABV_AABL_ID").AsInt32().NotNullable()
            ;
        Create.PrimaryKey("AALQ_PK").OnTable("NALD_ABS_LIC_QUANTITIES").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_ABS_LIC_VERSIONS").InSchema("nald")
            .WithColumn("AABL_ID").AsInt32().NotNullable()
            .WithColumn("ISSUE_NO").AsInt16().NotNullable()
            .WithColumn("INCR_NO").AsInt16().NotNullable()
            .WithColumn("AABV_TYPE").AsString(5).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("STATUS").AsString(5).NotNullable()
            .WithColumn("RETURNS_REQ").AsString(1).NotNullable()
            .WithColumn("CHARGEABLE").AsString(1).NotNullable()
            .WithColumn("ASRC_CODE").AsString(15).NotNullable()
            .WithColumn("ACON_APAR_ID").AsInt32().NotNullable()
            .WithColumn("ACON_AADD_ID").AsInt32().NotNullable()
            .WithColumn("ALTY_CODE").AsString(5).Nullable()
            .WithColumn("ACCL_CODE").AsString(5).Nullable()
            .WithColumn("MULTIPLE_LH").AsString(1).Nullable()
            .WithColumn("LIC_SIG_DATE").AsDateTime().Nullable()
            .WithColumn("APP_NO").AsString(20).Nullable()
            .WithColumn("LIC_DOC_FLAG").AsString(1).NotNullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            .WithColumn("EXPIRY_DATE1").AsDateTime().Nullable()
            .WithColumn("WA_ALTY_CODE").AsString(5).Nullable()
            .WithColumn("VOL_CONV").AsString(1).NotNullable()
            .WithColumn("WRT_CODE").AsString(1).NotNullable()
            .WithColumn("DEREG_CODE").AsString(5).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AABV_PK").OnTable("NALD_ABS_LIC_VERSIONS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AABL_ID", "ISSUE_NO", "INCR_NO");

        Create.Table("NALD_ABS_PURP_POINTS").InSchema("nald")
            .WithColumn("AABP_ID").AsInt32().NotNullable()
            .WithColumn("AAIP_ID").AsInt32().NotNullable()
            .WithColumn("AMOA_CODE").AsString(5).NotNullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AAPO_PK").OnTable("NALD_ABS_PURP_POINTS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AABP_ID", "AAIP_ID");

        Create.Table("NALD_ADDRESSES").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("ADDR_LINE1").AsString(80).NotNullable()
            .WithColumn("LAST_CHANGED").AsDateTime().NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("ADDR_LINE2").AsString(80).Nullable()
            .WithColumn("ADDR_LINE3").AsString(80).Nullable()
            .WithColumn("ADDR_LINE4").AsString(80).Nullable()
            .WithColumn("TOWN").AsString(30).Nullable()
            .WithColumn("COUNTY").AsString(30).Nullable()
            .WithColumn("POSTCODE").AsString(10).Nullable()
            .WithColumn("COUNTRY").AsString(30).Nullable()
            .WithColumn("APCO_CODE").AsString(5).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AADD_PK").OnTable("NALD_ADDRESSES").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_APP_FORM_HELP").InSchema("nald")
            .WithColumn("HLP_APPLN").AsString(30).NotNullable()
            .WithColumn("HLP_MODTAB_NAME").AsString(30).NotNullable()
            .WithColumn("HLP_SEQ").AsInt32().NotNullable()
            .WithColumn("HLP_INDEX").AsString(100).NotNullable()
            .WithColumn("HLP_TYPE").AsString(1).NotNullable()
            .WithColumn("HLP_GENERATED").AsString(1).Nullable()
            .WithColumn("HLP_TEXT").AsString(70).Nullable()
            ;
        Create.PrimaryKey("NFH_PK").OnTable("NALD_APP_FORM_HELP").WithSchema("nald").Columns("HLP_APPLN", "HLP_MODTAB_NAME", "HLP_SEQ");

        Create.Table("NALD_BANK_CODES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(30).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ABAN_PK").OnTable("NALD_BANK_CODES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_BILL_CHGVERSIONS").InSchema("nald")
            .WithColumn("ABRN_FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("ABRN_BILL_RUN_NO").AsInt16().NotNullable()
            .WithColumn("ACVR_AABL_ID").AsInt32().NotNullable()
            .WithColumn("ACVR_VERS_NO").AsInt16().NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("LH_ACC_NO").AsString(15).NotNullable()
            .WithColumn("IAS_CUST_REF").AsString(10).NotNullable()
            .WithColumn("CUT_OFF_DATE").AsDateTime().NotNullable()
            .WithColumn("CUT_OFF_IND").AsString(1).NotNullable()
            .WithColumn("CREDIT_DEBIT_FACTOR").AsDecimal().NotNullable()
            .WithColumn("RETURNS_ACTUAL").AsString(1).NotNullable()
            .WithColumn("BILLED_UPTO_DATE").AsDateTime().Nullable()
            .WithColumn("NEW_OWNER_VERS").AsInt16().Nullable()
            .WithColumn("NEW_OWNER_YEAR").AsInt16().Nullable()
            .WithColumn("NEW_LIC_YEAR").AsInt16().Nullable()
            .WithColumn("BILLABLE_NOW").AsString(1).Nullable()
            .WithColumn("BILLABLE_NEXT").AsString(1).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            .WithColumn("RL_SET").AsString(1).Nullable()
            ;
        Create.PrimaryKey("ABCV_PK").OnTable("NALD_BILL_CHGVERSIONS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ABRN_FIN_YEAR", "ABRN_BILL_RUN_NO", "ACVR_AABL_ID", "ACVR_VERS_NO");

        Create.Table("NALD_BILL_ERRORS").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("ABRN_FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("ABRN_BILL_RUN_NO").AsInt16().NotNullable()
            .WithColumn("MODULE_NAME").AsString(6).NotNullable()
            .WithColumn("ERROR_DATE").AsDateTime().NotNullable()
            .WithColumn("ERROR_TYPE").AsString(1).NotNullable()
            .WithColumn("ERROR_MESSAGE").AsString(2000).NotNullable()
            .WithColumn("NMES_MESSAGE_NUMBER").AsString(5).Nullable()
            .WithColumn("RECORD_DETAILS").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ABER_PK").OnTable("NALD_BILL_ERRORS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_BILL_HEADERS").InSchema("nald")
            .WithColumn("ABRN_FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("ABRN_BILL_RUN_NO").AsInt16().NotNullable()
            .WithColumn("FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("PREVIEW_NO").AsInt32().NotNullable()
            .WithColumn("BILL_PRINT_IND").AsString(1).NotNullable()
            .WithColumn("BILL_DATE").AsDateTime().NotNullable()
            .WithColumn("BILLABLE_IND").AsString(1).NotNullable()
            .WithColumn("MIN_INV_OVERRIDE").AsString(1).NotNullable()
            .WithColumn("LH_ACC_NO").AsString(15).NotNullable()
            .WithColumn("IAS_CUST_REF").AsString(10).NotNullable()
            .WithColumn("WRITTEN_LANG").AsString(1).NotNullable()
            .WithColumn("LH_SURNAME").AsString(60).NotNullable()
            .WithColumn("IAS_SURNAME").AsString(60).NotNullable()
            .WithColumn("IAS_ADDR1").AsString(80).NotNullable()
            .WithColumn("NEW_INV_FLAG").AsInt64().Nullable()
            .WithColumn("NEW_OWN_FLAG").AsInt64().Nullable()
            .WithColumn("BILL_NO").AsString(10).Nullable()
            .WithColumn("TPT_FLAG").AsString(1).Nullable()
            .WithColumn("MIN_CHARGE").AsString(1).Nullable()
            .WithColumn("NET_AMOUNT").AsDecimal(11, 2).Nullable()
            .WithColumn("VAT_AMOUNT").AsDecimal(11, 2).Nullable()
            .WithColumn("BILLED_AMOUNT").AsDecimal(11, 2).Nullable()
            .WithColumn("ABHD_ID").AsInt32().Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("NOTES_WELSH").AsString(2000).Nullable()
            .WithColumn("ENQ_NAME").AsString(30).Nullable()
            .WithColumn("ENQ_TEL_NO").AsString(15).Nullable()
            .WithColumn("IAS_TITLE").AsString(20).Nullable()
            .WithColumn("LH_TITLE").AsString(20).Nullable()
            .WithColumn("IAS_INITIALS").AsString(5).Nullable()
            .WithColumn("LH_INITIALS").AsString(5).Nullable()
            .WithColumn("LH_FORENAME").AsString(60).Nullable()
            .WithColumn("IAS_FORENAME").AsString(60).Nullable()
            .WithColumn("IAS_ADDR2").AsString(80).Nullable()
            .WithColumn("IAS_ADDR3").AsString(80).Nullable()
            .WithColumn("IAS_ADDR4").AsString(80).Nullable()
            .WithColumn("IAS_TOWN").AsString(30).Nullable()
            .WithColumn("IAS_POSTCODE").AsString(10).Nullable()
            .WithColumn("IAS_COUNTY").AsString(30).Nullable()
            .WithColumn("IAS_COUNTRY").AsString(30).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("REGION_CODE").AsString(1).NotNullable()
            .WithColumn("INCOME_TYPE").AsString(1).NotNullable()
            .WithColumn("BILL_TYPE").AsString(1).NotNullable()
            ;
        Create.PrimaryKey("ABHD_PK").OnTable("NALD_BILL_HEADERS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_BILL_PROCESSES").InSchema("nald")
            .WithColumn("ABRN_FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("ABRN_BILL_RUN_NO").AsInt16().NotNullable()
            .WithColumn("MODULE_NAME").AsString(6).NotNullable()
            .WithColumn("START_DATE").AsDateTime().NotNullable()
            .WithColumn("STATUS").AsString(1).NotNullable()
            .WithColumn("END_DATE").AsDateTime().NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ABPR_PK").OnTable("NALD_BILL_PROCESSES").WithSchema("nald").Columns("FGAC_REGION_CODE", "ABRN_FIN_YEAR", "ABRN_BILL_RUN_NO", "MODULE_NAME", "START_DATE");

        Create.Table("NALD_BILL_RUNS").InSchema("nald")
            .WithColumn("FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("BILL_RUN_NO").AsInt16().NotNullable()
            .WithColumn("BILL_RUN_TYPE").AsString(1).NotNullable()
            .WithColumn("BILL_DATE").AsDateTime().NotNullable()
            .WithColumn("INITIATOR").AsString(30).NotNullable()
            .WithColumn("INITIATION_DATE").AsDateTime().NotNullable()
            .WithColumn("ENQ_NAME").AsString(30).NotNullable()
            .WithColumn("ENQ_NAME_WELSH").AsString(30).NotNullable()
            .WithColumn("ENQ_NO").AsString(15).NotNullable()
            .WithColumn("ABORTED_RUN").AsString(1).NotNullable()
            .WithColumn("BILL_RUN_STATUS_DATE").AsDateTime().NotNullable()
            .WithColumn("BILL_RUN_STATUS").AsString(1).NotNullable()
            .WithColumn("INSTALL_BILL_DATE").AsDateTime().Nullable()
            .WithColumn("INV_ST_NO").AsString(10).Nullable()
            .WithColumn("CRN_ST_NO").AsString(10).Nullable()
            .WithColumn("NO_OF_INVS").AsInt16().Nullable()
            .WithColumn("NO_OF_CRNS").AsInt16().Nullable()
            .WithColumn("VALUE_OF_INVS").AsDecimal(11, 2).Nullable()
            .WithColumn("VALUE_OF_CRNS").AsDecimal(11, 2).Nullable()
            .WithColumn("ABORTEE").AsString(30).Nullable()
            .WithColumn("ABORT_REASON").AsString(70).Nullable()
            .WithColumn("CONFIRMEE").AsString(30).Nullable()
            .WithColumn("IAS_XFER_DATE").AsDateTime().Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ABRN_PK").OnTable("NALD_BILL_RUNS").WithSchema("nald").Columns("FGAC_REGION_CODE", "FIN_YEAR", "BILL_RUN_NO");

        Create.Table("NALD_BILL_TPT_RETURNS").InSchema("nald")
            .WithColumn("ABRN_FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("ABRN_BILL_RUN_NO").AsInt16().NotNullable()
            .WithColumn("ACEL_ID").AsInt32().NotNullable()
            .WithColumn("FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("LATEST_RET_DATE").AsDateTime().NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ABTP_PK").OnTable("NALD_BILL_TPT_RETURNS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ABRN_FIN_YEAR", "ABRN_BILL_RUN_NO", "ACEL_ID", "FIN_YEAR");

        Create.Table("NALD_BILL_TRANS").InSchema("nald")
            .WithColumn("ABHD_ID").AsInt32().Nullable()
            .WithColumn("NEW_INV_FLAG").AsInt64().Nullable()
            .WithColumn("NEW_OWN_FLAG").AsInt64().Nullable()
            .WithColumn("TPT_FLAG").AsString(1).Nullable()
            .WithColumn("ELEMENT_AGRMNTS").AsString(32).Nullable()
            .WithColumn("LH_ACC_AGRMNTS").AsString(32).Nullable()
            .WithColumn("ELEMENT_AGRMNT_VALS").AsString(32).Nullable()
            .WithColumn("LH_ACC_AGRMNTS_VALS").AsString(32).Nullable()
            .WithColumn("TRANS_DESCR").AsString(70).Nullable()
            .WithColumn("FINAL_A1_BILLABLE_AMOUNT").AsDecimal(11, 2).Nullable()
            .WithColumn("FINAL_A2_BILLABLE_AMOUNT").AsDecimal(11, 2).Nullable()
            .WithColumn("EIUC_SRCE_VALUE").AsDecimal(6, 3).Nullable()
            .WithColumn("EIUC_VALUE").AsDecimal(6, 2).Nullable()
            .WithColumn("TLP_VALUE").AsDecimal(6, 3).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            .WithColumn("EIUC_ELEMENT_AGRMNT_VALS").AsString(32).Nullable()
            .WithColumn("EIUC_2PT_VALUE").AsDecimal(5, 2).Nullable()
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("TRANS_TYPE").AsString(1).NotNullable()
            .WithColumn("ABRN_FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("ABRN_BILL_RUN_NO").AsInt16().NotNullable()
            .WithColumn("FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("NET_AMOUNT").AsDecimal(11, 2).NotNullable()
            .WithColumn("VAT_AMOUNT").AsDecimal(11, 2).NotNullable()
            .WithColumn("BILLABLE_ANN_QTY").AsDecimal(20, 6).NotNullable()
            .WithColumn("LIC_ID").AsInt32().NotNullable()
            .WithColumn("VERS_NO").AsInt16().NotNullable()
            .WithColumn("LH_ACC_NO").AsString(15).NotNullable()
            .WithColumn("IAS_CUST_REF").AsString(10).NotNullable()
            .WithColumn("ACEL_ID").AsInt32().NotNullable()
            .WithColumn("SRCE_CODE").AsString(1).NotNullable()
            .WithColumn("SEAS_CODE").AsString(1).NotNullable()
            .WithColumn("LOSS_CODE").AsString(1).NotNullable()
            .WithColumn("SRCE_VALUE").AsDecimal(6, 3).NotNullable()
            .WithColumn("SEAS_VALUE").AsDecimal(6, 3).NotNullable()
            .WithColumn("LOSS_VALUE").AsDecimal(6, 3).NotNullable()
            .WithColumn("VAT_CODE").AsString(3).NotNullable()
            .WithColumn("SUC_CODE").AsString(5).NotNullable()
            .WithColumn("VAT_RATE").AsDecimal(6, 3).NotNullable()
            .WithColumn("SUC_RATE").AsDecimal(6, 2).NotNullable()
            .WithColumn("BILL_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("BILL_END_DATE").AsDateTime().NotNullable()
            .WithColumn("RETURNS_ACTUAL").AsString(1).NotNullable()
            .WithColumn("ABS_PER_DAYS").AsInt16().NotNullable()
            .WithColumn("BILLABLE_DAYS").AsInt16().NotNullable()
            .WithColumn("AWAITING_BILL_HEADER").AsString(1).Nullable()
            ;
        Create.PrimaryKey("ABTN_PK").OnTable("NALD_BILL_TRANS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_BILL_YEARS").InSchema("nald")
            .WithColumn("ABCV_ABRN_FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("ABCV_ABRN_BILL_RUN_NO").AsInt16().NotNullable()
            .WithColumn("ABCV_ACVR_AABL_ID").AsInt32().NotNullable()
            .WithColumn("ABCV_ACVR_VERS_NO").AsInt16().NotNullable()
            .WithColumn("FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ABYR_PK").OnTable("NALD_BILL_YEARS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ABCV_ABRN_FIN_YEAR", "ABCV_ABRN_BILL_RUN_NO", "ABCV_ACVR_AABL_ID", "ABCV_ACVR_VERS_NO", "FIN_YEAR");

        Create.Table("NALD_BUTTONS").InSchema("nald")
            .WithColumn("BUTTON_NUMBER").AsInt16().NotNullable()
            .WithColumn("BUTTON_TYPE").AsString(1).NotNullable()
            .WithColumn("BUTTON_LABEL").AsString(15).NotNullable()
            .WithColumn("BUTTON_ICON").AsString(8).NotNullable()
            ;
        Create.PrimaryKey("NBUT_PK").OnTable("NALD_BUTTONS").WithSchema("nald").Columns("BUTTON_NUMBER");

        Create.Table("NALD_CHG_AGRMNTS").InSchema("nald")
            .WithColumn("ACEL_ID").AsInt32().NotNullable()
            .WithColumn("AFSA_CODE").AsString(5).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            .WithColumn("SIGNED_DATE").AsDateTime().Nullable()
            .WithColumn("FILE_REF").AsString(20).Nullable()
            .WithColumn("TEXT").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ACSA_PK").OnTable("NALD_CHG_AGRMNTS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ACEL_ID", "AFSA_CODE", "EFF_ST_DATE");

        Create.Table("NALD_CHG_ELEMENTS").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("ACVR_AABL_ID").AsInt32().NotNullable()
            .WithColumn("ACVR_VERS_NO").AsInt16().NotNullable()
            .WithColumn("ABS_PERIOD_ST_DAY").AsInt16().NotNullable()
            .WithColumn("ABS_PERIOD_ST_MONTH").AsInt16().NotNullable()
            .WithColumn("ABS_PERIOD_END_DAY").AsInt16().NotNullable()
            .WithColumn("ABS_PERIOD_END_MONTH").AsInt16().NotNullable()
            .WithColumn("AUTH_ANN_QTY").AsDecimal(20, 6).NotNullable()
            .WithColumn("ASFT_CODE").AsString(1).NotNullable()
            .WithColumn("ASFT_CODE_DERIVED").AsString(1).NotNullable()
            .WithColumn("ASRF_CODE").AsString(1).NotNullable()
            .WithColumn("ALSF_CODE").AsString(1).NotNullable()
            .WithColumn("APUR_APPR_CODE").AsString(1).NotNullable()
            .WithColumn("APUR_APSE_CODE").AsString(3).NotNullable()
            .WithColumn("APUR_APUS_CODE").AsInt16().NotNullable()
            .WithColumn("FCTS_OVERRIDDEN").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            .WithColumn("BILLABLE_ANN_QTY").AsDecimal(20, 6).Nullable()
            .WithColumn("TIMELTD_ST_DATE").AsDateTime().Nullable()
            .WithColumn("TIMELTD_END_DATE").AsDateTime().Nullable()
            .WithColumn("DESCR").AsString(70).Nullable()
            .WithColumn("DESCR_WELSH").AsString(70).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ACEL_PK").OnTable("NALD_CHG_ELEMENTS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_CHG_VERSIONS").InSchema("nald")
            .WithColumn("AABL_ID").AsInt32().NotNullable()
            .WithColumn("VERS_NO").AsInt16().NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("STATUS").AsString(5).NotNullable()
            .WithColumn("APPORTIONMENT").AsString(1).NotNullable()
            .WithColumn("IN_ERROR_STATUS").AsString(1).NotNullable()
            .WithColumn("AIIA_ALHA_ACC_NO").AsString(15).NotNullable()
            .WithColumn("AIIA_IAS_CUST_REF").AsString(10).NotNullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            .WithColumn("NEW_OWNER_VERS").AsInt16().Nullable()
            .WithColumn("NEW_OWNER_YEAR").AsInt16().Nullable()
            .WithColumn("NEW_LIC_YEAR").AsInt16().Nullable()
            .WithColumn("BILLED_UPTO_DATE").AsDateTime().Nullable()
            .WithColumn("TO_BE_BILLED").AsString(1).Nullable()
            .WithColumn("TLPA_STATUS").AsString(2).NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            .WithColumn("RL_FINAL").AsString(1).Nullable()
            ;
        Create.PrimaryKey("ACVR_PK").OnTable("NALD_CHG_VERSIONS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AABL_ID", "VERS_NO");

        Create.Table("NALD_CODE_CONTROLS").InSchema("nald")
            .WithColumn("CC_DOMAIN").AsString(30).NotNullable()
            .WithColumn("CC_COMMENT").AsString(240).Nullable()
            .WithColumn("CC_NEXT_VALUE").AsInt64().NotNullable()
            ;

        Create.Table("NALD_CONTACTS").InSchema("nald")
            .WithColumn("APAR_ID").AsInt32().NotNullable()
            .WithColumn("AADD_ID").AsInt32().NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ACON_PK").OnTable("NALD_CONTACTS").WithSchema("nald").Columns("FGAC_REGION_CODE", "APAR_ID", "AADD_ID");

        Create.Table("NALD_CONT_NOS").InSchema("nald")
            .WithColumn("ACON_APAR_ID").AsInt32().NotNullable()
            .WithColumn("ACON_AADD_ID").AsInt32().NotNullable()
            .WithColumn("ACNT_CODE").AsString(5).NotNullable()
            .WithColumn("CONT_NO").AsString(80).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ACNO_PK").OnTable("NALD_CONT_NOS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ACON_APAR_ID", "ACON_AADD_ID", "ACNT_CODE");

        Create.Table("NALD_CONT_NO_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(30).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ACNT_PK").OnTable("NALD_CONT_NO_TYPES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_CRIT_CLASSES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(30).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ACCL_PK").OnTable("NALD_CRIT_CLASSES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_CTRL_FLOWS").InSchema("nald")
            .WithColumn("AMAN_CODE").AsString(5).NotNullable()
            .WithColumn("SEQ_NO").AsInt16().NotNullable()
            .WithColumn("VALUE").AsDecimal(20, 6).NotNullable()
            .WithColumn("ST_DAY").AsInt16().Nullable()
            .WithColumn("ST_MONTH").AsInt16().Nullable()
            .WithColumn("END_DAY").AsInt16().Nullable()
            .WithColumn("END_MONTH").AsInt16().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().Nullable()
            ;
        Create.PrimaryKey("ACFL_PK").OnTable("NALD_CTRL_FLOWS").WithSchema("nald").Columns("AMAN_CODE", "SEQ_NO");

        Create.Table("NALD_CTRL_LEVELS").InSchema("nald")
            .WithColumn("AMAN_CODE").AsString(5).NotNullable()
            .WithColumn("SEQ_NO").AsInt16().NotNullable()
            .WithColumn("VALUE").AsDecimal(20, 6).NotNullable()
            .WithColumn("DATUM_TYPE").AsString(5).NotNullable()
            .WithColumn("ST_DAY").AsInt16().Nullable()
            .WithColumn("ST_MONTH").AsInt16().Nullable()
            .WithColumn("END_DAY").AsInt16().Nullable()
            .WithColumn("END_MONTH").AsInt16().Nullable()
            .WithColumn("LOCAL_REF").AsString(80).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().Nullable()
            ;
        Create.PrimaryKey("ACLE_PK").OnTable("NALD_CTRL_LEVELS").WithSchema("nald").Columns("AMAN_CODE", "SEQ_NO");

        Create.Table("NALD_CTRL_POINT_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("APTY_PK").OnTable("NALD_CTRL_POINT_TYPES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_DEREG_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ALDE_PK").OnTable("NALD_DEREG_TYPES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_DOCUMENT_REFS").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("DOC_REF").AsString(30).NotNullable()
            .WithColumn("AABL_ID").AsInt32().Nullable()
            .WithColumn("AIMP_ID").AsInt32().Nullable()
            .WithColumn("DOC_FROM_DATE").AsDateTime().Nullable()
            .WithColumn("DOC_TO_DATE").AsDateTime().Nullable()
            .WithColumn("EXT_LOC_DESCR").AsString(2000).Nullable()
            .WithColumn("TEXT").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ADRF_PK").OnTable("NALD_DOCUMENT_REFS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_EIUC_VALS").InSchema("nald")
            .WithColumn("AREP_CODE").AsString(5).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("EIUC_VALUE").AsDecimal(6, 2).NotNullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AEIUV_PK").OnTable("NALD_EIUC_VALS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AREP_CODE", "EFF_ST_DATE");

        Create.Table("NALD_FIN_AGRMNT_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("LEVEL_APPLIED").AsString(1).NotNullable()
            .WithColumn("USED_BY_SYS").AsString(1).NotNullable()
            .WithColumn("AFFECTS_INVS").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("AFSA_PK").OnTable("NALD_FIN_AGRMNT_TYPES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_FIN_AGRMNT_VALS").InSchema("nald")
            .WithColumn("AFSA_CODE").AsString(5).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("ADJ_FCT").AsDecimal(12, 9).Nullable()
            .WithColumn("COMP_VALUE").AsDecimal(11, 2).Nullable()
            .WithColumn("COMP_DAY").AsInt16().Nullable()
            .WithColumn("COMP_MONTH").AsInt16().Nullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ASPV_PK").OnTable("NALD_FIN_AGRMNT_VALS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AFSA_CODE", "EFF_ST_DATE");

        Create.Table("NALD_FORM_HELP").InSchema("nald")
            .WithColumn("HLP_APPLN").AsString(30).NotNullable()
            .WithColumn("HLP_INDEX").AsString(100).NotNullable()
            .WithColumn("HLP_MODTAB_NAME").AsString(30).Nullable()
            .WithColumn("HLP_GENERATED").AsString(1).Nullable()
            .WithColumn("HLP_SEQ").AsInt32().NotNullable()
            .WithColumn("HLP_TEXT").AsString(70).Nullable()
            .WithColumn("HLP_TYPE").AsString(1).NotNullable()
            ;

        Create.Table("NALD_GROUP_LH_ACCS").InSchema("nald")
            .WithColumn("ACC_NO").AsString(15).NotNullable()
            .WithColumn("ACON_APAR_ID").AsInt32().NotNullable()
            .WithColumn("ACON_AADD_ID").AsInt32().NotNullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AGCA_PK").OnTable("NALD_GROUP_LH_ACCS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ACC_NO");

        Create.Table("NALD_IAS_INVOICE_ACCS").InSchema("nald")
            .WithColumn("ALHA_ACC_NO").AsString(15).NotNullable()
            .WithColumn("IAS_CUST_REF").AsString(10).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("ACON_APAR_ID").AsInt32().NotNullable()
            .WithColumn("ACON_AADD_ID").AsInt32().NotNullable()
            .WithColumn("IAS_XFER_DATE").AsDateTime().Nullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AIIA_PK").OnTable("NALD_IAS_INVOICE_ACCS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ALHA_ACC_NO", "IAS_CUST_REF");

        Create.Table("NALD_IMP_LICENCES").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("LIC_NO").AsString(20).NotNullable()
            .WithColumn("ORIG_SIG_DATE").AsDateTime().Nullable()
            .WithColumn("ORIG_EFF_DATE").AsDateTime().Nullable()
            .WithColumn("ORIG_APP_NO").AsString(20).Nullable()
            .WithColumn("TERM_DATE").AsDateTime().Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("AREA").AsString(5).Nullable()
            .WithColumn("LEAP").AsString(5).Nullable()
            .WithColumn("CAMS").AsString(5).Nullable()
            .WithColumn("RETRO_STR").AsString(1).NotNullable()
            .WithColumn("X_REG_IND").AsString(1).NotNullable()
            .WithColumn("REV_DATE").AsDateTime().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AIMP_PK").OnTable("NALD_IMP_LICENCES").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_IMP_LIC_PURPOSES").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("AIMV_AIMP_ID").AsInt32().NotNullable()
            .WithColumn("AIMV_ISSUE_NO").AsInt16().NotNullable()
            .WithColumn("AIMV_INCR_NO").AsInt16().NotNullable()
            .WithColumn("APUR_APPR_CODE").AsString(1).NotNullable()
            .WithColumn("APUR_APSE_CODE").AsString(3).NotNullable()
            .WithColumn("APUR_APUS_CODE").AsInt16().NotNullable()
            .WithColumn("PERIOD_ST_DAY").AsInt16().Nullable()
            .WithColumn("PERIOD_ST_MONTH").AsInt16().Nullable()
            .WithColumn("PERIOD_END_DAY").AsInt16().Nullable()
            .WithColumn("PERIOD_END_MONTH").AsInt16().Nullable()
            .WithColumn("AMOI_CODE").AsString(5).Nullable()
            .WithColumn("CONST_ST_BY_DATE").AsDateTime().Nullable()
            .WithColumn("CONST_END_BY_DATE").AsDateTime().Nullable()
            .WithColumn("WORKS_ST_DATE").AsDateTime().Nullable()
            .WithColumn("WORKS_COMPL_DATE").AsDateTime().Nullable()
            .WithColumn("MAX_VOL").AsDecimal(17, 3).Nullable()
            .WithColumn("MAX_VOL_USABILITY").AsString(1).Nullable()
            .WithColumn("SURFACE_AREA").AsDecimal(10, 2).Nullable()
            .WithColumn("AISI_CODE").AsString(5).Nullable()
            .WithColumn("SPLWAY_LEVEL").AsDecimal(10, 2).Nullable()
            .WithColumn("SPLWAY_DATUM").AsString(5).Nullable()
            .WithColumn("SPLWAY_REF").AsString(80).Nullable()
            .WithColumn("OVFLOW_LEVEL").AsDecimal(10, 2).Nullable()
            .WithColumn("OVFLOW_DATUM").AsString(5).Nullable()
            .WithColumn("OVFLOW_REF").AsString(80).Nullable()
            .WithColumn("RSVOIR_ACT").AsString(1).Nullable()
            .WithColumn("RSVOIR_ACT_TEXT").AsString(300).Nullable()
            .WithColumn("LANDS_IMP").AsString(2000).Nullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("CEASED_DATE").AsDateTime().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AIPU_PK").OnTable("NALD_IMP_LIC_PURPOSES").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_IMP_LIC_VERSIONS").InSchema("nald")
            .WithColumn("AIMP_ID").AsInt32().NotNullable()
            .WithColumn("ISSUE_NO").AsInt16().NotNullable()
            .WithColumn("INCR_NO").AsInt16().NotNullable()
            .WithColumn("AIMV_TYPE").AsString(5).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("STATUS").AsString(5).NotNullable()
            .WithColumn("ASRC_CODE").AsString(15).NotNullable()
            .WithColumn("ACCL_CODE").AsString(5).Nullable()
            .WithColumn("LIC_SIG_DATE").AsDateTime().Nullable()
            .WithColumn("LIC_DOC_FLAG").AsString(1).Nullable()
            .WithColumn("APP_NO").AsString(20).Nullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            .WithColumn("ACON_APAR_ID").AsInt32().NotNullable()
            .WithColumn("ACON_AADD_ID").AsInt32().NotNullable()
            .WithColumn("MULTIPLE_LH").AsString(1).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AIMV_PK").OnTable("NALD_IMP_LIC_VERSIONS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AIMP_ID", "ISSUE_NO", "INCR_NO");

        Create.Table("NALD_IMP_PURP_POINTS").InSchema("nald")
            .WithColumn("AIPU_ID").AsInt32().NotNullable()
            .WithColumn("AAIP_ID").AsInt32().NotNullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("IMOI_CODE").AsString(5).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AIPO_PK").OnTable("NALD_IMP_PURP_POINTS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AIPU_ID", "AAIP_ID");

        Create.Table("NALD_IMP_SITE_STATUSES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("AISI_PK").OnTable("NALD_IMP_SITE_STATUSES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_LH_ACCS").InSchema("nald")
            .WithColumn("ACC_NO").AsString(15).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("SUSP_FROM_BILLING").AsString(1).NotNullable()
            .WithColumn("ACON_APAR_ID").AsInt32().NotNullable()
            .WithColumn("ACON_AADD_ID").AsInt32().NotNullable()
            .WithColumn("AGCA_ACC_NO").AsString(15).Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ALHA_PK").OnTable("NALD_LH_ACCS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ACC_NO");

        Create.Table("NALD_LH_AGRMNTS").InSchema("nald")
            .WithColumn("ALHA_ACC_NO").AsString(15).NotNullable()
            .WithColumn("AFSA_CODE").AsString(5).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            .WithColumn("SIGNED_DATE").AsDateTime().Nullable()
            .WithColumn("FILE_REF").AsString(20).Nullable()
            .WithColumn("TEXT").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ALHS_PK").OnTable("NALD_LH_AGRMNTS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ALHA_ACC_NO", "AFSA_CODE", "EFF_ST_DATE");

        Create.Table("NALD_LH_REC_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("AREC_PK").OnTable("NALD_LH_REC_TYPES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_LH_SUSP_LOGS").InSchema("nald")
            .WithColumn("ALHA_ACC_NO").AsString(15).NotNullable()
            .WithColumn("CREATE_DATE").AsDateTime().NotNullable()
            .WithColumn("USER_ID").AsString(30).NotNullable()
            .WithColumn("EVENT").AsString(6).NotNullable()
            .WithColumn("AMRE_AMRE_TYPE").AsString(4).Nullable()
            .WithColumn("AMRE_CODE").AsString(5).Nullable()
            .WithColumn("TEXT").AsString(300).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ALSL_PK").OnTable("NALD_LH_SUSP_LOGS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ALHA_ACC_NO", "CREATE_DATE");

        Create.Table("NALD_LIC_AGRMNTS").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("ALSA_CODE").AsString(5).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("AABP_ID").AsInt32().Nullable()
            .WithColumn("AIPU_ID").AsInt32().Nullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            .WithColumn("TEXT").AsString(2000).Nullable()
            .WithColumn("SIGNED_DATE").AsDateTime().Nullable()
            .WithColumn("FILE_REF").AsString(20).Nullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ALAG_PK").OnTable("NALD_LIC_AGRMNTS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_LIC_AGRMNT_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("AFFECTS_ABS").AsString(1).NotNullable()
            .WithColumn("AFFECTS_IMP").AsString(1).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ALSA_PK").OnTable("NALD_LIC_AGRMNT_TYPES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_LIC_AVAILS").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("AMLA_PK").OnTable("NALD_LIC_AVAILS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_LIC_CONDITIONS").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("ACIN_CODE").AsString(5).NotNullable()
            .WithColumn("ACIN_SUBCODE").AsString(5).NotNullable()
            .WithColumn("AABP_ID").AsInt32().Nullable()
            .WithColumn("AIPU_ID").AsInt32().Nullable()
            .WithColumn("PARAM1").AsString(40).Nullable()
            .WithColumn("PARAM2").AsString(40).Nullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            .WithColumn("TEXT").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ALCO_PK").OnTable("NALD_LIC_CONDITIONS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_LIC_COND_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("SUBCODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("SUBCODE_DESC").AsString(70).NotNullable()
            .WithColumn("AFFECTS_ABS").AsString(1).NotNullable()
            .WithColumn("AFFECTS_IMP").AsString(1).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ACIN_PK").OnTable("NALD_LIC_COND_TYPES").WithSchema("nald").Columns("CODE", "SUBCODE");

        Create.Table("NALD_LIC_ROLES").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("ALRT_CODE").AsString(5).NotNullable()
            .WithColumn("ACON_APAR_ID").AsInt32().NotNullable()
            .WithColumn("ACON_AADD_ID").AsInt32().NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("AABL_ID").AsInt32().Nullable()
            .WithColumn("AIMP_ID").AsInt32().Nullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ALRO_PK").OnTable("NALD_LIC_ROLES").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_LIC_ROLE_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("AFFECTS_ABS").AsString(1).NotNullable()
            .WithColumn("AFFECTS_IMP").AsString(1).NotNullable()
            .WithColumn("CUST_AGENCY").AsString(4).NotNullable()
            .WithColumn("USED_BY_SYS").AsString(1).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ALRT_PK").OnTable("NALD_LIC_ROLE_TYPES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_LIC_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ALTY_PK").OnTable("NALD_LIC_TYPES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_LOSS_FACTORS").InSchema("nald")
            .WithColumn("CODE").AsString(1).NotNullable()
            .WithColumn("DESCR").AsString(30).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ALSF_PK").OnTable("NALD_LOSS_FACTORS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_LOSS_FACTOR_VALS").InSchema("nald")
            .WithColumn("ALSF_CODE").AsString(1).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("VALUE").AsDecimal(6, 3).NotNullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            ;
        Create.PrimaryKey("ALFV_PK").OnTable("NALD_LOSS_FACTOR_VALS").WithSchema("nald").Columns("ALSF_CODE", "EFF_ST_DATE");

        Create.Table("NALD_MAN_REP_CODES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("USER_ID").AsString(30).NotNullable()
            .WithColumn("REPORT_DATETIME").AsDateTime().NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AMRC_PK").OnTable("NALD_MAN_REP_CODES").WithSchema("nald").Columns("FGAC_REGION_CODE", "CODE", "USER_ID", "REPORT_DATETIME");

        Create.Table("NALD_MAN_UNITS").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("NAME").AsString(80).NotNullable()
            .WithColumn("UNIT_TYPE").AsString(1).NotNullable()
            .WithColumn("NGR_SHEET").AsString(2).NotNullable()
            .WithColumn("NGR_EAST").AsString(5).NotNullable()
            .WithColumn("NGR_NORTH").AsString(5).NotNullable()
            .WithColumn("CART_EAST").AsInt32().NotNullable()
            .WithColumn("CART_NORTH").AsInt32().NotNullable()
            .WithColumn("APTY_CODE").AsString(5).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("THEO_GROSS_AVG_RES").AsDecimal(8, 2).Nullable()
            .WithColumn("LIC_AVG_RES").AsDecimal(8, 2).Nullable()
            .WithColumn("THEO_GROSS_PEAK_RES").AsDecimal(8, 2).Nullable()
            .WithColumn("LIC_PEAK_RES").AsDecimal(8, 2).Nullable()
            .WithColumn("AMLA_CODE").AsString(5).Nullable()
            .WithColumn("APFR_CODE").AsString(5).Nullable()
            .WithColumn("ASLA_CODE").AsString(5).Nullable()
            .WithColumn("ATLL_CODE").AsString(5).Nullable()
            .WithColumn("LIC_STATUS_TEXT").AsString(2000).Nullable()
            .WithColumn("CTRL_PT_NAME").AsString(60).Nullable()
            .WithColumn("CTRL_PT_NGR_SHEET").AsString(2).Nullable()
            .WithColumn("CTRL_PT_NGR_EAST").AsString(5).Nullable()
            .WithColumn("CTRL_PT_NGR_NORTH").AsString(5).Nullable()
            .WithColumn("CTRL_PT_CART_EAST").AsInt32().Nullable()
            .WithColumn("CTRL_PT_CART_NORTH").AsInt32().Nullable()
            .WithColumn("CTRL_PT_REASON_TEXT").AsString(70).Nullable()
            .WithColumn("AMAN_CODE").AsString(5).Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AMAN_PK").OnTable("NALD_MAN_UNITS").WithSchema("nald").Columns("FGAC_REGION_CODE", "CODE");

        Create.Table("NALD_MAN_UNIT_POINTS").InSchema("nald")
            .WithColumn("AAIP_ID").AsInt32().NotNullable()
            .WithColumn("AMAN_CODE").AsString(5).NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AMUP_PK").OnTable("NALD_MAN_UNIT_POINTS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AAIP_ID", "AMAN_CODE");

        Create.Table("NALD_MEANS_OF_ABS").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            ;
        Create.PrimaryKey("AMOA_PK").OnTable("NALD_MEANS_OF_ABS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_MEANS_OF_IMP").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            ;
        Create.PrimaryKey("AMOI_PK").OnTable("NALD_MEANS_OF_IMP").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_MEANS_OF_MEASURE").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            ;
        Create.PrimaryKey("AMOM_PK").OnTable("NALD_MEANS_OF_MEASURE").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_MESSAGES").InSchema("nald")
            .WithColumn("MESSAGE_NUMBER").AsString(5).NotNullable()
            .WithColumn("MESSAGE_TEXT").AsString(255).NotNullable()
            .WithColumn("REFERENCED_BY").AsString(70).Nullable()
            ;
        Create.PrimaryKey("NMES_PK").OnTable("NALD_MESSAGES").WithSchema("nald").Columns("MESSAGE_NUMBER");

        Create.Table("NALD_MOD_LOGS").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("CREATE_DATE").AsDateTime().NotNullable()
            .WithColumn("USER_ID").AsString(30).NotNullable()
            .WithColumn("EVENT").AsString(6).NotNullable()
            .WithColumn("AABL_ID").AsInt32().Nullable()
            .WithColumn("AIMP_ID").AsInt32().Nullable()
            .WithColumn("AMRE_AMRE_TYPE").AsString(4).Nullable()
            .WithColumn("AMRE_CODE").AsString(5).Nullable()
            .WithColumn("AABV_AABL_ID").AsInt32().Nullable()
            .WithColumn("AABV_ISSUE_NO").AsInt16().Nullable()
            .WithColumn("AABV_INCR_NO").AsInt16().Nullable()
            .WithColumn("ARVN_AABL_ID").AsInt32().Nullable()
            .WithColumn("ARVN_VERS_NO").AsInt16().Nullable()
            .WithColumn("ACVR_AABL_ID").AsInt32().Nullable()
            .WithColumn("ACVR_VERS_NO").AsInt16().Nullable()
            .WithColumn("AIMV_AIMP_ID").AsInt32().Nullable()
            .WithColumn("AIMV_ISSUE_NO").AsInt16().Nullable()
            .WithColumn("AIMV_INCR_NO").AsInt16().Nullable()
            .WithColumn("TEXT").AsString(300).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AMOD_PK").OnTable("NALD_MOD_LOGS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_MOD_REASONS").InSchema("nald")
            .WithColumn("AMRE_TYPE").AsString(4).NotNullable()
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("AMRE_PK").OnTable("NALD_MOD_REASONS").WithSchema("nald").Columns("AMRE_TYPE", "CODE");

        Create.Table("NALD_NGR_CONVERSIONS").InSchema("nald")
            .WithColumn("NGR_SHEET").AsString(2).NotNullable()
            .WithColumn("CART_EAST_PREFIX").AsInt16().NotNullable()
            .WithColumn("CART_NORTH_PREFIX").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ANSR_PK").OnTable("NALD_NGR_CONVERSIONS").WithSchema("nald").Columns("NGR_SHEET");

        Create.Table("NALD_NRW_DELETIONS_AUDIT").InSchema("nald")
            .WithColumn("TABLE_NAME").AsString(32).Nullable()
            .WithColumn("BEFORE").AsDecimal().Nullable()
            .WithColumn("AFTER").AsDecimal().Nullable()
            .WithColumn("DELETED").AsDecimal().Nullable()
            .WithColumn("D_CHECK").AsDecimal().Nullable()
            ;

        Create.Table("NALD_PARTIES").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("APAR_TYPE").AsString(3).NotNullable()
            .WithColumn("NAME").AsString(60).NotNullable()
            .WithColumn("SPOKEN_LANG").AsString(1).NotNullable()
            .WithColumn("WRITTEN_LANG").AsString(1).NotNullable()
            .WithColumn("LAST_CHANGED").AsDateTime().NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("FORENAME").AsString(60).Nullable()
            .WithColumn("INITIALS").AsString(5).Nullable()
            .WithColumn("SALUTATION").AsString(20).Nullable()
            .WithColumn("REF").AsString(30).Nullable()
            .WithColumn("DESCR").AsString(2000).Nullable()
            .WithColumn("LOCAL_NAME").AsString(60).Nullable()
            .WithColumn("ASIC_ASID_DIVISION").AsString(2).Nullable()
            .WithColumn("ASIC_CLASS").AsString(2).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("APAR_PK").OnTable("NALD_PARTIES").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_POINTS").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("NGR1_SHEET").AsString(2).NotNullable()
            .WithColumn("NGR1_EAST").AsString(5).NotNullable()
            .WithColumn("NGR1_NORTH").AsString(5).NotNullable()
            .WithColumn("CART1_EAST").AsInt32().NotNullable()
            .WithColumn("CART1_NORTH").AsInt32().NotNullable()
            .WithColumn("LOCAL_NAME").AsString(60).NotNullable()
            .WithColumn("ASRC_CODE").AsString(15).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("LOCAL_NAME_WELSH").AsString(60).Nullable()
            .WithColumn("NGR2_SHEET").AsString(2).Nullable()
            .WithColumn("NGR2_EAST").AsString(5).Nullable()
            .WithColumn("NGR2_NORTH").AsString(5).Nullable()
            .WithColumn("CART2_EAST").AsInt32().Nullable()
            .WithColumn("CART2_NORTH").AsInt32().Nullable()
            .WithColumn("NGR3_SHEET").AsString(2).Nullable()
            .WithColumn("NGR3_EAST").AsString(5).Nullable()
            .WithColumn("NGR3_NORTH").AsString(5).Nullable()
            .WithColumn("CART3_EAST").AsInt32().Nullable()
            .WithColumn("CART3_NORTH").AsInt32().Nullable()
            .WithColumn("NGR4_SHEET").AsString(2).Nullable()
            .WithColumn("NGR4_EAST").AsString(5).Nullable()
            .WithColumn("NGR4_NORTH").AsString(5).Nullable()
            .WithColumn("CART4_EAST").AsInt32().Nullable()
            .WithColumn("CART4_NORTH").AsInt32().Nullable()
            .WithColumn("AAPC_CODE").AsString(5).Nullable()
            .WithColumn("AAPT_APTP_CODE").AsString(5).Nullable()
            .WithColumn("AAPT_APTS_CODE").AsString(5).Nullable()
            .WithColumn("ABAN_CODE").AsString(5).Nullable()
            .WithColumn("LOCATION_TEXT").AsString(200).Nullable()
            .WithColumn("AADD_ID").AsInt32().Nullable()
            .WithColumn("DEPTH").AsDecimal(7, 2).Nullable()
            .WithColumn("WRB_NO").AsString(12).Nullable()
            .WithColumn("BGS_NO").AsString(12).Nullable()
            .WithColumn("REG_WELL_INDEX_REF").AsString(12).Nullable()
            .WithColumn("HYDRO_REF").AsString(49).Nullable()
            .WithColumn("HYDRO_INTERCEPT_DIST").AsDecimal(8, 4).Nullable()
            .WithColumn("HYDRO_GW_OFFSET_DIST").AsDecimal(8, 4).Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AAIP_PK").OnTable("NALD_POINTS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_POINT_CATEGORIES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(200).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("AAPC_PK").OnTable("NALD_POINT_CATEGORIES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_POINT_TYPES").InSchema("nald")
            .WithColumn("APTP_CODE").AsString(5).NotNullable()
            .WithColumn("APTS_CODE").AsString(5).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("AAPT_PK").OnTable("NALD_POINT_TYPES").WithSchema("nald").Columns("APTP_CODE", "APTS_CODE");

        Create.Table("NALD_POINT_TYPE_PRIMS").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("APTP_PK").OnTable("NALD_POINT_TYPE_PRIMS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_POINT_TYPE_SECS").InSchema("nald")
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            ;
        Create.PrimaryKey("APTS_PK").OnTable("NALD_POINT_TYPE_SECS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_POSTAL_COUNTIES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("NAME").AsString(30).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("APCO_PK").OnTable("NALD_POSTAL_COUNTIES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_PRES_FLOW_RESTS").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("APFR_PK").OnTable("NALD_PRES_FLOW_RESTS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_PRINTER_DRIVERS").InSchema("nald")
            .WithColumn("NAME").AsString(12).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            ;
        Create.PrimaryKey("APDR_PK").OnTable("NALD_PRINTER_DRIVERS").WithSchema("nald").Columns("NAME");

        Create.Table("NALD_PROC_DETAILS").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("BUS_FUNC_AREA").AsString(3).NotNullable()
            .WithColumn("MODULE_NAME").AsString(6).NotNullable()
            .WithColumn("ST_DATETIME").AsDateTime().NotNullable()
            .WithColumn("STATUS").AsString(1).NotNullable()
            .WithColumn("END_DATETIME").AsDateTime().Nullable()
            .WithColumn("NMES_MESSAGE_NUMBER").AsString(5).Nullable()
            .WithColumn("RECORD_DETAILS").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("APRD_PK").OnTable("NALD_PROC_DETAILS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_PURPOSES").InSchema("nald")
            .WithColumn("APPR_CODE").AsString(1).NotNullable()
            .WithColumn("APSE_CODE").AsString(3).NotNullable()
            .WithColumn("APUS_CODE").AsInt16().NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("APUR_PK").OnTable("NALD_PURPOSES").WithSchema("nald").Columns("APPR_CODE", "APSE_CODE", "APUS_CODE");

        Create.Table("NALD_PURP_PRIMS").InSchema("nald")
            .WithColumn("CODE").AsString(1).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("APPR_PK").OnTable("NALD_PURP_PRIMS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_PURP_SECS").InSchema("nald")
            .WithColumn("CODE").AsString(3).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("APSE_PK").OnTable("NALD_PURP_SECS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_PURP_USES").InSchema("nald")
            .WithColumn("CODE").AsInt16().NotNullable()
            .WithColumn("DESCR").AsString(200).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("ALSF_CODE").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("APUS_PK").OnTable("NALD_PURP_USES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_REF_CODES").InSchema("nald")
            .WithColumn("RV_LOW_VALUE").AsString(240).NotNullable()
            .WithColumn("RV_HIGH_VALUE").AsString(240).Nullable()
            .WithColumn("RV_ABBREVIATION").AsString(240).Nullable()
            .WithColumn("RV_DOMAIN").AsString(100).NotNullable()
            .WithColumn("RV_MEANING").AsString(240).Nullable()
            .WithColumn("RV_TYPE").AsString(10).Nullable()
            ;

        Create.Table("NALD_REPORTS").InSchema("nald")
            .WithColumn("NAME").AsString(6).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("RUN_MODE").AsString(6).NotNullable()
            .WithColumn("BUS_SUB_DIR").AsString(10).NotNullable()
            .WithColumn("FILE_PREFIX").AsString(4).Nullable()
            ;
        Create.PrimaryKey("ARTS_PK").OnTable("NALD_REPORTS").WithSchema("nald").Columns("NAME");

        Create.Table("NALD_REPORT_DRIVERS").InSchema("nald")
            .WithColumn("ARTS_NAME").AsString(6).NotNullable()
            .WithColumn("APDR_NAME").AsString(12).NotNullable()
            ;
        Create.PrimaryKey("ARDR_PK").OnTable("NALD_REPORT_DRIVERS").WithSchema("nald").Columns("ARTS_NAME", "APDR_NAME");

        Create.Table("NALD_REPORT_LICENCES").InSchema("nald")
            .WithColumn("AABL_ID").AsInt32().NotNullable()
            .WithColumn("USER_ID").AsString(30).NotNullable()
            .WithColumn("REPORT_DATETIME").AsDateTime().NotNullable()
            .WithColumn("AABL_LIC_NO").AsString(20).NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AREL_PK").OnTable("NALD_REPORT_LICENCES").WithSchema("nald").Columns("FGAC_REGION_CODE", "AABL_ID", "USER_ID", "REPORT_DATETIME");

        Create.Table("NALD_REP_UNITS").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("NAME").AsString(80).NotNullable()
            .WithColumn("NGR_SHEET").AsString(2).NotNullable()
            .WithColumn("NGR_EAST").AsString(5).NotNullable()
            .WithColumn("NGR_NORTH").AsString(5).NotNullable()
            .WithColumn("CART_EAST").AsInt32().NotNullable()
            .WithColumn("CART_NORTH").AsInt32().NotNullable()
            .WithColumn("ARUT_CODE").AsString(5).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("AREP_CODE").AsString(5).Nullable()
            .WithColumn("ACON_AADD_ID").AsInt32().Nullable()
            .WithColumn("ACON_APAR_ID").AsInt32().Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("AREP_PK").OnTable("NALD_REP_UNITS").WithSchema("nald").Columns("FGAC_REGION_CODE", "CODE");

        Create.Table("NALD_REP_UNIT_POINTS").InSchema("nald")
            .WithColumn("AAIP_ID").AsInt32().NotNullable()
            .WithColumn("AREP_CODE").AsString(5).NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ARUP_PK").OnTable("NALD_REP_UNIT_POINTS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AAIP_ID", "AREP_CODE");

        Create.Table("NALD_REP_UNIT_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(200).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("USED_BY_SYS").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ARUT_PK").OnTable("NALD_REP_UNIT_TYPES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_RET_AGENCY_FREQS").InSchema("nald")
            .WithColumn("REC_FREQ_CODE").AsString(5).NotNullable()
            .WithColumn("RET_FREQ_CODE").AsString(5).NotNullable()
            .WithColumn("REC_FREQ_DESCR").AsString(70).NotNullable()
            .WithColumn("RET_FREQ_DESCR").AsString(200).NotNullable()
            .WithColumn("NO_OF_DAYS_GRACE").AsInt16().NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ARAF_PK").OnTable("NALD_RET_AGENCY_FREQS").WithSchema("nald").Columns("REC_FREQ_CODE", "RET_FREQ_CODE");

        Create.Table("NALD_RET_COL_FREQS").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ARCF_PK").OnTable("NALD_RET_COL_FREQS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_RET_FMT_POINTS").InSchema("nald")
            .WithColumn("ARTY_ID").AsInt32().NotNullable()
            .WithColumn("AAIP_ID").AsInt32().NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ARFP_PK").OnTable("NALD_RET_FMT_POINTS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ARTY_ID", "AAIP_ID");

        Create.Table("NALD_RET_FMT_PURPOSES").InSchema("nald")
            .WithColumn("ARTY_ID").AsInt32().NotNullable()
            .WithColumn("APUR_APPR_CODE").AsString(1).NotNullable()
            .WithColumn("APUR_APSE_CODE").AsString(3).NotNullable()
            .WithColumn("APUR_APUS_CODE").AsInt16().NotNullable()
            .WithColumn("PURP_ALIAS").AsString(70).Nullable()
            .WithColumn("PURP_ALIAS_WELSH").AsString(70).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ARPU_PK").OnTable("NALD_RET_FMT_PURPOSES").WithSchema("nald").Columns("FGAC_REGION_CODE", "ARTY_ID", "APUR_APPR_CODE", "APUR_APSE_CODE", "APUR_APUS_CODE");

        Create.Table("NALD_RET_FORMATS").InSchema("nald")
            .WithColumn("ID").AsInt32().NotNullable()
            .WithColumn("ARVN_AABL_ID").AsInt32().NotNullable()
            .WithColumn("ARVN_VERS_NO").AsInt16().NotNullable()
            .WithColumn("RETURN_FORM_TYPE").AsString(1).NotNullable()
            .WithColumn("ARTC_CODE").AsString(5).NotNullable()
            .WithColumn("ARTC_REC_FREQ_CODE").AsString(5).NotNullable()
            .WithColumn("ARTC_RET_FREQ_CODE").AsString(5).NotNullable()
            .WithColumn("FORMS_REQ_ALL_YEAR").AsString(1).NotNullable()
            .WithColumn("FORM_PRODN_MONTH").AsInt16().NotNullable()
            .WithColumn("NO_OF_DAYS_GRACE").AsInt16().NotNullable()
            .WithColumn("TPT_FLAG").AsString(1).NotNullable()
            .WithColumn("ABS_PERIOD_ST_DAY").AsInt16().Nullable()
            .WithColumn("ABS_PERIOD_ST_MONTH").AsInt16().Nullable()
            .WithColumn("ABS_PERIOD_END_DAY").AsInt16().Nullable()
            .WithColumn("ABS_PERIOD_END_MONTH").AsInt16().Nullable()
            .WithColumn("TIMELTD_ST_DATE").AsDateTime().Nullable()
            .WithColumn("TIMELTD_END_DATE").AsDateTime().Nullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            .WithColumn("SITE_DESCR").AsString(70).Nullable()
            .WithColumn("DESCR").AsString(70).Nullable()
            .WithColumn("ANNUAL_QTY").AsDecimal(17, 3).Nullable()
            .WithColumn("ANNUAL_QTY_USABILITY").AsString(1).Nullable()
            .WithColumn("CC_IND").AsString(1).NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ARTY_PK").OnTable("NALD_RET_FORMATS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ID");

        Create.Table("NALD_RET_FORM_LOGS").InSchema("nald")
            .WithColumn("ARTY_ID").AsInt32().NotNullable()
            .WithColumn("DATE_FROM").AsDateTime().NotNullable()
            .WithColumn("DATE_TO").AsDateTime().NotNullable()
            .WithColumn("UNDER_QUERY_FLAG").AsString(1).NotNullable()
            .WithColumn("FORM_PROD_NO").AsInt32().NotNullable()
            .WithColumn("FORM_PROD_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("FORM_PROD_IN_PROGRESS").AsString(1).NotNullable()
            .WithColumn("YEAR2_ST_DATE").AsDateTime().Nullable()
            .WithColumn("YEAR1_END_DATE").AsDateTime().Nullable()
            .WithColumn("ALRO_ID").AsInt32().Nullable()
            .WithColumn("ACON_APAR_ID_TO").AsInt32().Nullable()
            .WithColumn("ACON_AADD_ID_TO").AsInt32().Nullable()
            .WithColumn("SENT_DATE").AsDateTime().Nullable()
            .WithColumn("RECD_DATE").AsDateTime().Nullable()
            .WithColumn("REQD_BY_DATE").AsDateTime().Nullable()
            .WithColumn("CLOSED_DATE").AsDateTime().Nullable()
            .WithColumn("ACON_APAR_ID_FROM").AsInt32().Nullable()
            .WithColumn("ACON_AADD_ID_FROM").AsInt32().Nullable()
            .WithColumn("MONTHLY_RET_QTY").AsDecimal(17, 3).Nullable()
            .WithColumn("UNDER_QUERY_NOTE").AsString(70).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ARFL_PK").OnTable("NALD_RET_FORM_LOGS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ARTY_ID", "DATE_FROM");

        Create.Table("NALD_RET_FREQ_COMBS").InSchema("nald")
            .WithColumn("ARCF_CODE").AsString(5).NotNullable()
            .WithColumn("ARAF_REC_FREQ_CODE").AsString(5).NotNullable()
            .WithColumn("ARAF_RET_FREQ_CODE").AsString(5).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ARTC_PK").OnTable("NALD_RET_FREQ_COMBS").WithSchema("nald").Columns("ARCF_CODE", "ARAF_REC_FREQ_CODE", "ARAF_RET_FREQ_CODE");

        Create.Table("NALD_RET_LINES").InSchema("nald")
            .WithColumn("ARFL_ARTY_ID").AsInt32().NotNullable()
            .WithColumn("ARFL_DATE_FROM").AsDateTime().NotNullable()
            .WithColumn("RET_DATE").AsDateTime().NotNullable()
            .WithColumn("RET_QTY").AsDecimal(17, 3).Nullable()
            .WithColumn("RET_QTY_USABILITY").AsString(1).NotNullable()
            .WithColumn("UNIT_RET_FLAG").AsString(1).Nullable()
            .WithColumn("ATPT_ACEL_ID").AsInt32().Nullable()
            .WithColumn("ATPT_FIN_YEAR").AsInt16().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ARLN_PK").OnTable("NALD_RET_LINES").WithSchema("nald").Columns("FGAC_REGION_CODE", "ARFL_ARTY_ID", "ARFL_DATE_FROM", "RET_DATE");

        Create.Table("NALD_RET_LOG_ERRORS").InSchema("nald")
            .WithColumn("NALD_ERROR").AsString(300).Nullable()
            .WithColumn("ORACLE_ERROR").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            .WithColumn("FORM_PROD_NO").AsInt32().NotNullable()
            .WithColumn("ARTY_ID").AsInt32().NotNullable()
            .WithColumn("ARVN_VERS_NO").AsInt16().NotNullable()
            .WithColumn("LIC_NO").AsString(20).NotNullable()
            .WithColumn("ERR_DATETIME").AsDateTime().Nullable()
            ;
        Create.PrimaryKey("ARLE_PK").OnTable("NALD_RET_LOG_ERRORS").WithSchema("nald").Columns("FGAC_REGION_CODE", "FORM_PROD_NO", "ARTY_ID");

        Create.Table("NALD_RET_VERSIONS").InSchema("nald")
            .WithColumn("AABL_ID").AsInt32().NotNullable()
            .WithColumn("VERS_NO").AsInt16().NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("STATUS").AsString(5).NotNullable()
            .WithColumn("FORM_LOGS_REQD").AsString(1).NotNullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ARVN_PK").OnTable("NALD_RET_VERSIONS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AABL_ID", "VERS_NO");

        Create.Table("NALD_SCHED_JOBS_FGAC").InSchema("nald")
            .WithColumn("ID").AsInt32().Nullable()
            .WithColumn("JOB_TYPE").AsString(1).Nullable()
            .WithColumn("RUN_FILE").AsString(100).Nullable()
            .WithColumn("USER_ID").AsString(30).Nullable()
            .WithColumn("BUS_SUB_DIR").AsString(10).Nullable()
            .WithColumn("DESCR").AsString(70).Nullable()
            .WithColumn("DEFER_IND").AsString(1).Nullable()
            .WithColumn("SUB_DATETIME").AsDateTime().Nullable()
            .WithColumn("ST_DATETIME").AsDateTime().Nullable()
            .WithColumn("END_DATETIME").AsDateTime().Nullable()
            .WithColumn("STATUS").AsString(1).Nullable()
            .WithColumn("DESNAME").AsString(200).Nullable()
            .WithColumn("DESTYPE").AsString(1).Nullable()
            .WithColumn("PARAM_LIST").AsString(2000).Nullable()
            .WithColumn("DESFORMAT").AsString(200).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().Nullable()
            ;

        Create.Table("NALD_SEAS_FACTORS").InSchema("nald")
            .WithColumn("CODE").AsString(1).NotNullable()
            .WithColumn("DESCR").AsString(30).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("SEAS_ST_DAY").AsInt16().NotNullable()
            .WithColumn("SEAS_ST_MONTH").AsInt16().NotNullable()
            .WithColumn("SEAS_END_DAY").AsInt16().NotNullable()
            .WithColumn("SEAS_END_MONTH").AsInt16().NotNullable()
            .WithColumn("NO_OF_DAYS").AsInt16().NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ASFT_PK").OnTable("NALD_SEAS_FACTORS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_SEAS_FACTOR_VALS").InSchema("nald")
            .WithColumn("ASFT_CODE").AsString(1).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("VALUE").AsDecimal(6, 3).NotNullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            ;
        Create.PrimaryKey("ASFV_PK").OnTable("NALD_SEAS_FACTOR_VALS").WithSchema("nald").Columns("ASFT_CODE", "EFF_ST_DATE");

        Create.Table("NALD_SEAS_LIC_AVAILS").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ASLA_PK").OnTable("NALD_SEAS_LIC_AVAILS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_SOFTWARE").InSchema("nald")
            .WithColumn("SFT_ID").AsString(30).NotNullable()
            .WithColumn("SFT_NAME").AsString(74).NotNullable()
            .WithColumn("SFT_TYPE").AsString(7).NotNullable()
            .WithColumn("SFT_UPDTYPE").AsString(3).NotNullable()
            .WithColumn("SFT_DEFAULT").AsDecimal().NotNullable()
            .WithColumn("SFT_CHANGE").AsDecimal().NotNullable()
            .WithColumn("SFT_MANAGER").AsDecimal().NotNullable()
            ;
        Create.PrimaryKey("NSFT_PK").OnTable("NALD_SOFTWARE").WithSchema("nald").Columns("SFT_ID");

        Create.Table("NALD_SOFTWARE_PRIVS").InSchema("nald")
            .WithColumn("SFT_ID").AsString(30).NotNullable()
            .WithColumn("ROLE_NAME").AsString(30).NotNullable()
            .WithColumn("ROLE_PRIV").AsDecimal().NotNullable()
            ;
        Create.PrimaryKey("NSPR_PK").OnTable("NALD_SOFTWARE_PRIVS").WithSchema("nald").Columns("SFT_ID", "ROLE_NAME");

        Create.Table("NALD_SOFT_BUTTONS").InSchema("nald")
            .WithColumn("SFT_ID").AsString(30).NotNullable()
            .WithColumn("BUTTON_NUMBER").AsInt16().NotNullable()
            .WithColumn("SBT_SEQ").AsInt16().NotNullable()
            .WithColumn("SBT_DEFAULT").AsString(1).NotNullable()
            .WithColumn("SBT_CHANGE").AsString(1).NotNullable()
            .WithColumn("SBT_MANAGER").AsString(1).NotNullable()
            ;
        Create.PrimaryKey("NSBT_PK").OnTable("NALD_SOFT_BUTTONS").WithSchema("nald").Columns("SFT_ID", "BUTTON_NUMBER");

        Create.Table("NALD_SOFT_BUTTON_PRIVS").InSchema("nald")
            .WithColumn("BUTTON_NUMBER").AsInt16().NotNullable()
            .WithColumn("SFT_ID").AsString(30).NotNullable()
            .WithColumn("ROLE_NAME").AsString(30).NotNullable()
            ;
        Create.PrimaryKey("NSBP_PK").OnTable("NALD_SOFT_BUTTON_PRIVS").WithSchema("nald").Columns("SFT_ID", "BUTTON_NUMBER", "ROLE_NAME");

        Create.Table("NALD_SOURCES").InSchema("nald")
            .WithColumn("CODE").AsString(15).NotNullable()
            .WithColumn("NAME").AsString(60).NotNullable()
            .WithColumn("LOCAL_NAME").AsString(100).NotNullable()
            .WithColumn("SRC_TYPE").AsString(2).NotNullable()
            .WithColumn("NGR_SHEET").AsString(2).NotNullable()
            .WithColumn("NGR_EAST").AsString(5).NotNullable()
            .WithColumn("NGR_NORTH").AsString(5).NotNullable()
            .WithColumn("CART_EAST").AsInt32().NotNullable()
            .WithColumn("CART_NORTH").AsInt32().NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("AQUIFER_CLASS").AsString(5).Nullable()
            .WithColumn("NOTES").AsString(2000).Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ASRC_PK").OnTable("NALD_SOURCES").WithSchema("nald").Columns("FGAC_REGION_CODE", "CODE");

        Create.Table("NALD_SRC_FACTORS").InSchema("nald")
            .WithColumn("CODE").AsString(1).NotNullable()
            .WithColumn("DESCR").AsString(30).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ASRF_PK").OnTable("NALD_SRC_FACTORS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_SRC_FACTOR_VALS").InSchema("nald")
            .WithColumn("ASRF_CODE").AsString(1).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("VALUE").AsDecimal(6, 3).NotNullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            .WithColumn("EIUC_SRCE_VALUE").AsDecimal(6, 3).Nullable()
            ;
        Create.PrimaryKey("ASRV_PK").OnTable("NALD_SRC_FACTOR_VALS").WithSchema("nald").Columns("ASRF_CODE", "EFF_ST_DATE");

        Create.Table("NALD_STDIND_CLASSES").InSchema("nald")
            .WithColumn("ASID_DIVISION").AsString(2).NotNullable()
            .WithColumn("CLASS").AsString(2).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ASIC_PK").OnTable("NALD_STDIND_CLASSES").WithSchema("nald").Columns("ASID_DIVISION", "CLASS");

        Create.Table("NALD_STDIND_DIVISIONS").InSchema("nald")
            .WithColumn("DIVISION").AsString(2).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            ;
        Create.PrimaryKey("ASID_PK").OnTable("NALD_STDIND_DIVISIONS").WithSchema("nald").Columns("DIVISION");

        Create.Table("NALD_SUC_VALS").InSchema("nald")
            .WithColumn("AREP_CODE").AsString(5).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("SUC_VALUE").AsDecimal(6, 2).NotNullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ASUV_PK").OnTable("NALD_SUC_VALS").WithSchema("nald").Columns("FGAC_REGION_CODE", "AREP_CODE", "EFF_ST_DATE");

        Create.Table("NALD_SYSTEM_PARAMS").InSchema("nald")
            .WithColumn("REGION_CODE").AsString(1).NotNullable()
            .WithColumn("REGION_NAME").AsString(30).NotNullable()
            .WithColumn("INCOME_TYPE").AsString(1).NotNullable()
            .WithColumn("IAS_SYSTEM_CODE").AsString(3).NotNullable()
            .WithColumn("FIRST_BILLING_YEAR").AsInt16().NotNullable()
            .WithColumn("FIN_YEAR_ST_DAY").AsInt16().NotNullable()
            .WithColumn("FIN_YEAR_ST_MONTH").AsInt16().NotNullable()
            .WithColumn("FIN_YEAR_END_DAY").AsInt16().NotNullable()
            .WithColumn("FIN_YEAR_END_MONTH").AsInt16().NotNullable()
            .WithColumn("DFLT_SRC_FACTOR").AsString(1).NotNullable()
            .WithColumn("DFLT_DAYS_GRACE").AsInt16().NotNullable()
            .WithColumn("BANK_SORT_CODE").AsInt32().NotNullable()
            .WithColumn("BANK_ACCOUNT_NO").AsInt32().NotNullable()
            .WithColumn("GIRO_TERMINATOR").AsInt16().NotNullable()
            .WithColumn("OCR_FONT_SWITCH").AsString(1).NotNullable()
            .WithColumn("LAST_IAS_NAME_XFER").AsDateTime().Nullable()
            .WithColumn("FORM_PRODN_MONTH").AsInt16().Nullable()
            .WithColumn("ENQ_NAME").AsString(30).Nullable()
            .WithColumn("ENQ_NAME_WELSH").AsString(30).Nullable()
            .WithColumn("ENQ_TEL_NO").AsString(15).Nullable()
            .WithColumn("DFLT_VAT_CODE").AsString(1).Nullable()
            .WithColumn("PRINTER_DEFN_PATH").AsString(80).Nullable()
            .WithColumn("REPORT_DEST_PATH").AsString(80).Nullable()
            .WithColumn("REGION_NAME_WELSH").AsString(30).Nullable()
            .WithColumn("LAST_CUST_FILE_SEQ").AsInt32().NotNullable()
            .WithColumn("LAST_TRANS_FILE_SEQ").AsInt32().NotNullable()
            .WithColumn("FIMS_FILE_FREQUENCY").AsString(1).NotNullable()
            .WithColumn("FIMS_FILE_TIME").AsString(5).NotNullable()
            .WithColumn("FIMS_FILE_DAY").AsInt16().Nullable()
            .WithColumn("FIMS_FILE_DATE").AsInt16().Nullable()
            .WithColumn("FIMS_LAST_FILE_CREATED").AsDateTime().Nullable()
            .WithColumn("CUST_FILE_SET").AsString(1).NotNullable()
            .WithColumn("WA_LICS_ENABLED").AsString(1).Nullable()
            .WithColumn("TEMPORARY_LIC_CHARGEABLE").AsString(1).Nullable()
            .WithColumn("TRANSFER_LIC_CHARGEABLE").AsString(1).Nullable()
            .WithColumn("TEMP_LIC_LIMIT").AsInt16().Nullable()
            .WithColumn("DEREG_HIGH").AsDecimal(17, 3).Nullable()
            .WithColumn("DEREG_LOW").AsDecimal(17, 3).Nullable()
            .WithColumn("TLPA_APPLIED").AsString(1).NotNullable()
            .WithColumn("TLPA_APPLIED_DATE").AsDateTime().Nullable()
            .WithColumn("TLPA_FILE_ENABLED").AsString(1).NotNullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            .WithColumn("EIUC_COMP_ON").AsString(1).Nullable()
            ;
        Create.PrimaryKey("ANSD_PK").OnTable("NALD_SYSTEM_PARAMS").WithSchema("nald").Columns("FGAC_REGION_CODE", "REGION_CODE");

        Create.Table("NALD_TIMELTD_AVAILS").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ATLL_PK").OnTable("NALD_TIMELTD_AVAILS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_TLP_FACTORS").InSchema("nald")
            .WithColumn("CODE").AsString(1).NotNullable()
            .WithColumn("DESCR").AsString(30).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ATLP_PK").OnTable("NALD_TLP_FACTORS").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_TLP_FACTOR_VALS").InSchema("nald")
            .WithColumn("ASRF_CODE").AsString(1).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("VALUE").AsDecimal(6, 3).NotNullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            ;
        Create.PrimaryKey("ATLV_PK").OnTable("NALD_TLP_FACTOR_VALS").WithSchema("nald").Columns("ASRF_CODE", "EFF_ST_DATE");

        Create.Table("NALD_TPT_RETURNS").InSchema("nald")
            .WithColumn("ACEL_ID").AsInt32().NotNullable()
            .WithColumn("FIN_YEAR").AsInt16().NotNullable()
            .WithColumn("LATEST_RET_DATE").AsDateTime().NotNullable()
            .WithColumn("RET_RECD_DATE").AsDateTime().Nullable()
            .WithColumn("RETURN_QTY").AsDecimal(20, 6).Nullable()
            .WithColumn("BILLABLE_RET_QTY").AsDecimal(20, 6).Nullable()
            .WithColumn("AUTO_SUM_INDICATOR").AsString(1).Nullable()
            .WithColumn("RET_ENTRY_INDICATOR").AsString(1).Nullable()
            .WithColumn("BILLED_DATE").AsDateTime().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsInt16().NotNullable()
            ;
        Create.PrimaryKey("ATPT_PK").OnTable("NALD_TPT_RETURNS").WithSchema("nald").Columns("FGAC_REGION_CODE", "ACEL_ID", "FIN_YEAR");

        Create.Table("NALD_VAT_CODES").InSchema("nald")
            .WithColumn("CODE").AsString(3).NotNullable()
            .WithColumn("DESCR").AsString(30).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("AVAT_PK").OnTable("NALD_VAT_CODES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_VAT_RATES").InSchema("nald")
            .WithColumn("AVAT_CODE").AsString(3).NotNullable()
            .WithColumn("EFF_ST_DATE").AsDateTime().NotNullable()
            .WithColumn("VALUE").AsDecimal(6, 3).NotNullable()
            .WithColumn("EFF_END_DATE").AsDateTime().Nullable()
            ;
        Create.PrimaryKey("AVCV_PK").OnTable("NALD_VAT_RATES").WithSchema("nald").Columns("AVAT_CODE", "EFF_ST_DATE");

        Create.Table("NALD_WA_LIC_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ALWA_PK").OnTable("NALD_WA_LIC_TYPES").WithSchema("nald").Columns("CODE");

        Create.Table("NALD_YEAR_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString(5).NotNullable()
            .WithColumn("DESCR").AsString(70).NotNullable()
            .WithColumn("PERIOD_FROM_DAY").AsInt16().NotNullable()
            .WithColumn("PERIOD_FROM_MONTH").AsInt16().NotNullable()
            .WithColumn("PERIOD_TO_DAY").AsInt16().NotNullable()
            .WithColumn("PERIOD_TO_MONTH").AsInt16().NotNullable()
            .WithColumn("DISABLED").AsString(1).NotNullable()
            .WithColumn("DISP_ORD").AsInt32().Nullable()
            ;
        Create.PrimaryKey("ARYR_PK").OnTable("NALD_YEAR_TYPES").WithSchema("nald").Columns("CODE");

    }

    private void CreateForeignKeys()
    {
        Create.ForeignKey("APSC_AARC_FK")
            .FromTable("NALD_ABSTAT_CAT_PRIMS").InSchema("nald").ForeignColumns("AARC_STAT_REF")
            .ToTable("NALD_ABSTAT_CATGRIES").InSchema("nald").PrimaryColumns("STAT_REF").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("APSC_APPR_FK")
            .FromTable("NALD_ABSTAT_CAT_PRIMS").InSchema("nald").ForeignColumns("APPR_CODE")
            .ToTable("NALD_PURP_PRIMS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ASSC_AARC_FK")
            .FromTable("NALD_ABSTAT_CAT_SECS").InSchema("nald").ForeignColumns("AARC_STAT_REF")
            .ToTable("NALD_ABSTAT_CATGRIES").InSchema("nald").PrimaryColumns("STAT_REF").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ASSC_APSE_FK")
            .FromTable("NALD_ABSTAT_CAT_SECS").InSchema("nald").ForeignColumns("APSE_CODE")
            .ToTable("NALD_PURP_SECS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ACUR_AARC_FK")
            .FromTable("NALD_ABSTAT_CAT_USES").InSchema("nald").ForeignColumns("AARC_STAT_REF")
            .ToTable("NALD_ABSTAT_CATGRIES").InSchema("nald").PrimaryColumns("STAT_REF").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ACUR_APUS_FK1")
            .FromTable("NALD_ABSTAT_CAT_USES").InSchema("nald").ForeignColumns("APUS_CODE_FROM")
            .ToTable("NALD_PURP_USES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ACUR_APUS_FK2")
            .FromTable("NALD_ABSTAT_CAT_USES").InSchema("nald").ForeignColumns("APUS_CODE_TO")
            .ToTable("NALD_PURP_USES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AABE_AABL_FK")
            .FromTable("NALD_ABSTAT_EXCEPTIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABL_ID")
            .ToTable("NALD_ABS_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("AABE_AABV_FK")
            .FromTable("NALD_ABSTAT_EXCEPTIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABV_ID", "AABV_ISSUE_NO", "AABV_INCR_NO")
            .ToTable("NALD_ABS_LIC_VERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "AABL_ID", "ISSUE_NO", "INCR_NO");

        Create.ForeignKey("AABE_AAYR_FK")
            .FromTable("NALD_ABSTAT_EXCEPTIONS").InSchema("nald").ForeignColumns("AAYR_ARYR_CODE", "AAYR_YEAR")
            .ToTable("NALD_ABSTAT_YEARS").InSchema("nald").PrimaryColumns("ARYR_CODE", "YEAR");

        Create.ForeignKey("AABE_APUR_FK")
            .FromTable("NALD_ABSTAT_EXCEPTIONS").InSchema("nald").ForeignColumns("APUR_APPR_CODE", "APUR_APSE_CODE", "APUR_APUS_CODE")
            .ToTable("NALD_PURPOSES").InSchema("nald").PrimaryColumns("APPR_CODE", "APSE_CODE", "APUS_CODE");

        Create.ForeignKey("AABE_ARTY_FK")
            .FromTable("NALD_ABSTAT_EXCEPTIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ARTY_ID")
            .ToTable("NALD_RET_FORMATS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("AABE_NMES_FK")
            .FromTable("NALD_ABSTAT_EXCEPTIONS").InSchema("nald").ForeignColumns("NMES_MESSAGE_NUMBER")
            .ToTable("NALD_MESSAGES").InSchema("nald").PrimaryColumns("MESSAGE_NUMBER");

        Create.ForeignKey("ARAB_AABL_FK")
            .FromTable("NALD_ABSTAT_TOTALS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABL_ID")
            .ToTable("NALD_ABS_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ARAB_AAYR_FK")
            .FromTable("NALD_ABSTAT_TOTALS").InSchema("nald").ForeignColumns("AAYR_ARYR_CODE", "AAYR_YEAR")
            .ToTable("NALD_ABSTAT_YEARS").InSchema("nald").PrimaryColumns("ARYR_CODE", "YEAR");

        Create.ForeignKey("ARAB_APUR_FK")
            .FromTable("NALD_ABSTAT_TOTALS").InSchema("nald").ForeignColumns("APUR_APPR_CODE", "APUR_APSE_CODE", "APUR_APUS_CODE")
            .ToTable("NALD_PURPOSES").InSchema("nald").PrimaryColumns("APPR_CODE", "APSE_CODE", "APUS_CODE");

        Create.ForeignKey("AAYR_ARYR_FK")
            .FromTable("NALD_ABSTAT_YEARS").InSchema("nald").ForeignColumns("ARYR_CODE")
            .ToTable("NALD_YEAR_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AABL_AREP_FK1")
            .FromTable("NALD_ABS_LICENCES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AREP_SUC_CODE")
            .ToTable("NALD_REP_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AABL_AREP_FK2")
            .FromTable("NALD_ABS_LICENCES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AREP_LEAP_CODE")
            .ToTable("NALD_REP_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AABL_AREP_FK3")
            .FromTable("NALD_ABS_LICENCES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AREP_AREA_CODE")
            .ToTable("NALD_REP_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AABL_AREP_FK4")
            .FromTable("NALD_ABS_LICENCES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AREP_CAMS_CODE")
            .ToTable("NALD_REP_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AABP_AABV_FK")
            .FromTable("NALD_ABS_LIC_PURPOSES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABV_AABL_ID", "AABV_ISSUE_NO", "AABV_INCR_NO")
            .ToTable("NALD_ABS_LIC_VERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "AABL_ID", "ISSUE_NO", "INCR_NO").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("AABP_AMOM_FK")
            .FromTable("NALD_ABS_LIC_PURPOSES").InSchema("nald").ForeignColumns("AMOM_CODE")
            .ToTable("NALD_MEANS_OF_MEASURE").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AABP_APUR_FK")
            .FromTable("NALD_ABS_LIC_PURPOSES").InSchema("nald").ForeignColumns("APUR_APPR_CODE", "APUR_APSE_CODE", "APUR_APUS_CODE")
            .ToTable("NALD_PURPOSES").InSchema("nald").PrimaryColumns("APPR_CODE", "APSE_CODE", "APUS_CODE");

        Create.ForeignKey("AABP_AREC_FK")
            .FromTable("NALD_ABS_LIC_PURPOSES").InSchema("nald").ForeignColumns("AREC_CODE")
            .ToTable("NALD_LH_REC_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AALQ_AABV_FK")
            .FromTable("NALD_ABS_LIC_QUANTITIES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABV_AABL_ID", "AABV_ISSUE_NO", "AABV_INCR_NO")
            .ToTable("NALD_ABS_LIC_VERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "AABL_ID", "ISSUE_NO", "INCR_NO").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("AABV_AABL_FK")
            .FromTable("NALD_ABS_LIC_VERSIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABL_ID")
            .ToTable("NALD_ABS_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("AABV_ACCL_FK")
            .FromTable("NALD_ABS_LIC_VERSIONS").InSchema("nald").ForeignColumns("ACCL_CODE")
            .ToTable("NALD_CRIT_CLASSES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AABV_ACON_FK")
            .FromTable("NALD_ABS_LIC_VERSIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACON_APAR_ID", "ACON_AADD_ID")
            .ToTable("NALD_CONTACTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "APAR_ID", "AADD_ID");

        Create.ForeignKey("AABV_ALTY_FK")
            .FromTable("NALD_ABS_LIC_VERSIONS").InSchema("nald").ForeignColumns("ALTY_CODE")
            .ToTable("NALD_LIC_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AABV_ALWA_FK")
            .FromTable("NALD_ABS_LIC_VERSIONS").InSchema("nald").ForeignColumns("WA_ALTY_CODE")
            .ToTable("NALD_WA_LIC_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AABV_ASRC_FK")
            .FromTable("NALD_ABS_LIC_VERSIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ASRC_CODE")
            .ToTable("NALD_SOURCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AABV_DEDE_FK")
            .FromTable("NALD_ABS_LIC_VERSIONS").InSchema("nald").ForeignColumns("DEREG_CODE")
            .ToTable("NALD_DEREG_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AAPO_AABP_FK")
            .FromTable("NALD_ABS_PURP_POINTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABP_ID")
            .ToTable("NALD_ABS_LIC_PURPOSES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("AAPO_AAIP_FK")
            .FromTable("NALD_ABS_PURP_POINTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AAIP_ID")
            .ToTable("NALD_POINTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("AAPO_AMOA_FK")
            .FromTable("NALD_ABS_PURP_POINTS").InSchema("nald").ForeignColumns("AMOA_CODE")
            .ToTable("NALD_MEANS_OF_ABS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AADD_APCO_FK")
            .FromTable("NALD_ADDRESSES").InSchema("nald").ForeignColumns("APCO_CODE")
            .ToTable("NALD_POSTAL_COUNTIES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("NHLP_FK1")
            .FromTable("NALD_APP_FORM_HELP").InSchema("nald").ForeignColumns("HLP_MODTAB_NAME")
            .ToTable("NALD_SOFTWARE").InSchema("nald").PrimaryColumns("SFT_ID");

        Create.ForeignKey("ABCV_ABRN_FK")
            .FromTable("NALD_BILL_CHGVERSIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ABRN_FIN_YEAR", "ABRN_BILL_RUN_NO")
            .ToTable("NALD_BILL_RUNS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "FIN_YEAR", "BILL_RUN_NO");

        Create.ForeignKey("ABCV_ACVR_FK")
            .FromTable("NALD_BILL_CHGVERSIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACVR_AABL_ID", "ACVR_VERS_NO")
            .ToTable("NALD_CHG_VERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "AABL_ID", "VERS_NO");

        Create.ForeignKey("ABER_ABRN_FK")
            .FromTable("NALD_BILL_ERRORS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ABRN_FIN_YEAR", "ABRN_BILL_RUN_NO")
            .ToTable("NALD_BILL_RUNS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "FIN_YEAR", "BILL_RUN_NO");

        Create.ForeignKey("ABER_NMES_FK")
            .FromTable("NALD_BILL_ERRORS").InSchema("nald").ForeignColumns("NMES_MESSAGE_NUMBER")
            .ToTable("NALD_MESSAGES").InSchema("nald").PrimaryColumns("MESSAGE_NUMBER");

        Create.ForeignKey("ABHD_ABHD_FK")
            .FromTable("NALD_BILL_HEADERS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ABHD_ID")
            .ToTable("NALD_BILL_HEADERS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ABHD_ABRN_FK")
            .FromTable("NALD_BILL_HEADERS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ABRN_FIN_YEAR", "ABRN_BILL_RUN_NO")
            .ToTable("NALD_BILL_RUNS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "FIN_YEAR", "BILL_RUN_NO");

        Create.ForeignKey("ABHD_AIIA_FK")
            .FromTable("NALD_BILL_HEADERS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "LH_ACC_NO", "IAS_CUST_REF")
            .ToTable("NALD_IAS_INVOICE_ACCS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ALHA_ACC_NO", "IAS_CUST_REF");

        Create.ForeignKey("ABHD_ALHA_FK")
            .FromTable("NALD_BILL_HEADERS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "LH_ACC_NO")
            .ToTable("NALD_LH_ACCS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ACC_NO");

        Create.ForeignKey("ABPR_ABRN_FK")
            .FromTable("NALD_BILL_PROCESSES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ABRN_FIN_YEAR", "ABRN_BILL_RUN_NO")
            .ToTable("NALD_BILL_RUNS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "FIN_YEAR", "BILL_RUN_NO");

        Create.ForeignKey("ABTP_ABRN_FK")
            .FromTable("NALD_BILL_TPT_RETURNS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ABRN_FIN_YEAR", "ABRN_BILL_RUN_NO")
            .ToTable("NALD_BILL_RUNS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "FIN_YEAR", "BILL_RUN_NO");

        Create.ForeignKey("ABTP_ACEL_FK")
            .FromTable("NALD_BILL_TPT_RETURNS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACEL_ID")
            .ToTable("NALD_CHG_ELEMENTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ABTN_AABL_FK")
            .FromTable("NALD_BILL_TRANS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "LIC_ID")
            .ToTable("NALD_ABS_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ABTN_ABHD_FK")
            .FromTable("NALD_BILL_TRANS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ABHD_ID")
            .ToTable("NALD_BILL_HEADERS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ABTN_ABRN_FK")
            .FromTable("NALD_BILL_TRANS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ABRN_FIN_YEAR", "ABRN_BILL_RUN_NO")
            .ToTable("NALD_BILL_RUNS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "FIN_YEAR", "BILL_RUN_NO");

        Create.ForeignKey("ABTN_ACEL_FK")
            .FromTable("NALD_BILL_TRANS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACEL_ID")
            .ToTable("NALD_CHG_ELEMENTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ABTN_ACVR_FK")
            .FromTable("NALD_BILL_TRANS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "LIC_ID", "VERS_NO")
            .ToTable("NALD_CHG_VERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "AABL_ID", "VERS_NO");

        Create.ForeignKey("ABTN_AVAT_FK")
            .FromTable("NALD_BILL_TRANS").InSchema("nald").ForeignColumns("VAT_CODE")
            .ToTable("NALD_VAT_CODES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ABYR_ABCV_FK")
            .FromTable("NALD_BILL_YEARS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ABCV_ABRN_FIN_YEAR", "ABCV_ABRN_BILL_RUN_NO", "ABCV_ACVR_AABL_ID", "ABCV_ACVR_VERS_NO")
            .ToTable("NALD_BILL_CHGVERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ABRN_FIN_YEAR", "ABRN_BILL_RUN_NO", "ACVR_AABL_ID", "ACVR_VERS_NO");

        Create.ForeignKey("ACSA_ACEL_FK")
            .FromTable("NALD_CHG_AGRMNTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACEL_ID")
            .ToTable("NALD_CHG_ELEMENTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ACSA_AFSA_FK")
            .FromTable("NALD_CHG_AGRMNTS").InSchema("nald").ForeignColumns("AFSA_CODE")
            .ToTable("NALD_FIN_AGRMNT_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ACEL_ACVR_FK")
            .FromTable("NALD_CHG_ELEMENTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACVR_AABL_ID", "ACVR_VERS_NO")
            .ToTable("NALD_CHG_VERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "AABL_ID", "VERS_NO").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ACEL_ALSF_FK")
            .FromTable("NALD_CHG_ELEMENTS").InSchema("nald").ForeignColumns("ALSF_CODE")
            .ToTable("NALD_LOSS_FACTORS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ACEL_APUR_FK")
            .FromTable("NALD_CHG_ELEMENTS").InSchema("nald").ForeignColumns("APUR_APPR_CODE", "APUR_APSE_CODE", "APUR_APUS_CODE")
            .ToTable("NALD_PURPOSES").InSchema("nald").PrimaryColumns("APPR_CODE", "APSE_CODE", "APUS_CODE");

        Create.ForeignKey("ACEL_ASFT_FK1")
            .FromTable("NALD_CHG_ELEMENTS").InSchema("nald").ForeignColumns("ASFT_CODE")
            .ToTable("NALD_SEAS_FACTORS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ACEL_ASFT_FK2")
            .FromTable("NALD_CHG_ELEMENTS").InSchema("nald").ForeignColumns("ASFT_CODE_DERIVED")
            .ToTable("NALD_SEAS_FACTORS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ACEL_ASRF_FK")
            .FromTable("NALD_CHG_ELEMENTS").InSchema("nald").ForeignColumns("ASRF_CODE")
            .ToTable("NALD_SRC_FACTORS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ACVR_AABL_FK")
            .FromTable("NALD_CHG_VERSIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABL_ID")
            .ToTable("NALD_ABS_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ACVR_AIIA_FK")
            .FromTable("NALD_CHG_VERSIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AIIA_ALHA_ACC_NO", "AIIA_IAS_CUST_REF")
            .ToTable("NALD_IAS_INVOICE_ACCS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ALHA_ACC_NO", "IAS_CUST_REF");

        Create.ForeignKey("ACON_AADD_FK")
            .FromTable("NALD_CONTACTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AADD_ID")
            .ToTable("NALD_ADDRESSES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ACON_APAR_FK")
            .FromTable("NALD_CONTACTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "APAR_ID")
            .ToTable("NALD_PARTIES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ACNO_ACNT_FK")
            .FromTable("NALD_CONT_NOS").InSchema("nald").ForeignColumns("ACNT_CODE")
            .ToTable("NALD_CONT_NO_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ACNO_ACON_FK")
            .FromTable("NALD_CONT_NOS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACON_APAR_ID", "ACON_AADD_ID")
            .ToTable("NALD_CONTACTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "APAR_ID", "AADD_ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ACFL_AMAN_FK")
            .FromTable("NALD_CTRL_FLOWS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AMAN_CODE")
            .ToTable("NALD_MAN_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ACLE_AMAN_FK")
            .FromTable("NALD_CTRL_LEVELS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AMAN_CODE")
            .ToTable("NALD_MAN_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ADRF_AABL_FK")
            .FromTable("NALD_DOCUMENT_REFS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABL_ID")
            .ToTable("NALD_ABS_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ADRF_AIMP_FK")
            .FromTable("NALD_DOCUMENT_REFS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AIMP_ID")
            .ToTable("NALD_IMP_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("AEIUV_AREP_FK")
            .FromTable("NALD_EIUC_VALS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AREP_CODE")
            .ToTable("NALD_REP_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("ASPV_AFSA_FK")
            .FromTable("NALD_FIN_AGRMNT_VALS").InSchema("nald").ForeignColumns("AFSA_CODE")
            .ToTable("NALD_FIN_AGRMNT_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AGCA_ACON_FK")
            .FromTable("NALD_GROUP_LH_ACCS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACON_APAR_ID", "ACON_AADD_ID")
            .ToTable("NALD_CONTACTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "APAR_ID", "AADD_ID");

        Create.ForeignKey("AIIA_ACON_FK")
            .FromTable("NALD_IAS_INVOICE_ACCS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACON_APAR_ID", "ACON_AADD_ID")
            .ToTable("NALD_CONTACTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "APAR_ID", "AADD_ID");

        Create.ForeignKey("AIIA_ALHA_FK")
            .FromTable("NALD_IAS_INVOICE_ACCS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ALHA_ACC_NO")
            .ToTable("NALD_LH_ACCS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ACC_NO");

        Create.ForeignKey("AIMP_AREP_FK1")
            .FromTable("NALD_IMP_LICENCES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "LEAP")
            .ToTable("NALD_REP_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AIMP_AREP_FK2")
            .FromTable("NALD_IMP_LICENCES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AREA")
            .ToTable("NALD_REP_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AIMP_AREP_FK3")
            .FromTable("NALD_IMP_LICENCES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "CAMS")
            .ToTable("NALD_REP_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AIPU_AIMV_FK")
            .FromTable("NALD_IMP_LIC_PURPOSES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AIMV_AIMP_ID", "AIMV_ISSUE_NO", "AIMV_INCR_NO")
            .ToTable("NALD_IMP_LIC_VERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "AIMP_ID", "ISSUE_NO", "INCR_NO").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("AIPU_AISI_FK")
            .FromTable("NALD_IMP_LIC_PURPOSES").InSchema("nald").ForeignColumns("AISI_CODE")
            .ToTable("NALD_IMP_SITE_STATUSES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AIPU_AMOI_FK")
            .FromTable("NALD_IMP_LIC_PURPOSES").InSchema("nald").ForeignColumns("AMOI_CODE")
            .ToTable("NALD_MEANS_OF_IMP").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AIPU_APUR_FK")
            .FromTable("NALD_IMP_LIC_PURPOSES").InSchema("nald").ForeignColumns("APUR_APPR_CODE", "APUR_APSE_CODE", "APUR_APUS_CODE")
            .ToTable("NALD_PURPOSES").InSchema("nald").PrimaryColumns("APPR_CODE", "APSE_CODE", "APUS_CODE");

        Create.ForeignKey("AIMV_ACCL_FK")
            .FromTable("NALD_IMP_LIC_VERSIONS").InSchema("nald").ForeignColumns("ACCL_CODE")
            .ToTable("NALD_CRIT_CLASSES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AIMV_ACON_FK")
            .FromTable("NALD_IMP_LIC_VERSIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACON_APAR_ID", "ACON_AADD_ID")
            .ToTable("NALD_CONTACTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "APAR_ID", "AADD_ID");

        Create.ForeignKey("AIMV_AIMP_FK")
            .FromTable("NALD_IMP_LIC_VERSIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AIMP_ID")
            .ToTable("NALD_IMP_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("AIMV_ASRC_FK")
            .FromTable("NALD_IMP_LIC_VERSIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ASRC_CODE")
            .ToTable("NALD_SOURCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AIPO_AAIP_FK")
            .FromTable("NALD_IMP_PURP_POINTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AAIP_ID")
            .ToTable("NALD_POINTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("AIPO_AIPU_FK")
            .FromTable("NALD_IMP_PURP_POINTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AIPU_ID")
            .ToTable("NALD_IMP_LIC_PURPOSES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("AIPO_IMOI_FK")
            .FromTable("NALD_IMP_PURP_POINTS").InSchema("nald").ForeignColumns("IMOI_CODE")
            .ToTable("NALD_MEANS_OF_IMP").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ALHA_ACON_FK")
            .FromTable("NALD_LH_ACCS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACON_APAR_ID", "ACON_AADD_ID")
            .ToTable("NALD_CONTACTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "APAR_ID", "AADD_ID");

        Create.ForeignKey("ALHA_AGCA_FK")
            .FromTable("NALD_LH_ACCS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AGCA_ACC_NO")
            .ToTable("NALD_GROUP_LH_ACCS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ACC_NO");

        Create.ForeignKey("ALHS_AFSA_FK")
            .FromTable("NALD_LH_AGRMNTS").InSchema("nald").ForeignColumns("AFSA_CODE")
            .ToTable("NALD_FIN_AGRMNT_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ALHS_ALHA_FK")
            .FromTable("NALD_LH_AGRMNTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ALHA_ACC_NO")
            .ToTable("NALD_LH_ACCS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ACC_NO");

        Create.ForeignKey("ALSL_ALHA_FK")
            .FromTable("NALD_LH_SUSP_LOGS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ALHA_ACC_NO")
            .ToTable("NALD_LH_ACCS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ACC_NO");

        Create.ForeignKey("ALSL_AMRE_FK")
            .FromTable("NALD_LH_SUSP_LOGS").InSchema("nald").ForeignColumns("AMRE_AMRE_TYPE", "AMRE_CODE")
            .ToTable("NALD_MOD_REASONS").InSchema("nald").PrimaryColumns("AMRE_TYPE", "CODE");

        Create.ForeignKey("ALAG_AABP_FK")
            .FromTable("NALD_LIC_AGRMNTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABP_ID")
            .ToTable("NALD_ABS_LIC_PURPOSES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ALAG_AIPU_FK")
            .FromTable("NALD_LIC_AGRMNTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AIPU_ID")
            .ToTable("NALD_IMP_LIC_PURPOSES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ALAG_ALSA_FK")
            .FromTable("NALD_LIC_AGRMNTS").InSchema("nald").ForeignColumns("ALSA_CODE")
            .ToTable("NALD_LIC_AGRMNT_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ALCO_AABP_FK")
            .FromTable("NALD_LIC_CONDITIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABP_ID")
            .ToTable("NALD_ABS_LIC_PURPOSES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ALCO_ACIN_FK")
            .FromTable("NALD_LIC_CONDITIONS").InSchema("nald").ForeignColumns("ACIN_CODE", "ACIN_SUBCODE")
            .ToTable("NALD_LIC_COND_TYPES").InSchema("nald").PrimaryColumns("CODE", "SUBCODE");

        Create.ForeignKey("ALCO_AIPU_FK")
            .FromTable("NALD_LIC_CONDITIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AIPU_ID")
            .ToTable("NALD_IMP_LIC_PURPOSES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ALRO_AABL_FK")
            .FromTable("NALD_LIC_ROLES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABL_ID")
            .ToTable("NALD_ABS_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ALRO_ACON_FK")
            .FromTable("NALD_LIC_ROLES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACON_APAR_ID", "ACON_AADD_ID")
            .ToTable("NALD_CONTACTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "APAR_ID", "AADD_ID");

        Create.ForeignKey("ALRO_AIMP_FK")
            .FromTable("NALD_LIC_ROLES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AIMP_ID")
            .ToTable("NALD_IMP_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ALRO_ALRT_FK")
            .FromTable("NALD_LIC_ROLES").InSchema("nald").ForeignColumns("ALRT_CODE")
            .ToTable("NALD_LIC_ROLE_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ALFV_ALSF_FK")
            .FromTable("NALD_LOSS_FACTOR_VALS").InSchema("nald").ForeignColumns("ALSF_CODE")
            .ToTable("NALD_LOSS_FACTORS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AMAN_AMAN_FK")
            .FromTable("NALD_MAN_UNITS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AMAN_CODE")
            .ToTable("NALD_MAN_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AMAN_AMLA_FK")
            .FromTable("NALD_MAN_UNITS").InSchema("nald").ForeignColumns("AMLA_CODE")
            .ToTable("NALD_LIC_AVAILS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AMAN_APFR_FK")
            .FromTable("NALD_MAN_UNITS").InSchema("nald").ForeignColumns("APFR_CODE")
            .ToTable("NALD_PRES_FLOW_RESTS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AMAN_APTY_FK")
            .FromTable("NALD_MAN_UNITS").InSchema("nald").ForeignColumns("APTY_CODE")
            .ToTable("NALD_CTRL_POINT_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AMAN_ASLA_FK")
            .FromTable("NALD_MAN_UNITS").InSchema("nald").ForeignColumns("ASLA_CODE")
            .ToTable("NALD_SEAS_LIC_AVAILS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AMAN_ATLL_FK")
            .FromTable("NALD_MAN_UNITS").InSchema("nald").ForeignColumns("ATLL_CODE")
            .ToTable("NALD_TIMELTD_AVAILS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AMUP_AAIP_FK")
            .FromTable("NALD_MAN_UNIT_POINTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AAIP_ID")
            .ToTable("NALD_POINTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("AMUP_AMAN_FK")
            .FromTable("NALD_MAN_UNIT_POINTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AMAN_CODE")
            .ToTable("NALD_MAN_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AMOD_AABL_FK")
            .FromTable("NALD_MOD_LOGS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABL_ID")
            .ToTable("NALD_ABS_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("AMOD_AABV_FK")
            .FromTable("NALD_MOD_LOGS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABV_AABL_ID", "AABV_ISSUE_NO", "AABV_INCR_NO")
            .ToTable("NALD_ABS_LIC_VERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "AABL_ID", "ISSUE_NO", "INCR_NO").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("AMOD_ACVR_FK")
            .FromTable("NALD_MOD_LOGS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACVR_AABL_ID", "ACVR_VERS_NO")
            .ToTable("NALD_CHG_VERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "AABL_ID", "VERS_NO").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("AMOD_AIMP_FK")
            .FromTable("NALD_MOD_LOGS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AIMP_ID")
            .ToTable("NALD_IMP_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("AMOD_AIMV_FK")
            .FromTable("NALD_MOD_LOGS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AIMV_AIMP_ID", "AIMV_ISSUE_NO", "AIMV_INCR_NO")
            .ToTable("NALD_IMP_LIC_VERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "AIMP_ID", "ISSUE_NO", "INCR_NO").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("AMOD_AMRE_FK")
            .FromTable("NALD_MOD_LOGS").InSchema("nald").ForeignColumns("AMRE_AMRE_TYPE", "AMRE_CODE")
            .ToTable("NALD_MOD_REASONS").InSchema("nald").PrimaryColumns("AMRE_TYPE", "CODE");

        Create.ForeignKey("AMOD_ARVN_FK")
            .FromTable("NALD_MOD_LOGS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ARVN_AABL_ID", "ARVN_VERS_NO")
            .ToTable("NALD_RET_VERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "AABL_ID", "VERS_NO").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("APAR_ASIC_FK")
            .FromTable("NALD_PARTIES").InSchema("nald").ForeignColumns("ASIC_ASID_DIVISION", "ASIC_CLASS")
            .ToTable("NALD_STDIND_CLASSES").InSchema("nald").PrimaryColumns("ASID_DIVISION", "CLASS");

        Create.ForeignKey("AAIP_AADD_FK")
            .FromTable("NALD_POINTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AADD_ID")
            .ToTable("NALD_ADDRESSES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("AAIP_AAPC_FK")
            .FromTable("NALD_POINTS").InSchema("nald").ForeignColumns("AAPC_CODE")
            .ToTable("NALD_POINT_CATEGORIES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AAIP_AAPT_FK")
            .FromTable("NALD_POINTS").InSchema("nald").ForeignColumns("AAPT_APTP_CODE", "AAPT_APTS_CODE")
            .ToTable("NALD_POINT_TYPES").InSchema("nald").PrimaryColumns("APTP_CODE", "APTS_CODE");

        Create.ForeignKey("AAIP_ABAN_FK")
            .FromTable("NALD_POINTS").InSchema("nald").ForeignColumns("ABAN_CODE")
            .ToTable("NALD_BANK_CODES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AAIP_ASRC_FK")
            .FromTable("NALD_POINTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ASRC_CODE")
            .ToTable("NALD_SOURCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AAPT_APTP_FK")
            .FromTable("NALD_POINT_TYPES").InSchema("nald").ForeignColumns("APTP_CODE")
            .ToTable("NALD_POINT_TYPE_PRIMS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("AAPT_APTS_FK")
            .FromTable("NALD_POINT_TYPES").InSchema("nald").ForeignColumns("APTS_CODE")
            .ToTable("NALD_POINT_TYPE_SECS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("APRD_NMES_FK")
            .FromTable("NALD_PROC_DETAILS").InSchema("nald").ForeignColumns("NMES_MESSAGE_NUMBER")
            .ToTable("NALD_MESSAGES").InSchema("nald").PrimaryColumns("MESSAGE_NUMBER");

        Create.ForeignKey("APUR_APPR_FK")
            .FromTable("NALD_PURPOSES").InSchema("nald").ForeignColumns("APPR_CODE")
            .ToTable("NALD_PURP_PRIMS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("APUR_APSE_FK")
            .FromTable("NALD_PURPOSES").InSchema("nald").ForeignColumns("APSE_CODE")
            .ToTable("NALD_PURP_SECS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("APUR_APUS_FK")
            .FromTable("NALD_PURPOSES").InSchema("nald").ForeignColumns("APUS_CODE")
            .ToTable("NALD_PURP_USES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("APUS_ALSF_FK")
            .FromTable("NALD_PURP_USES").InSchema("nald").ForeignColumns("ALSF_CODE")
            .ToTable("NALD_LOSS_FACTORS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ARDR_APDR_FK")
            .FromTable("NALD_REPORT_DRIVERS").InSchema("nald").ForeignColumns("APDR_NAME")
            .ToTable("NALD_PRINTER_DRIVERS").InSchema("nald").PrimaryColumns("NAME");

        Create.ForeignKey("ARDR_ARTS_FK")
            .FromTable("NALD_REPORT_DRIVERS").InSchema("nald").ForeignColumns("ARTS_NAME")
            .ToTable("NALD_REPORTS").InSchema("nald").PrimaryColumns("NAME");

        Create.ForeignKey("AREL_AABL_FK")
            .FromTable("NALD_REPORT_LICENCES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABL_ID")
            .ToTable("NALD_ABS_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("AREP_ACON_FK")
            .FromTable("NALD_REP_UNITS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACON_APAR_ID", "ACON_AADD_ID")
            .ToTable("NALD_CONTACTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "APAR_ID", "AADD_ID");

        Create.ForeignKey("AREP_AREP_FK")
            .FromTable("NALD_REP_UNITS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AREP_CODE")
            .ToTable("NALD_REP_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("AREP_ARUT_FK")
            .FromTable("NALD_REP_UNITS").InSchema("nald").ForeignColumns("ARUT_CODE")
            .ToTable("NALD_REP_UNIT_TYPES").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ARUP_AAIP_FK")
            .FromTable("NALD_REP_UNIT_POINTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AAIP_ID")
            .ToTable("NALD_POINTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ARUP_AREP_FK")
            .FromTable("NALD_REP_UNIT_POINTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AREP_CODE")
            .ToTable("NALD_REP_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("ARFP_AAIP_FK")
            .FromTable("NALD_RET_FMT_POINTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AAIP_ID")
            .ToTable("NALD_POINTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ARFP_ARTY_FK")
            .FromTable("NALD_RET_FMT_POINTS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ARTY_ID")
            .ToTable("NALD_RET_FORMATS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ARPU_APUR_FK")
            .FromTable("NALD_RET_FMT_PURPOSES").InSchema("nald").ForeignColumns("APUR_APPR_CODE", "APUR_APSE_CODE", "APUR_APUS_CODE")
            .ToTable("NALD_PURPOSES").InSchema("nald").PrimaryColumns("APPR_CODE", "APSE_CODE", "APUS_CODE");

        Create.ForeignKey("ARPU_ARTY_FK")
            .FromTable("NALD_RET_FMT_PURPOSES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ARTY_ID")
            .ToTable("NALD_RET_FORMATS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ARTY_ARTC_FK")
            .FromTable("NALD_RET_FORMATS").InSchema("nald").ForeignColumns("ARTC_CODE", "ARTC_REC_FREQ_CODE", "ARTC_RET_FREQ_CODE")
            .ToTable("NALD_RET_FREQ_COMBS").InSchema("nald").PrimaryColumns("ARCF_CODE", "ARAF_REC_FREQ_CODE", "ARAF_RET_FREQ_CODE");

        Create.ForeignKey("ARTY_ARVN_FK")
            .FromTable("NALD_RET_FORMATS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ARVN_AABL_ID", "ARVN_VERS_NO")
            .ToTable("NALD_RET_VERSIONS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "AABL_ID", "VERS_NO").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ARFL_ACON_FK1")
            .FromTable("NALD_RET_FORM_LOGS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACON_APAR_ID_TO", "ACON_AADD_ID_TO")
            .ToTable("NALD_CONTACTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "APAR_ID", "AADD_ID");

        Create.ForeignKey("ARFL_ACON_FK2")
            .FromTable("NALD_RET_FORM_LOGS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACON_APAR_ID_FROM", "ACON_AADD_ID_FROM")
            .ToTable("NALD_CONTACTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "APAR_ID", "AADD_ID");

        Create.ForeignKey("ARFL_ALRO_FK")
            .FromTable("NALD_RET_FORM_LOGS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ALRO_ID")
            .ToTable("NALD_LIC_ROLES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ARFL_ARTY_FK")
            .FromTable("NALD_RET_FORM_LOGS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ARTY_ID")
            .ToTable("NALD_RET_FORMATS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ARTC_ARAF_FK")
            .FromTable("NALD_RET_FREQ_COMBS").InSchema("nald").ForeignColumns("ARAF_REC_FREQ_CODE", "ARAF_RET_FREQ_CODE")
            .ToTable("NALD_RET_AGENCY_FREQS").InSchema("nald").PrimaryColumns("REC_FREQ_CODE", "RET_FREQ_CODE");

        Create.ForeignKey("ARTC_ARCF_FK")
            .FromTable("NALD_RET_FREQ_COMBS").InSchema("nald").ForeignColumns("ARCF_CODE")
            .ToTable("NALD_RET_COL_FREQS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ARLN_ARFL_FK")
            .FromTable("NALD_RET_LINES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ARFL_ARTY_ID", "ARFL_DATE_FROM")
            .ToTable("NALD_RET_FORM_LOGS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ARTY_ID", "DATE_FROM");

        Create.ForeignKey("ARLN_ATPT_FK")
            .FromTable("NALD_RET_LINES").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ATPT_ACEL_ID", "ATPT_FIN_YEAR")
            .ToTable("NALD_TPT_RETURNS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ACEL_ID", "FIN_YEAR");

        Create.ForeignKey("ARVN_AABL_FK")
            .FromTable("NALD_RET_VERSIONS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AABL_ID")
            .ToTable("NALD_ABS_LICENCES").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("ASFV_ASFT_FK")
            .FromTable("NALD_SEAS_FACTOR_VALS").InSchema("nald").ForeignColumns("ASFT_CODE")
            .ToTable("NALD_SEAS_FACTORS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("NSPR_FK1")
            .FromTable("NALD_SOFTWARE_PRIVS").InSchema("nald").ForeignColumns("SFT_ID")
            .ToTable("NALD_SOFTWARE").InSchema("nald").PrimaryColumns("SFT_ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("NSBT_FK1")
            .FromTable("NALD_SOFT_BUTTONS").InSchema("nald").ForeignColumns("SFT_ID")
            .ToTable("NALD_SOFTWARE").InSchema("nald").PrimaryColumns("SFT_ID").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("NSBT_FK2")
            .FromTable("NALD_SOFT_BUTTONS").InSchema("nald").ForeignColumns("BUTTON_NUMBER")
            .ToTable("NALD_BUTTONS").InSchema("nald").PrimaryColumns("BUTTON_NUMBER").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("NSBP_FK2")
            .FromTable("NALD_SOFT_BUTTON_PRIVS").InSchema("nald").ForeignColumns("SFT_ID", "BUTTON_NUMBER")
            .ToTable("NALD_SOFT_BUTTONS").InSchema("nald").PrimaryColumns("SFT_ID", "BUTTON_NUMBER").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ASRV_ASRF_FK")
            .FromTable("NALD_SRC_FACTOR_VALS").InSchema("nald").ForeignColumns("ASRF_CODE")
            .ToTable("NALD_SRC_FACTORS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ASIC_ASID_FK")
            .FromTable("NALD_STDIND_CLASSES").InSchema("nald").ForeignColumns("ASID_DIVISION")
            .ToTable("NALD_STDIND_DIVISIONS").InSchema("nald").PrimaryColumns("DIVISION").OnDelete(System.Data.Rule.Cascade);

        Create.ForeignKey("ASUV_AREP_FK")
            .FromTable("NALD_SUC_VALS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "AREP_CODE")
            .ToTable("NALD_REP_UNITS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "CODE");

        Create.ForeignKey("ATLV_ASRF_FK")
            .FromTable("NALD_TLP_FACTOR_VALS").InSchema("nald").ForeignColumns("ASRF_CODE")
            .ToTable("NALD_TLP_FACTORS").InSchema("nald").PrimaryColumns("CODE");

        Create.ForeignKey("ATPT_ACEL_FK")
            .FromTable("NALD_TPT_RETURNS").InSchema("nald").ForeignColumns("FGAC_REGION_CODE", "ACEL_ID")
            .ToTable("NALD_CHG_ELEMENTS").InSchema("nald").PrimaryColumns("FGAC_REGION_CODE", "ID");

        Create.ForeignKey("AVCV_AVAT_FK")
            .FromTable("NALD_VAT_RATES").InSchema("nald").ForeignColumns("AVAT_CODE")
            .ToTable("NALD_VAT_CODES").InSchema("nald").PrimaryColumns("CODE");

    }
}
