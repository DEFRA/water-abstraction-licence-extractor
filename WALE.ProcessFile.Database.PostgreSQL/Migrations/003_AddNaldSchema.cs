using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(3)]
public class AddNaldSchema : Migration
{
    public override void Up()
    {
        Create.Schema("nald");

        // Table creation for NALD data
        CreateNaldTables();
    }

    public override void Down()
    {
        Delete.Schema("nald");
    }

    private void CreateNaldTables()
    {
        // STAT_REF,STAT_CATEGORY,ALL_PRIMARY,ALL_SECONDARY,ALL_USES,INCLUDE_IN_REPORT,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABSTAT_CATGRIES").InSchema("nald")
            .WithColumn("STAT_REF").AsString().Nullable()
            .WithColumn("STAT_CATEGORY").AsString().Nullable()
            .WithColumn("ALL_PRIMARY").AsString().Nullable()
            .WithColumn("ALL_SECONDARY").AsString().Nullable()
            .WithColumn("ALL_USES").AsString().Nullable()
            .WithColumn("INCLUDE_IN_REPORT").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AARC_STAT_REF,APPR_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABSTAT_CAT_PRIMS").InSchema("nald")
            .WithColumn("AARC_STAT_REF").AsString().Nullable()
            .WithColumn("APPR_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AARC_STAT_REF,APSE_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABSTAT_CAT_SECS").InSchema("nald")
            .WithColumn("AARC_STAT_REF").AsString().Nullable()
            .WithColumn("APSE_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AARC_STAT_REF,APUS_CODE_FROM,APUS_CODE_TO,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABSTAT_CAT_USES").InSchema("nald")
            .WithColumn("AARC_STAT_REF").AsString().Nullable()
            .WithColumn("APUS_CODE_FROM").AsString().Nullable()
            .WithColumn("APUS_CODE_TO").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AAYR_ARYR_CODE,AAYR_YEAR,AABL_ID,NMES_MESSAGE_NUMBER,LIC_NO,AABV_ID,AABV_ISSUE_NO,AABV_INCR_NO,ARTY_ID,APUR_APPR_CODE,APUR_APSE_CODE,APUR_APUS_CODE,ANN_AUTH_QTY,ANN_ACT_QTY,DATESTAMP,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABSTAT_EXCEPTIONS").InSchema("nald")
            .WithColumn("AAYR_ARYR_CODE").AsString().Nullable()
            .WithColumn("AAYR_YEAR").AsString().Nullable()
            .WithColumn("AABL_ID").AsString().Nullable()
            .WithColumn("NMES_MESSAGE_NUMBER").AsString().Nullable()
            .WithColumn("LIC_NO").AsString().Nullable()
            .WithColumn("AABV_ID").AsString().Nullable()
            .WithColumn("AABV_ISSUE_NO").AsString().Nullable()
            .WithColumn("AABV_INCR_NO").AsString().Nullable()
            .WithColumn("ARTY_ID").AsString().Nullable()
            .WithColumn("APUR_APPR_CODE").AsString().Nullable()
            .WithColumn("APUR_APSE_CODE").AsString().Nullable()
            .WithColumn("APUR_APUS_CODE").AsString().Nullable()
            .WithColumn("ANN_AUTH_QTY").AsString().Nullable()
            .WithColumn("ANN_ACT_QTY").AsString().Nullable()
            .WithColumn("DATESTAMP").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AABL_AREP_LEAP_CODE,AARC_STAT_REF,TW_TOT_AUTH_QTY,SW_TOT_AUTH_QTY,GW_TOT_AUTH_QTY,TW_TOT_ACT_QTY,SW_TOT_ACT_QTY,GW_TOT_ACT_QTY,TOT_LICENSED_RETURNED,TOT_NO_LICENCES,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABSTAT_REPORT_DATA").InSchema("nald")
            .WithColumn("AABL_AREP_LEAP_CODE").AsString().Nullable()
            .WithColumn("AARC_STAT_REF").AsString().Nullable()
            .WithColumn("TW_TOT_AUTH_QTY").AsString().Nullable()
            .WithColumn("SW_TOT_AUTH_QTY").AsString().Nullable()
            .WithColumn("GW_TOT_AUTH_QTY").AsString().Nullable()
            .WithColumn("TW_TOT_ACT_QTY").AsString().Nullable()
            .WithColumn("SW_TOT_ACT_QTY").AsString().Nullable()
            .WithColumn("GW_TOT_ACT_QTY").AsString().Nullable()
            .WithColumn("TOT_LICENSED_RETURNED").AsString().Nullable()
            .WithColumn("TOT_NO_LICENCES").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AAYR_ARYR_CODE,AAYR_YEAR,AABL_ID,APUR_APPR_CODE,APUR_APSE_CODE,APUR_APUS_CODE,ACT_OVERRIDDEN,AUTH_CALC_FROM_DAILY,AUTH_OVERRIDDEN,PREV_YEAR_AUTH_USED,SOURCE_TYPE,ANN_ACT_QTY,ANN_ACT_USABILITY,ANN_AUTH_QTY,ANN_AUTH_USABILITY,USER_NOTES,DELETED,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABSTAT_TOTALS").InSchema("nald")
            .WithColumn("AAYR_ARYR_CODE").AsString().Nullable()
            .WithColumn("AAYR_YEAR").AsString().Nullable()
            .WithColumn("AABL_ID").AsString().Nullable()
            .WithColumn("APUR_APPR_CODE").AsString().Nullable()
            .WithColumn("APUR_APSE_CODE").AsString().Nullable()
            .WithColumn("APUR_APUS_CODE").AsString().Nullable()
            .WithColumn("ACT_OVERRIDDEN").AsString().Nullable()
            .WithColumn("AUTH_CALC_FROM_DAILY").AsString().Nullable()
            .WithColumn("AUTH_OVERRIDDEN").AsString().Nullable()
            .WithColumn("PREV_YEAR_AUTH_USED").AsString().Nullable()
            .WithColumn("SOURCE_TYPE").AsString().Nullable()
            .WithColumn("ANN_ACT_QTY").AsString().Nullable()
            .WithColumn("ANN_ACT_USABILITY").AsString().Nullable()
            .WithColumn("ANN_AUTH_QTY").AsString().Nullable()
            .WithColumn("ANN_AUTH_USABILITY").AsString().Nullable()
            .WithColumn("USER_NOTES").AsString().Nullable()
            .WithColumn("DELETED").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ARYR_CODE,YEAR,SNAPSHOT_DATE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABSTAT_YEARS").InSchema("nald")
            .WithColumn("ARYR_CODE").AsString().Nullable()
            .WithColumn("YEAR").AsString().Nullable()
            .WithColumn("SNAPSHOT_DATE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,LIC_NO,AREP_SUC_CODE,AREP_AREA_CODE,SUSP_FROM_BILLING,AREP_LEAP_CODE,EXPIRY_DATE,ORIG_EFF_DATE,ORIG_SIG_DATE,ORIG_APP_NO,ORIG_LIC_NO,NOTES,REV_DATE,LAPSED_DATE,SUSP_FROM_RETURNS,AREP_CAMS_CODE,X_REG_IND,PREV_LIC_NO,FOLL_LIC_NO,AREP_EIUC_CODE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABS_LICENCES").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("LIC_NO").AsString().Nullable()
            .WithColumn("AREP_SUC_CODE").AsString().Nullable()
            .WithColumn("AREP_AREA_CODE").AsString().Nullable()
            .WithColumn("SUSP_FROM_BILLING").AsString().Nullable()
            .WithColumn("AREP_LEAP_CODE").AsString().Nullable()
            .WithColumn("EXPIRY_DATE").AsString().Nullable()
            .WithColumn("ORIG_EFF_DATE").AsString().Nullable()
            .WithColumn("ORIG_SIG_DATE").AsString().Nullable()
            .WithColumn("ORIG_APP_NO").AsString().Nullable()
            .WithColumn("ORIG_LIC_NO").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("REV_DATE").AsString().Nullable()
            .WithColumn("LAPSED_DATE").AsString().Nullable()
            .WithColumn("SUSP_FROM_RETURNS").AsString().Nullable()
            .WithColumn("AREP_CAMS_CODE").AsString().Nullable()
            .WithColumn("X_REG_IND").AsString().Nullable()
            .WithColumn("PREV_LIC_NO").AsString().Nullable()
            .WithColumn("FOLL_LIC_NO").AsString().Nullable()
            .WithColumn("AREP_EIUC_CODE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,AABV_AABL_ID,AABV_ISSUE_NO,AABV_INCR_NO,APUR_APPR_CODE,APUR_APSE_CODE,APUR_APUS_CODE,PERIOD_ST_DAY,PERIOD_ST_MONTH,PERIOD_END_DAY,PERIOD_END_MONTH,AMOM_CODE,ANNUAL_QTY,ANNUAL_QTY_USABILITY,DAILY_QTY,DAILY_QTY_USABILITY,HOURLY_QTY,HOURLY_QTY_USABILITY,INST_QTY,INST_QTY_USABILITY,TIMELTD_ST_DATE,TIMELTD_END_DATE,LANDS,AREC_CODE,DISP_ORD,NOTES,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABS_LIC_PURPOSES").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("AABV_AABL_ID").AsString().Nullable()
            .WithColumn("AABV_ISSUE_NO").AsString().Nullable()
            .WithColumn("AABV_INCR_NO").AsString().Nullable()
            .WithColumn("APUR_APPR_CODE").AsString().Nullable()
            .WithColumn("APUR_APSE_CODE").AsString().Nullable()
            .WithColumn("APUR_APUS_CODE").AsString().Nullable()
            .WithColumn("PERIOD_ST_DAY").AsString().Nullable()
            .WithColumn("PERIOD_ST_MONTH").AsString().Nullable()
            .WithColumn("PERIOD_END_DAY").AsString().Nullable()
            .WithColumn("PERIOD_END_MONTH").AsString().Nullable()
            .WithColumn("AMOM_CODE").AsString().Nullable()
            .WithColumn("ANNUAL_QTY").AsString().Nullable()
            .WithColumn("ANNUAL_QTY_USABILITY").AsString().Nullable()
            .WithColumn("DAILY_QTY").AsString().Nullable()
            .WithColumn("DAILY_QTY_USABILITY").AsString().Nullable()
            .WithColumn("HOURLY_QTY").AsString().Nullable()
            .WithColumn("HOURLY_QTY_USABILITY").AsString().Nullable()
            .WithColumn("INST_QTY").AsString().Nullable()
            .WithColumn("INST_QTY_USABILITY").AsString().Nullable()
            .WithColumn("TIMELTD_ST_DATE").AsString().Nullable()
            .WithColumn("TIMELTD_END_DATE").AsString().Nullable()
            .WithColumn("LANDS").AsString().Nullable()
            .WithColumn("AREC_CODE").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,AABV_AABL_ID,AABV_ISSUE_NO,AABV_INCR_NO,MAX_ANNUAL_QTY,MAX_DAILY_QTY,AGGREGATED_IND,PURP_POINTS_IND,USER_VALID_IND,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABS_LIC_QUANTITIES").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("AABV_AABL_ID").AsString().Nullable()
            .WithColumn("AABV_ISSUE_NO").AsString().Nullable()
            .WithColumn("AABV_INCR_NO").AsString().Nullable()
            .WithColumn("MAX_ANNUAL_QTY").AsString().Nullable()
            .WithColumn("MAX_DAILY_QTY").AsString().Nullable()
            .WithColumn("AGGREGATED_IND").AsString().Nullable()
            .WithColumn("PURP_POINTS_IND").AsString().Nullable()
            .WithColumn("USER_VALID_IND").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AABL_ID,ISSUE_NO,INCR_NO,AABV_TYPE,EFF_ST_DATE,STATUS,RETURNS_REQ,CHARGEABLE,ASRC_CODE,ACON_APAR_ID,ACON_AADD_ID,ALTY_CODE,ACCL_CODE,MULTIPLE_LH,LIC_SIG_DATE,APP_NO,LIC_DOC_FLAG,EFF_END_DATE,EXPIRY_DATE1,WA_ALTY_CODE,VOL_CONV,WRT_CODE,DEREG_CODE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABS_LIC_VERSIONS").InSchema("nald")
            .WithColumn("AABL_ID").AsString().Nullable()
            .WithColumn("ISSUE_NO").AsString().Nullable()
            .WithColumn("INCR_NO").AsString().Nullable()
            .WithColumn("AABV_TYPE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("STATUS").AsString().Nullable()
            .WithColumn("RETURNS_REQ").AsString().Nullable()
            .WithColumn("CHARGEABLE").AsString().Nullable()
            .WithColumn("ASRC_CODE").AsString().Nullable()
            .WithColumn("ACON_APAR_ID").AsString().Nullable()
            .WithColumn("ACON_AADD_ID").AsString().Nullable()
            .WithColumn("ALTY_CODE").AsString().Nullable()
            .WithColumn("ACCL_CODE").AsString().Nullable()
            .WithColumn("MULTIPLE_LH").AsString().Nullable()
            .WithColumn("LIC_SIG_DATE").AsString().Nullable()
            .WithColumn("APP_NO").AsString().Nullable()
            .WithColumn("LIC_DOC_FLAG").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("EXPIRY_DATE1").AsString().Nullable()
            .WithColumn("WA_ALTY_CODE").AsString().Nullable()
            .WithColumn("VOL_CONV").AsString().Nullable()
            .WithColumn("WRT_CODE").AsString().Nullable()
            .WithColumn("DEREG_CODE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AABP_ID,AAIP_ID,AMOA_CODE,NOTES,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ABS_PURP_POINTS").InSchema("nald")
            .WithColumn("AABP_ID").AsString().Nullable()
            .WithColumn("AAIP_ID").AsString().Nullable()
            .WithColumn("AMOA_CODE").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,ADDR_LINE1,LAST_CHANGED,DISABLED,ADDR_LINE2,ADDR_LINE3,ADDR_LINE4,TOWN,COUNTY,POSTCODE,COUNTRY,APCO_CODE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_ADDRESSES").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("ADDR_LINE1").AsString().Nullable()
            .WithColumn("LAST_CHANGED").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("ADDR_LINE2").AsString().Nullable()
            .WithColumn("ADDR_LINE3").AsString().Nullable()
            .WithColumn("ADDR_LINE4").AsString().Nullable()
            .WithColumn("TOWN").AsString().Nullable()
            .WithColumn("COUNTY").AsString().Nullable()
            .WithColumn("POSTCODE").AsString().Nullable()
            .WithColumn("COUNTRY").AsString().Nullable()
            .WithColumn("APCO_CODE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_BANK_CODES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ABRN_FIN_YEAR,ABRN_BILL_RUN_NO,ACVR_AABL_ID,ACVR_VERS_NO,EFF_ST_DATE,LH_ACC_NO,IAS_CUST_REF,CUT_OFF_DATE,CUT_OFF_IND,CREDIT_DEBIT_FACTOR,RETURNS_ACTUAL,BILLED_UPTO_DATE,NEW_OWNER_VERS,NEW_OWNER_YEAR,NEW_LIC_YEAR,BILLABLE_NOW,BILLABLE_NEXT,FGAC_REGION_CODE,RL_SET,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_BILL_CHGVERSIONS").InSchema("nald")
            .WithColumn("ABRN_FIN_YEAR").AsString().Nullable()
            .WithColumn("ABRN_BILL_RUN_NO").AsString().Nullable()
            .WithColumn("ACVR_AABL_ID").AsString().Nullable()
            .WithColumn("ACVR_VERS_NO").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("LH_ACC_NO").AsString().Nullable()
            .WithColumn("IAS_CUST_REF").AsString().Nullable()
            .WithColumn("CUT_OFF_DATE").AsString().Nullable()
            .WithColumn("CUT_OFF_IND").AsString().Nullable()
            .WithColumn("CREDIT_DEBIT_FACTOR").AsString().Nullable()
            .WithColumn("RETURNS_ACTUAL").AsString().Nullable()
            .WithColumn("BILLED_UPTO_DATE").AsString().Nullable()
            .WithColumn("NEW_OWNER_VERS").AsString().Nullable()
            .WithColumn("NEW_OWNER_YEAR").AsString().Nullable()
            .WithColumn("NEW_LIC_YEAR").AsString().Nullable()
            .WithColumn("BILLABLE_NOW").AsString().Nullable()
            .WithColumn("BILLABLE_NEXT").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("RL_SET").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,ABRN_FIN_YEAR,ABRN_BILL_RUN_NO,MODULE_NAME,ERROR_DATE,ERROR_TYPE,ERROR_MESSAGE,NMES_MESSAGE_NUMBER,RECORD_DETAILS,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_BILL_ERRORS").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("ABRN_FIN_YEAR").AsString().Nullable()
            .WithColumn("ABRN_BILL_RUN_NO").AsString().Nullable()
            .WithColumn("MODULE_NAME").AsString().Nullable()
            .WithColumn("ERROR_DATE").AsString().Nullable()
            .WithColumn("ERROR_TYPE").AsString().Nullable()
            .WithColumn("ERROR_MESSAGE").AsString().Nullable()
            .WithColumn("NMES_MESSAGE_NUMBER").AsString().Nullable()
            .WithColumn("RECORD_DETAILS").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,REGION_CODE,INCOME_TYPE,BILL_TYPE,ABRN_FIN_YEAR,ABRN_BILL_RUN_NO,FIN_YEAR,PREVIEW_NO,BILL_PRINT_IND,BILL_DATE,BILLABLE_IND,MIN_INV_OVERRIDE,LH_ACC_NO,IAS_CUST_REF,WRITTEN_LANG,LH_SURNAME,IAS_SURNAME,IAS_ADDR1,NEW_INV_FLAG,NEW_OWN_FLAG,BILL_NO,TPT_FLAG,MIN_CHARGE,NET_AMOUNT,VAT_AMOUNT,BILLED_AMOUNT,ABHD_ID,NOTES,NOTES_WELSH,ENQ_NAME,ENQ_TEL_NO,IAS_TITLE,LH_TITLE,IAS_INITIALS,LH_INITIALS,LH_FORENAME,IAS_FORENAME,IAS_ADDR2,IAS_ADDR3,IAS_ADDR4,IAS_TOWN,IAS_POSTCODE,IAS_COUNTY,IAS_COUNTRY,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_BILL_HEADERS").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("REGION_CODE").AsString().Nullable()
            .WithColumn("INCOME_TYPE").AsString().Nullable()
            .WithColumn("BILL_TYPE").AsString().Nullable()
            .WithColumn("ABRN_FIN_YEAR").AsString().Nullable()
            .WithColumn("ABRN_BILL_RUN_NO").AsString().Nullable()
            .WithColumn("FIN_YEAR").AsString().Nullable()
            .WithColumn("PREVIEW_NO").AsString().Nullable()
            .WithColumn("BILL_PRINT_IND").AsString().Nullable()
            .WithColumn("BILL_DATE").AsString().Nullable()
            .WithColumn("BILLABLE_IND").AsString().Nullable()
            .WithColumn("MIN_INV_OVERRIDE").AsString().Nullable()
            .WithColumn("LH_ACC_NO").AsString().Nullable()
            .WithColumn("IAS_CUST_REF").AsString().Nullable()
            .WithColumn("WRITTEN_LANG").AsString().Nullable()
            .WithColumn("LH_SURNAME").AsString().Nullable()
            .WithColumn("IAS_SURNAME").AsString().Nullable()
            .WithColumn("IAS_ADDR1").AsString().Nullable()
            .WithColumn("NEW_INV_FLAG").AsString().Nullable()
            .WithColumn("NEW_OWN_FLAG").AsString().Nullable()
            .WithColumn("BILL_NO").AsString().Nullable()
            .WithColumn("TPT_FLAG").AsString().Nullable()
            .WithColumn("MIN_CHARGE").AsString().Nullable()
            .WithColumn("NET_AMOUNT").AsString().Nullable()
            .WithColumn("VAT_AMOUNT").AsString().Nullable()
            .WithColumn("BILLED_AMOUNT").AsString().Nullable()
            .WithColumn("ABHD_ID").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("NOTES_WELSH").AsString().Nullable()
            .WithColumn("ENQ_NAME").AsString().Nullable()
            .WithColumn("ENQ_TEL_NO").AsString().Nullable()
            .WithColumn("IAS_TITLE").AsString().Nullable()
            .WithColumn("LH_TITLE").AsString().Nullable()
            .WithColumn("IAS_INITIALS").AsString().Nullable()
            .WithColumn("LH_INITIALS").AsString().Nullable()
            .WithColumn("LH_FORENAME").AsString().Nullable()
            .WithColumn("IAS_FORENAME").AsString().Nullable()
            .WithColumn("IAS_ADDR2").AsString().Nullable()
            .WithColumn("IAS_ADDR3").AsString().Nullable()
            .WithColumn("IAS_ADDR4").AsString().Nullable()
            .WithColumn("IAS_TOWN").AsString().Nullable()
            .WithColumn("IAS_POSTCODE").AsString().Nullable()
            .WithColumn("IAS_COUNTY").AsString().Nullable()
            .WithColumn("IAS_COUNTRY").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ABRN_FIN_YEAR,ABRN_BILL_RUN_NO,MODULE_NAME,START_DATE,STATUS,END_DATE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_BILL_PROCESSES").InSchema("nald")
            .WithColumn("ABRN_FIN_YEAR").AsString().Nullable()
            .WithColumn("ABRN_BILL_RUN_NO").AsString().Nullable()
            .WithColumn("MODULE_NAME").AsString().Nullable()
            .WithColumn("START_DATE").AsString().Nullable()
            .WithColumn("STATUS").AsString().Nullable()
            .WithColumn("END_DATE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // FIN_YEAR,BILL_RUN_NO,BILL_RUN_TYPE,BILL_DATE,INITIATOR,INITIATION_DATE,ENQ_NAME,ENQ_NAME_WELSH,ENQ_NO,ABORTED_RUN,BILL_RUN_STATUS_DATE,BILL_RUN_STATUS,INSTALL_BILL_DATE,INV_ST_NO,CRN_ST_NO,NO_OF_INVS,NO_OF_CRNS,VALUE_OF_INVS,VALUE_OF_CRNS,ABORTEE,ABORT_REASON,CONFIRMEE,IAS_XFER_DATE,NOTES,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_BILL_RUNS").InSchema("nald")
            .WithColumn("FIN_YEAR").AsString().Nullable()
            .WithColumn("BILL_RUN_NO").AsString().Nullable()
            .WithColumn("BILL_RUN_TYPE").AsString().Nullable()
            .WithColumn("BILL_DATE").AsString().Nullable()
            .WithColumn("INITIATOR").AsString().Nullable()
            .WithColumn("INITIATION_DATE").AsString().Nullable()
            .WithColumn("ENQ_NAME").AsString().Nullable()
            .WithColumn("ENQ_NAME_WELSH").AsString().Nullable()
            .WithColumn("ENQ_NO").AsString().Nullable()
            .WithColumn("ABORTED_RUN").AsString().Nullable()
            .WithColumn("BILL_RUN_STATUS_DATE").AsString().Nullable()
            .WithColumn("BILL_RUN_STATUS").AsString().Nullable()
            .WithColumn("INSTALL_BILL_DATE").AsString().Nullable()
            .WithColumn("INV_ST_NO").AsString().Nullable()
            .WithColumn("CRN_ST_NO").AsString().Nullable()
            .WithColumn("NO_OF_INVS").AsString().Nullable()
            .WithColumn("NO_OF_CRNS").AsString().Nullable()
            .WithColumn("VALUE_OF_INVS").AsString().Nullable()
            .WithColumn("VALUE_OF_CRNS").AsString().Nullable()
            .WithColumn("ABORTEE").AsString().Nullable()
            .WithColumn("ABORT_REASON").AsString().Nullable()
            .WithColumn("CONFIRMEE").AsString().Nullable()
            .WithColumn("IAS_XFER_DATE").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ABRN_FIN_YEAR,ABRN_BILL_RUN_NO,ACEL_ID,FIN_YEAR,LATEST_RET_DATE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_BILL_TPT_RETURNS").InSchema("nald")
            .WithColumn("ABRN_FIN_YEAR").AsString().Nullable()
            .WithColumn("ABRN_BILL_RUN_NO").AsString().Nullable()
            .WithColumn("ACEL_ID").AsString().Nullable()
            .WithColumn("FIN_YEAR").AsString().Nullable()
            .WithColumn("LATEST_RET_DATE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,TRANS_TYPE,ABRN_FIN_YEAR,ABRN_BILL_RUN_NO,FIN_YEAR,NET_AMOUNT,VAT_AMOUNT,BILLABLE_ANN_QTY,LIC_ID,VERS_NO,LH_ACC_NO,IAS_CUST_REF,ACEL_ID,SRCE_CODE,SEAS_CODE,LOSS_CODE,SRCE_VALUE,SEAS_VALUE,LOSS_VALUE,VAT_CODE,SUC_CODE,VAT_RATE,SUC_RATE,BILL_ST_DATE,BILL_END_DATE,RETURNS_ACTUAL,ABS_PER_DAYS,BILLABLE_DAYS,AWAITING_BILL_HEADER,ABHD_ID,NEW_INV_FLAG,NEW_OWN_FLAG,TPT_FLAG,ELEMENT_AGRMNTS,LH_ACC_AGRMNTS,ELEMENT_AGRMNT_VALS,LH_ACC_AGRMNTS_VALS,TRANS_DESCR,FINAL_A1_BILLABLE_AMOUNT,FINAL_A2_BILLABLE_AMOUNT,EIUC_SRCE_VALUE,EIUC_VALUE,TLP_VALUE,FGAC_REGION_CODE,EIUC_ELEMENT_AGRMNT_VALS,EIUC_2PT_VALUE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_BILL_TRANS").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("TRANS_TYPE").AsString().Nullable()
            .WithColumn("ABRN_FIN_YEAR").AsString().Nullable()
            .WithColumn("ABRN_BILL_RUN_NO").AsString().Nullable()
            .WithColumn("FIN_YEAR").AsString().Nullable()
            .WithColumn("NET_AMOUNT").AsString().Nullable()
            .WithColumn("VAT_AMOUNT").AsString().Nullable()
            .WithColumn("BILLABLE_ANN_QTY").AsString().Nullable()
            .WithColumn("LIC_ID").AsString().Nullable()
            .WithColumn("VERS_NO").AsString().Nullable()
            .WithColumn("LH_ACC_NO").AsString().Nullable()
            .WithColumn("IAS_CUST_REF").AsString().Nullable()
            .WithColumn("ACEL_ID").AsString().Nullable()
            .WithColumn("SRCE_CODE").AsString().Nullable()
            .WithColumn("SEAS_CODE").AsString().Nullable()
            .WithColumn("LOSS_CODE").AsString().Nullable()
            .WithColumn("SRCE_VALUE").AsString().Nullable()
            .WithColumn("SEAS_VALUE").AsString().Nullable()
            .WithColumn("LOSS_VALUE").AsString().Nullable()
            .WithColumn("VAT_CODE").AsString().Nullable()
            .WithColumn("SUC_CODE").AsString().Nullable()
            .WithColumn("VAT_RATE").AsString().Nullable()
            .WithColumn("SUC_RATE").AsString().Nullable()
            .WithColumn("BILL_ST_DATE").AsString().Nullable()
            .WithColumn("BILL_END_DATE").AsString().Nullable()
            .WithColumn("RETURNS_ACTUAL").AsString().Nullable()
            .WithColumn("ABS_PER_DAYS").AsString().Nullable()
            .WithColumn("BILLABLE_DAYS").AsString().Nullable()
            .WithColumn("AWAITING_BILL_HEADER").AsString().Nullable()
            .WithColumn("ABHD_ID").AsString().Nullable()
            .WithColumn("NEW_INV_FLAG").AsString().Nullable()
            .WithColumn("NEW_OWN_FLAG").AsString().Nullable()
            .WithColumn("TPT_FLAG").AsString().Nullable()
            .WithColumn("ELEMENT_AGRMNTS").AsString().Nullable()
            .WithColumn("LH_ACC_AGRMNTS").AsString().Nullable()
            .WithColumn("ELEMENT_AGRMNT_VALS").AsString().Nullable()
            .WithColumn("LH_ACC_AGRMNTS_VALS").AsString().Nullable()
            .WithColumn("TRANS_DESCR").AsString().Nullable()
            .WithColumn("FINAL_A1_BILLABLE_AMOUNT").AsString().Nullable()
            .WithColumn("FINAL_A2_BILLABLE_AMOUNT").AsString().Nullable()
            .WithColumn("EIUC_SRCE_VALUE").AsString().Nullable()
            .WithColumn("EIUC_VALUE").AsString().Nullable()
            .WithColumn("TLP_VALUE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("EIUC_ELEMENT_AGRMNT_VALS").AsString().Nullable()
            .WithColumn("EIUC_2PT_VALUE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ABCV_ABRN_FIN_YEAR,ABCV_ABRN_BILL_RUN_NO,ABCV_ACVR_AABL_ID,ABCV_ACVR_VERS_NO,FIN_YEAR,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_BILL_YEARS").InSchema("nald")
            .WithColumn("ABCV_ABRN_FIN_YEAR").AsString().Nullable()
            .WithColumn("ABCV_ABRN_BILL_RUN_NO").AsString().Nullable()
            .WithColumn("ABCV_ACVR_AABL_ID").AsString().Nullable()
            .WithColumn("ABCV_ACVR_VERS_NO").AsString().Nullable()
            .WithColumn("FIN_YEAR").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // BUTTON_NUMBER,BUTTON_TYPE,BUTTON_LABEL,BUTTON_ICON,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_BUTTONS").InSchema("nald")
            .WithColumn("BUTTON_NUMBER").AsString().Nullable()
            .WithColumn("BUTTON_TYPE").AsString().Nullable()
            .WithColumn("BUTTON_LABEL").AsString().Nullable()
            .WithColumn("BUTTON_ICON").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ACEL_ID,AFSA_CODE,EFF_ST_DATE,EFF_END_DATE,SIGNED_DATE,FILE_REF,TEXT,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_CHG_AGRMNTS").InSchema("nald")
            .WithColumn("ACEL_ID").AsString().Nullable()
            .WithColumn("AFSA_CODE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("SIGNED_DATE").AsString().Nullable()
            .WithColumn("FILE_REF").AsString().Nullable()
            .WithColumn("TEXT").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,ACVR_AABL_ID,ACVR_VERS_NO,ABS_PERIOD_ST_DAY,ABS_PERIOD_ST_MONTH,ABS_PERIOD_END_DAY,ABS_PERIOD_END_MONTH,AUTH_ANN_QTY,ASFT_CODE,ASFT_CODE_DERIVED,ASRF_CODE,ALSF_CODE,APUR_APPR_CODE,APUR_APSE_CODE,APUR_APUS_CODE,FCTS_OVERRIDDEN,DISP_ORD,BILLABLE_ANN_QTY,TIMELTD_ST_DATE,TIMELTD_END_DATE,DESCR,DESCR_WELSH,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_CHG_ELEMENTS").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("ACVR_AABL_ID").AsString().Nullable()
            .WithColumn("ACVR_VERS_NO").AsString().Nullable()
            .WithColumn("ABS_PERIOD_ST_DAY").AsString().Nullable()
            .WithColumn("ABS_PERIOD_ST_MONTH").AsString().Nullable()
            .WithColumn("ABS_PERIOD_END_DAY").AsString().Nullable()
            .WithColumn("ABS_PERIOD_END_MONTH").AsString().Nullable()
            .WithColumn("AUTH_ANN_QTY").AsString().Nullable()
            .WithColumn("ASFT_CODE").AsString().Nullable()
            .WithColumn("ASFT_CODE_DERIVED").AsString().Nullable()
            .WithColumn("ASRF_CODE").AsString().Nullable()
            .WithColumn("ALSF_CODE").AsString().Nullable()
            .WithColumn("APUR_APPR_CODE").AsString().Nullable()
            .WithColumn("APUR_APSE_CODE").AsString().Nullable()
            .WithColumn("APUR_APUS_CODE").AsString().Nullable()
            .WithColumn("FCTS_OVERRIDDEN").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("BILLABLE_ANN_QTY").AsString().Nullable()
            .WithColumn("TIMELTD_ST_DATE").AsString().Nullable()
            .WithColumn("TIMELTD_END_DATE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DESCR_WELSH").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AABL_ID,VERS_NO,EFF_ST_DATE,STATUS,APPORTIONMENT,IN_ERROR_STATUS,AIIA_ALHA_ACC_NO,AIIA_IAS_CUST_REF,EFF_END_DATE,NEW_OWNER_VERS,NEW_OWNER_YEAR,NEW_LIC_YEAR,BILLED_UPTO_DATE,TO_BE_BILLED,TLPA_STATUS,FGAC_REGION_CODE,RL_FINAL,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_CHG_VERSIONS").InSchema("nald")
            .WithColumn("AABL_ID").AsString().Nullable()
            .WithColumn("VERS_NO").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("STATUS").AsString().Nullable()
            .WithColumn("APPORTIONMENT").AsString().Nullable()
            .WithColumn("IN_ERROR_STATUS").AsString().Nullable()
            .WithColumn("AIIA_ALHA_ACC_NO").AsString().Nullable()
            .WithColumn("AIIA_IAS_CUST_REF").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("NEW_OWNER_VERS").AsString().Nullable()
            .WithColumn("NEW_OWNER_YEAR").AsString().Nullable()
            .WithColumn("NEW_LIC_YEAR").AsString().Nullable()
            .WithColumn("BILLED_UPTO_DATE").AsString().Nullable()
            .WithColumn("TO_BE_BILLED").AsString().Nullable()
            .WithColumn("TLPA_STATUS").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("RL_FINAL").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CC_DOMAIN,CC_COMMENT,CC_NEXT_VALUE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_CODE_CONTROLS").InSchema("nald")
            .WithColumn("CC_DOMAIN").AsString().Nullable()
            .WithColumn("CC_COMMENT").AsString().Nullable()
            .WithColumn("CC_NEXT_VALUE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // APAR_ID,AADD_ID,DISABLED,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_CONTACTS").InSchema("nald")
            .WithColumn("APAR_ID").AsString().Nullable()
            .WithColumn("AADD_ID").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ACON_APAR_ID,ACON_AADD_ID,ACNT_CODE,CONT_NO,DISP_ORD,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_CONT_NOS").InSchema("nald")
            .WithColumn("ACON_APAR_ID").AsString().Nullable()
            .WithColumn("ACON_AADD_ID").AsString().Nullable()
            .WithColumn("ACNT_CODE").AsString().Nullable()
            .WithColumn("CONT_NO").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_CONT_NO_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_CRIT_CLASSES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AMAN_CODE,SEQ_NO,VALUE,ST_DAY,ST_MONTH,END_DAY,END_MONTH,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_CTRL_FLOWS").InSchema("nald")
            .WithColumn("AMAN_CODE").AsString().Nullable()
            .WithColumn("SEQ_NO").AsString().Nullable()
            .WithColumn("VALUE").AsString().Nullable()
            .WithColumn("ST_DAY").AsString().Nullable()
            .WithColumn("ST_MONTH").AsString().Nullable()
            .WithColumn("END_DAY").AsString().Nullable()
            .WithColumn("END_MONTH").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AMAN_CODE,SEQ_NO,VALUE,DATUM_TYPE,ST_DAY,ST_MONTH,END_DAY,END_MONTH,LOCAL_REF,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_CTRL_LEVELS").InSchema("nald")
            .WithColumn("AMAN_CODE").AsString().Nullable()
            .WithColumn("SEQ_NO").AsString().Nullable()
            .WithColumn("VALUE").AsString().Nullable()
            .WithColumn("DATUM_TYPE").AsString().Nullable()
            .WithColumn("ST_DAY").AsString().Nullable()
            .WithColumn("ST_MONTH").AsString().Nullable()
            .WithColumn("END_DAY").AsString().Nullable()
            .WithColumn("END_MONTH").AsString().Nullable()
            .WithColumn("LOCAL_REF").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_CTRL_POINT_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_DEREG_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,DOC_REF,AABL_ID,AIMP_ID,DOC_FROM_DATE,DOC_TO_DATE,EXT_LOC_DESCR,TEXT,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_DOCUMENT_REFS").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("DOC_REF").AsString().Nullable()
            .WithColumn("AABL_ID").AsString().Nullable()
            .WithColumn("AIMP_ID").AsString().Nullable()
            .WithColumn("DOC_FROM_DATE").AsString().Nullable()
            .WithColumn("DOC_TO_DATE").AsString().Nullable()
            .WithColumn("EXT_LOC_DESCR").AsString().Nullable()
            .WithColumn("TEXT").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AREP_CODE,EFF_ST_DATE,EIUC_VALUE,EFF_END_DATE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_EIUC_VALS").InSchema("nald")
            .WithColumn("AREP_CODE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("EIUC_VALUE").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,LEVEL_APPLIED,USED_BY_SYS,AFFECTS_INVS,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_FIN_AGRMNT_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("LEVEL_APPLIED").AsString().Nullable()
            .WithColumn("USED_BY_SYS").AsString().Nullable()
            .WithColumn("AFFECTS_INVS").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AFSA_CODE,EFF_ST_DATE,ADJ_FCT,COMP_VALUE,COMP_DAY,COMP_MONTH,EFF_END_DATE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_FIN_AGRMNT_VALS").InSchema("nald")
            .WithColumn("AFSA_CODE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("ADJ_FCT").AsString().Nullable()
            .WithColumn("COMP_VALUE").AsString().Nullable()
            .WithColumn("COMP_DAY").AsString().Nullable()
            .WithColumn("COMP_MONTH").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // HLP_APPLN,HLP_INDEX,HLP_MODTAB_NAME,HLP_GENERATED,HLP_SEQ,HLP_TEXT,HLP_TYPE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_FORM_HELP").InSchema("nald")
            .WithColumn("HLP_APPLN").AsString().Nullable()
            .WithColumn("HLP_INDEX").AsString().Nullable()
            .WithColumn("HLP_MODTAB_NAME").AsString().Nullable()
            .WithColumn("HLP_GENERATED").AsString().Nullable()
            .WithColumn("HLP_SEQ").AsString().Nullable()
            .WithColumn("HLP_TEXT").AsString().Nullable()
            .WithColumn("HLP_TYPE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ACC_NO,ACON_APAR_ID,ACON_AADD_ID,NOTES,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_GROUP_LH_ACCS").InSchema("nald")
            .WithColumn("ACC_NO").AsString().Nullable()
            .WithColumn("ACON_APAR_ID").AsString().Nullable()
            .WithColumn("ACON_AADD_ID").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ALHA_ACC_NO,IAS_CUST_REF,DISABLED,ACON_APAR_ID,ACON_AADD_ID,IAS_XFER_DATE,DISP_ORD,NOTES,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_IAS_INVOICE_ACCS").InSchema("nald")
            .WithColumn("ALHA_ACC_NO").AsString().Nullable()
            .WithColumn("IAS_CUST_REF").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("ACON_APAR_ID").AsString().Nullable()
            .WithColumn("ACON_AADD_ID").AsString().Nullable()
            .WithColumn("IAS_XFER_DATE").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,LIC_NO,ORIG_SIG_DATE,ORIG_EFF_DATE,ORIG_APP_NO,TERM_DATE,NOTES,AREA,LEAP,CAMS,RETRO_STR,X_REG_IND,REV_DATE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_IMP_LICENCES").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("LIC_NO").AsString().Nullable()
            .WithColumn("ORIG_SIG_DATE").AsString().Nullable()
            .WithColumn("ORIG_EFF_DATE").AsString().Nullable()
            .WithColumn("ORIG_APP_NO").AsString().Nullable()
            .WithColumn("TERM_DATE").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("AREA").AsString().Nullable()
            .WithColumn("LEAP").AsString().Nullable()
            .WithColumn("CAMS").AsString().Nullable()
            .WithColumn("RETRO_STR").AsString().Nullable()
            .WithColumn("X_REG_IND").AsString().Nullable()
            .WithColumn("REV_DATE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,AIMV_AIMP_ID,AIMV_ISSUE_NO,AIMV_INCR_NO,APUR_APPR_CODE,APUR_APSE_CODE,APUR_APUS_CODE,PERIOD_ST_DAY,PERIOD_ST_MONTH,PERIOD_END_DAY,PERIOD_END_MONTH,AMOI_CODE,CONST_ST_BY_DATE,CONST_END_BY_DATE,WORKS_ST_DATE,WORKS_COMPL_DATE,MAX_VOL,MAX_VOL_USABILITY,SURFACE_AREA,AISI_CODE,SPLWAY_LEVEL,SPLWAY_DATUM,SPLWAY_REF,OVFLOW_LEVEL,OVFLOW_DATUM,OVFLOW_REF,RSVOIR_ACT,RSVOIR_ACT_TEXT,LANDS_IMP,DISP_ORD,NOTES,CEASED_DATE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_IMP_LIC_PURPOSES").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("AIMV_AIMP_ID").AsString().Nullable()
            .WithColumn("AIMV_ISSUE_NO").AsString().Nullable()
            .WithColumn("AIMV_INCR_NO").AsString().Nullable()
            .WithColumn("APUR_APPR_CODE").AsString().Nullable()
            .WithColumn("APUR_APSE_CODE").AsString().Nullable()
            .WithColumn("APUR_APUS_CODE").AsString().Nullable()
            .WithColumn("PERIOD_ST_DAY").AsString().Nullable()
            .WithColumn("PERIOD_ST_MONTH").AsString().Nullable()
            .WithColumn("PERIOD_END_DAY").AsString().Nullable()
            .WithColumn("PERIOD_END_MONTH").AsString().Nullable()
            .WithColumn("AMOI_CODE").AsString().Nullable()
            .WithColumn("CONST_ST_BY_DATE").AsString().Nullable()
            .WithColumn("CONST_END_BY_DATE").AsString().Nullable()
            .WithColumn("WORKS_ST_DATE").AsString().Nullable()
            .WithColumn("WORKS_COMPL_DATE").AsString().Nullable()
            .WithColumn("MAX_VOL").AsString().Nullable()
            .WithColumn("MAX_VOL_USABILITY").AsString().Nullable()
            .WithColumn("SURFACE_AREA").AsString().Nullable()
            .WithColumn("AISI_CODE").AsString().Nullable()
            .WithColumn("SPLWAY_LEVEL").AsString().Nullable()
            .WithColumn("SPLWAY_DATUM").AsString().Nullable()
            .WithColumn("SPLWAY_REF").AsString().Nullable()
            .WithColumn("OVFLOW_LEVEL").AsString().Nullable()
            .WithColumn("OVFLOW_DATUM").AsString().Nullable()
            .WithColumn("OVFLOW_REF").AsString().Nullable()
            .WithColumn("RSVOIR_ACT").AsString().Nullable()
            .WithColumn("RSVOIR_ACT_TEXT").AsString().Nullable()
            .WithColumn("LANDS_IMP").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("CEASED_DATE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AIMP_ID,ISSUE_NO,INCR_NO,AIMV_TYPE,EFF_ST_DATE,STATUS,ASRC_CODE,ACCL_CODE,LIC_SIG_DATE,LIC_DOC_FLAG,APP_NO,EFF_END_DATE,ACON_APAR_ID,ACON_AADD_ID,MULTIPLE_LH,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_IMP_LIC_VERSIONS").InSchema("nald")
            .WithColumn("AIMP_ID").AsString().Nullable()
            .WithColumn("ISSUE_NO").AsString().Nullable()
            .WithColumn("INCR_NO").AsString().Nullable()
            .WithColumn("AIMV_TYPE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("STATUS").AsString().Nullable()
            .WithColumn("ASRC_CODE").AsString().Nullable()
            .WithColumn("ACCL_CODE").AsString().Nullable()
            .WithColumn("LIC_SIG_DATE").AsString().Nullable()
            .WithColumn("LIC_DOC_FLAG").AsString().Nullable()
            .WithColumn("APP_NO").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("ACON_APAR_ID").AsString().Nullable()
            .WithColumn("ACON_AADD_ID").AsString().Nullable()
            .WithColumn("MULTIPLE_LH").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AIPU_ID,AAIP_ID,NOTES,IMOI_CODE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_IMP_PURP_POINTS").InSchema("nald")
            .WithColumn("AIPU_ID").AsString().Nullable()
            .WithColumn("AAIP_ID").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("IMOI_CODE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_IMP_SITE_STATUSES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ACC_NO,DISABLED,SUSP_FROM_BILLING,ACON_APAR_ID,ACON_AADD_ID,AGCA_ACC_NO,NOTES,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LH_ACCS").InSchema("nald")
            .WithColumn("ACC_NO").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("SUSP_FROM_BILLING").AsString().Nullable()
            .WithColumn("ACON_APAR_ID").AsString().Nullable()
            .WithColumn("ACON_AADD_ID").AsString().Nullable()
            .WithColumn("AGCA_ACC_NO").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ALHA_ACC_NO,AFSA_CODE,EFF_ST_DATE,EFF_END_DATE,SIGNED_DATE,FILE_REF,TEXT,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LH_AGRMNTS").InSchema("nald")
            .WithColumn("ALHA_ACC_NO").AsString().Nullable()
            .WithColumn("AFSA_CODE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("SIGNED_DATE").AsString().Nullable()
            .WithColumn("FILE_REF").AsString().Nullable()
            .WithColumn("TEXT").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LH_REC_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ALHA_ACC_NO,CREATE_DATE,USER_ID,EVENT,AMRE_AMRE_TYPE,AMRE_CODE,TEXT,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LH_SUSP_LOGS").InSchema("nald")
            .WithColumn("ALHA_ACC_NO").AsString().Nullable()
            .WithColumn("CREATE_DATE").AsString().Nullable()
            .WithColumn("USER_ID").AsString().Nullable()
            .WithColumn("EVENT").AsString().Nullable()
            .WithColumn("AMRE_AMRE_TYPE").AsString().Nullable()
            .WithColumn("AMRE_CODE").AsString().Nullable()
            .WithColumn("TEXT").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,ALSA_CODE,EFF_ST_DATE,AABP_ID,AIPU_ID,EFF_END_DATE,TEXT,SIGNED_DATE,FILE_REF,DISP_ORD,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LIC_AGRMNTS").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("ALSA_CODE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("AABP_ID").AsString().Nullable()
            .WithColumn("AIPU_ID").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("TEXT").AsString().Nullable()
            .WithColumn("SIGNED_DATE").AsString().Nullable()
            .WithColumn("FILE_REF").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,AFFECTS_ABS,AFFECTS_IMP,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LIC_AGRMNT_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("AFFECTS_ABS").AsString().Nullable()
            .WithColumn("AFFECTS_IMP").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LIC_AVAILS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,ACIN_CODE,ACIN_SUBCODE,AABP_ID,AIPU_ID,PARAM1,PARAM2,DISP_ORD,TEXT,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LIC_CONDITIONS").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("ACIN_CODE").AsString().Nullable()
            .WithColumn("ACIN_SUBCODE").AsString().Nullable()
            .WithColumn("AABP_ID").AsString().Nullable()
            .WithColumn("AIPU_ID").AsString().Nullable()
            .WithColumn("PARAM1").AsString().Nullable()
            .WithColumn("PARAM2").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("TEXT").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,SUBCODE,DESCR,SUBCODE_DESC,AFFECTS_ABS,AFFECTS_IMP,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LIC_COND_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("SUBCODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("SUBCODE_DESC").AsString().Nullable()
            .WithColumn("AFFECTS_ABS").AsString().Nullable()
            .WithColumn("AFFECTS_IMP").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,ALRT_CODE,ACON_APAR_ID,ACON_AADD_ID,EFF_ST_DATE,AABL_ID,AIMP_ID,EFF_END_DATE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LIC_ROLES").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("ALRT_CODE").AsString().Nullable()
            .WithColumn("ACON_APAR_ID").AsString().Nullable()
            .WithColumn("ACON_AADD_ID").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("AABL_ID").AsString().Nullable()
            .WithColumn("AIMP_ID").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,AFFECTS_ABS,AFFECTS_IMP,CUST_AGENCY,USED_BY_SYS,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LIC_ROLE_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("AFFECTS_ABS").AsString().Nullable()
            .WithColumn("AFFECTS_IMP").AsString().Nullable()
            .WithColumn("CUST_AGENCY").AsString().Nullable()
            .WithColumn("USED_BY_SYS").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LIC_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LOSS_FACTORS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ALSF_CODE,EFF_ST_DATE,VALUE,EFF_END_DATE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_LOSS_FACTOR_VALS").InSchema("nald")
            .WithColumn("ALSF_CODE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("VALUE").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,USER_ID,REPORT_DATETIME,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_MAN_REP_CODES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("USER_ID").AsString().Nullable()
            .WithColumn("REPORT_DATETIME").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,NAME,UNIT_TYPE,NGR_SHEET,NGR_EAST,NGR_NORTH,CART_EAST,CART_NORTH,APTY_CODE,DISABLED,THEO_GROSS_AVG_RES,LIC_AVG_RES,THEO_GROSS_PEAK_RES,LIC_PEAK_RES,AMLA_CODE,APFR_CODE,ASLA_CODE,ATLL_CODE,LIC_STATUS_TEXT,CTRL_PT_NAME,CTRL_PT_NGR_SHEET,CTRL_PT_NGR_EAST,CTRL_PT_NGR_NORTH,CTRL_PT_CART_EAST,CTRL_PT_CART_NORTH,CTRL_PT_REASON_TEXT,AMAN_CODE,NOTES,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_MAN_UNITS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("NAME").AsString().Nullable()
            .WithColumn("UNIT_TYPE").AsString().Nullable()
            .WithColumn("NGR_SHEET").AsString().Nullable()
            .WithColumn("NGR_EAST").AsString().Nullable()
            .WithColumn("NGR_NORTH").AsString().Nullable()
            .WithColumn("CART_EAST").AsString().Nullable()
            .WithColumn("CART_NORTH").AsString().Nullable()
            .WithColumn("APTY_CODE").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("THEO_GROSS_AVG_RES").AsString().Nullable()
            .WithColumn("LIC_AVG_RES").AsString().Nullable()
            .WithColumn("THEO_GROSS_PEAK_RES").AsString().Nullable()
            .WithColumn("LIC_PEAK_RES").AsString().Nullable()
            .WithColumn("AMLA_CODE").AsString().Nullable()
            .WithColumn("APFR_CODE").AsString().Nullable()
            .WithColumn("ASLA_CODE").AsString().Nullable()
            .WithColumn("ATLL_CODE").AsString().Nullable()
            .WithColumn("LIC_STATUS_TEXT").AsString().Nullable()
            .WithColumn("CTRL_PT_NAME").AsString().Nullable()
            .WithColumn("CTRL_PT_NGR_SHEET").AsString().Nullable()
            .WithColumn("CTRL_PT_NGR_EAST").AsString().Nullable()
            .WithColumn("CTRL_PT_NGR_NORTH").AsString().Nullable()
            .WithColumn("CTRL_PT_CART_EAST").AsString().Nullable()
            .WithColumn("CTRL_PT_CART_NORTH").AsString().Nullable()
            .WithColumn("CTRL_PT_REASON_TEXT").AsString().Nullable()
            .WithColumn("AMAN_CODE").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AAIP_ID,AMAN_CODE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_MAN_UNIT_POINTS").InSchema("nald")
            .WithColumn("AAIP_ID").AsString().Nullable()
            .WithColumn("AMAN_CODE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,NOTES,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_MEANS_OF_ABS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,NOTES,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_MEANS_OF_IMP").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,NOTES,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_MEANS_OF_MEASURE").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // MESSAGE_NUMBER,MESSAGE_TEXT,REFERENCED_BY,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_MESSAGES").InSchema("nald")
            .WithColumn("MESSAGE_NUMBER").AsString().Nullable()
            .WithColumn("MESSAGE_TEXT").AsString().Nullable()
            .WithColumn("REFERENCED_BY").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,CREATE_DATE,USER_ID,EVENT,AABL_ID,AIMP_ID,AMRE_AMRE_TYPE,AMRE_CODE,AABV_AABL_ID,AABV_ISSUE_NO,AABV_INCR_NO,ARVN_AABL_ID,ARVN_VERS_NO,ACVR_AABL_ID,ACVR_VERS_NO,AIMV_AIMP_ID,AIMV_ISSUE_NO,AIMV_INCR_NO,TEXT,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_MOD_LOGS").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("CREATE_DATE").AsString().Nullable()
            .WithColumn("USER_ID").AsString().Nullable()
            .WithColumn("EVENT").AsString().Nullable()
            .WithColumn("AABL_ID").AsString().Nullable()
            .WithColumn("AIMP_ID").AsString().Nullable()
            .WithColumn("AMRE_AMRE_TYPE").AsString().Nullable()
            .WithColumn("AMRE_CODE").AsString().Nullable()
            .WithColumn("AABV_AABL_ID").AsString().Nullable()
            .WithColumn("AABV_ISSUE_NO").AsString().Nullable()
            .WithColumn("AABV_INCR_NO").AsString().Nullable()
            .WithColumn("ARVN_AABL_ID").AsString().Nullable()
            .WithColumn("ARVN_VERS_NO").AsString().Nullable()
            .WithColumn("ACVR_AABL_ID").AsString().Nullable()
            .WithColumn("ACVR_VERS_NO").AsString().Nullable()
            .WithColumn("AIMV_AIMP_ID").AsString().Nullable()
            .WithColumn("AIMV_ISSUE_NO").AsString().Nullable()
            .WithColumn("AIMV_INCR_NO").AsString().Nullable()
            .WithColumn("TEXT").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AMRE_TYPE,CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_MOD_REASONS").InSchema("nald")
            .WithColumn("AMRE_TYPE").AsString().Nullable()
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // NGR_SHEET,CART_EAST_PREFIX,CART_NORTH_PREFIX,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_NGR_CONVERSIONS").InSchema("nald")
            .WithColumn("NGR_SHEET").AsString().Nullable()
            .WithColumn("CART_EAST_PREFIX").AsString().Nullable()
            .WithColumn("CART_NORTH_PREFIX").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,APAR_TYPE,NAME,SPOKEN_LANG,WRITTEN_LANG,LAST_CHANGED,DISABLED,FORENAME,INITIALS,SALUTATION,REF,DESCR,LOCAL_NAME,ASIC_ASID_DIVISION,ASIC_CLASS,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_PARTIES").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("APAR_TYPE").AsString().Nullable()
            .WithColumn("NAME").AsString().Nullable()
            .WithColumn("SPOKEN_LANG").AsString().Nullable()
            .WithColumn("WRITTEN_LANG").AsString().Nullable()
            .WithColumn("LAST_CHANGED").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("FORENAME").AsString().Nullable()
            .WithColumn("INITIALS").AsString().Nullable()
            .WithColumn("SALUTATION").AsString().Nullable()
            .WithColumn("REF").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("LOCAL_NAME").AsString().Nullable()
            .WithColumn("ASIC_ASID_DIVISION").AsString().Nullable()
            .WithColumn("ASIC_CLASS").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,NGR1_SHEET,NGR1_EAST,NGR1_NORTH,CART1_EAST,CART1_NORTH,LOCAL_NAME,ASRC_CODE,DISABLED,LOCAL_NAME_WELSH,NGR2_SHEET,NGR2_EAST,NGR2_NORTH,CART2_EAST,CART2_NORTH,NGR3_SHEET,NGR3_EAST,NGR3_NORTH,CART3_EAST,CART3_NORTH,NGR4_SHEET,NGR4_EAST,NGR4_NORTH,CART4_EAST,CART4_NORTH,AAPC_CODE,AAPT_APTP_CODE,AAPT_APTS_CODE,ABAN_CODE,LOCATION_TEXT,AADD_ID,DEPTH,WRB_NO,BGS_NO,REG_WELL_INDEX_REF,HYDRO_REF,HYDRO_INTERCEPT_DIST,HYDRO_GW_OFFSET_DIST,NOTES,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_POINTS").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("NGR1_SHEET").AsString().Nullable()
            .WithColumn("NGR1_EAST").AsString().Nullable()
            .WithColumn("NGR1_NORTH").AsString().Nullable()
            .WithColumn("CART1_EAST").AsString().Nullable()
            .WithColumn("CART1_NORTH").AsString().Nullable()
            .WithColumn("LOCAL_NAME").AsString().Nullable()
            .WithColumn("ASRC_CODE").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("LOCAL_NAME_WELSH").AsString().Nullable()
            .WithColumn("NGR2_SHEET").AsString().Nullable()
            .WithColumn("NGR2_EAST").AsString().Nullable()
            .WithColumn("NGR2_NORTH").AsString().Nullable()
            .WithColumn("CART2_EAST").AsString().Nullable()
            .WithColumn("CART2_NORTH").AsString().Nullable()
            .WithColumn("NGR3_SHEET").AsString().Nullable()
            .WithColumn("NGR3_EAST").AsString().Nullable()
            .WithColumn("NGR3_NORTH").AsString().Nullable()
            .WithColumn("CART3_EAST").AsString().Nullable()
            .WithColumn("CART3_NORTH").AsString().Nullable()
            .WithColumn("NGR4_SHEET").AsString().Nullable()
            .WithColumn("NGR4_EAST").AsString().Nullable()
            .WithColumn("NGR4_NORTH").AsString().Nullable()
            .WithColumn("CART4_EAST").AsString().Nullable()
            .WithColumn("CART4_NORTH").AsString().Nullable()
            .WithColumn("AAPC_CODE").AsString().Nullable()
            .WithColumn("AAPT_APTP_CODE").AsString().Nullable()
            .WithColumn("AAPT_APTS_CODE").AsString().Nullable()
            .WithColumn("ABAN_CODE").AsString().Nullable()
            .WithColumn("LOCATION_TEXT").AsString().Nullable()
            .WithColumn("AADD_ID").AsString().Nullable()
            .WithColumn("DEPTH").AsString().Nullable()
            .WithColumn("WRB_NO").AsString().Nullable()
            .WithColumn("BGS_NO").AsString().Nullable()
            .WithColumn("REG_WELL_INDEX_REF").AsString().Nullable()
            .WithColumn("HYDRO_REF").AsString().Nullable()
            .WithColumn("HYDRO_INTERCEPT_DIST").AsString().Nullable()
            .WithColumn("HYDRO_GW_OFFSET_DIST").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_POINT_CATEGORIES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // APTP_CODE,APTS_CODE,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_POINT_TYPES").InSchema("nald")
            .WithColumn("APTP_CODE").AsString().Nullable()
            .WithColumn("APTS_CODE").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_POINT_TYPE_PRIMS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_POINT_TYPE_SECS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,NAME,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_POSTAL_COUNTIES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("NAME").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_PRES_FLOW_RESTS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // NAME,DESCR,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_PRINTER_DRIVERS").InSchema("nald")
            .WithColumn("NAME").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,BUS_FUNC_AREA,MODULE_NAME,ST_DATETIME,STATUS,END_DATETIME,NMES_MESSAGE_NUMBER,RECORD_DETAILS,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_PROC_DETAILS").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("BUS_FUNC_AREA").AsString().Nullable()
            .WithColumn("MODULE_NAME").AsString().Nullable()
            .WithColumn("ST_DATETIME").AsString().Nullable()
            .WithColumn("STATUS").AsString().Nullable()
            .WithColumn("END_DATETIME").AsString().Nullable()
            .WithColumn("NMES_MESSAGE_NUMBER").AsString().Nullable()
            .WithColumn("RECORD_DETAILS").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // APPR_CODE,APSE_CODE,APUS_CODE,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_PURPOSES").InSchema("nald")
            .WithColumn("APPR_CODE").AsString().Nullable()
            .WithColumn("APSE_CODE").AsString().Nullable()
            .WithColumn("APUS_CODE").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_PURP_PRIMS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_PURP_SECS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,ALSF_CODE,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_PURP_USES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("ALSF_CODE").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // RV_LOW_VALUE,RV_HIGH_VALUE,RV_ABBREVIATION,RV_DOMAIN,RV_MEANING,RV_TYPE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_REF_CODES").InSchema("nald")
            .WithColumn("RV_LOW_VALUE").AsString().Nullable()
            .WithColumn("RV_HIGH_VALUE").AsString().Nullable()
            .WithColumn("RV_ABBREVIATION").AsString().Nullable()
            .WithColumn("RV_DOMAIN").AsString().Nullable()
            .WithColumn("RV_MEANING").AsString().Nullable()
            .WithColumn("RV_TYPE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // NAME,DESCR,RUN_MODE,BUS_SUB_DIR,FILE_PREFIX,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_REPORTS").InSchema("nald")
            .WithColumn("NAME").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("RUN_MODE").AsString().Nullable()
            .WithColumn("BUS_SUB_DIR").AsString().Nullable()
            .WithColumn("FILE_PREFIX").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ARTS_NAME,APDR_NAME,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_REPORT_DRIVERS").InSchema("nald")
            .WithColumn("ARTS_NAME").AsString().Nullable()
            .WithColumn("APDR_NAME").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AABL_ID,USER_ID,REPORT_DATETIME,AABL_LIC_NO,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_REPORT_LICENCES").InSchema("nald")
            .WithColumn("AABL_ID").AsString().Nullable()
            .WithColumn("USER_ID").AsString().Nullable()
            .WithColumn("REPORT_DATETIME").AsString().Nullable()
            .WithColumn("AABL_LIC_NO").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,NAME,NGR_SHEET,NGR_EAST,NGR_NORTH,CART_EAST,CART_NORTH,ARUT_CODE,DISABLED,AREP_CODE,ACON_AADD_ID,ACON_APAR_ID,NOTES,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_REP_UNITS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("NAME").AsString().Nullable()
            .WithColumn("NGR_SHEET").AsString().Nullable()
            .WithColumn("NGR_EAST").AsString().Nullable()
            .WithColumn("NGR_NORTH").AsString().Nullable()
            .WithColumn("CART_EAST").AsString().Nullable()
            .WithColumn("CART_NORTH").AsString().Nullable()
            .WithColumn("ARUT_CODE").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("AREP_CODE").AsString().Nullable()
            .WithColumn("ACON_AADD_ID").AsString().Nullable()
            .WithColumn("ACON_APAR_ID").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AAIP_ID,AREP_CODE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_REP_UNIT_POINTS").InSchema("nald")
            .WithColumn("AAIP_ID").AsString().Nullable()
            .WithColumn("AREP_CODE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,USED_BY_SYS,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_REP_UNIT_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("USED_BY_SYS").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // REC_FREQ_CODE,RET_FREQ_CODE,REC_FREQ_DESCR,RET_FREQ_DESCR,NO_OF_DAYS_GRACE,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_RET_AGENCY_FREQS").InSchema("nald")
            .WithColumn("REC_FREQ_CODE").AsString().Nullable()
            .WithColumn("RET_FREQ_CODE").AsString().Nullable()
            .WithColumn("REC_FREQ_DESCR").AsString().Nullable()
            .WithColumn("RET_FREQ_DESCR").AsString().Nullable()
            .WithColumn("NO_OF_DAYS_GRACE").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_RET_COL_FREQS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ARTY_ID,AAIP_ID,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_RET_FMT_POINTS").InSchema("nald")
            .WithColumn("ARTY_ID").AsString().Nullable()
            .WithColumn("AAIP_ID").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ARTY_ID,APUR_APPR_CODE,APUR_APSE_CODE,APUR_APUS_CODE,PURP_ALIAS,PURP_ALIAS_WELSH,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_RET_FMT_PURPOSES").InSchema("nald")
            .WithColumn("ARTY_ID").AsString().Nullable()
            .WithColumn("APUR_APPR_CODE").AsString().Nullable()
            .WithColumn("APUR_APSE_CODE").AsString().Nullable()
            .WithColumn("APUR_APUS_CODE").AsString().Nullable()
            .WithColumn("PURP_ALIAS").AsString().Nullable()
            .WithColumn("PURP_ALIAS_WELSH").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,ARVN_AABL_ID,ARVN_VERS_NO,RETURN_FORM_TYPE,ARTC_CODE,ARTC_REC_FREQ_CODE,ARTC_RET_FREQ_CODE,FORMS_REQ_ALL_YEAR,FORM_PRODN_MONTH,NO_OF_DAYS_GRACE,TPT_FLAG,ABS_PERIOD_ST_DAY,ABS_PERIOD_ST_MONTH,ABS_PERIOD_END_DAY,ABS_PERIOD_END_MONTH,TIMELTD_ST_DATE,TIMELTD_END_DATE,DISP_ORD,SITE_DESCR,DESCR,ANNUAL_QTY,ANNUAL_QTY_USABILITY,CC_IND,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_RET_FORMATS").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("ARVN_AABL_ID").AsString().Nullable()
            .WithColumn("ARVN_VERS_NO").AsString().Nullable()
            .WithColumn("RETURN_FORM_TYPE").AsString().Nullable()
            .WithColumn("ARTC_CODE").AsString().Nullable()
            .WithColumn("ARTC_REC_FREQ_CODE").AsString().Nullable()
            .WithColumn("ARTC_RET_FREQ_CODE").AsString().Nullable()
            .WithColumn("FORMS_REQ_ALL_YEAR").AsString().Nullable()
            .WithColumn("FORM_PRODN_MONTH").AsString().Nullable()
            .WithColumn("NO_OF_DAYS_GRACE").AsString().Nullable()
            .WithColumn("TPT_FLAG").AsString().Nullable()
            .WithColumn("ABS_PERIOD_ST_DAY").AsString().Nullable()
            .WithColumn("ABS_PERIOD_ST_MONTH").AsString().Nullable()
            .WithColumn("ABS_PERIOD_END_DAY").AsString().Nullable()
            .WithColumn("ABS_PERIOD_END_MONTH").AsString().Nullable()
            .WithColumn("TIMELTD_ST_DATE").AsString().Nullable()
            .WithColumn("TIMELTD_END_DATE").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SITE_DESCR").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("ANNUAL_QTY").AsString().Nullable()
            .WithColumn("ANNUAL_QTY_USABILITY").AsString().Nullable()
            .WithColumn("CC_IND").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ARTY_ID,DATE_FROM,DATE_TO,UNDER_QUERY_FLAG,FORM_PROD_NO,FORM_PROD_ST_DATE,FORM_PROD_IN_PROGRESS,YEAR2_ST_DATE,YEAR1_END_DATE,ALRO_ID,ACON_APAR_ID_TO,ACON_AADD_ID_TO,SENT_DATE,RECD_DATE,REQD_BY_DATE,CLOSED_DATE,ACON_APAR_ID_FROM,ACON_AADD_ID_FROM,MONTHLY_RET_QTY,UNDER_QUERY_NOTE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_RET_FORM_LOGS").InSchema("nald")
            .WithColumn("ARTY_ID").AsString().Nullable()
            .WithColumn("DATE_FROM").AsString().Nullable()
            .WithColumn("DATE_TO").AsString().Nullable()
            .WithColumn("UNDER_QUERY_FLAG").AsString().Nullable()
            .WithColumn("FORM_PROD_NO").AsString().Nullable()
            .WithColumn("FORM_PROD_ST_DATE").AsString().Nullable()
            .WithColumn("FORM_PROD_IN_PROGRESS").AsString().Nullable()
            .WithColumn("YEAR2_ST_DATE").AsString().Nullable()
            .WithColumn("YEAR1_END_DATE").AsString().Nullable()
            .WithColumn("ALRO_ID").AsString().Nullable()
            .WithColumn("ACON_APAR_ID_TO").AsString().Nullable()
            .WithColumn("ACON_AADD_ID_TO").AsString().Nullable()
            .WithColumn("SENT_DATE").AsString().Nullable()
            .WithColumn("RECD_DATE").AsString().Nullable()
            .WithColumn("REQD_BY_DATE").AsString().Nullable()
            .WithColumn("CLOSED_DATE").AsString().Nullable()
            .WithColumn("ACON_APAR_ID_FROM").AsString().Nullable()
            .WithColumn("ACON_AADD_ID_FROM").AsString().Nullable()
            .WithColumn("MONTHLY_RET_QTY").AsString().Nullable()
            .WithColumn("UNDER_QUERY_NOTE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ARCF_CODE,ARAF_REC_FREQ_CODE,ARAF_RET_FREQ_CODE,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_RET_FREQ_COMBS").InSchema("nald")
            .WithColumn("ARCF_CODE").AsString().Nullable()
            .WithColumn("ARAF_REC_FREQ_CODE").AsString().Nullable()
            .WithColumn("ARAF_RET_FREQ_CODE").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ARFL_ARTY_ID,ARFL_DATE_FROM,RET_DATE,RET_QTY,RET_QTY_USABILITY,UNIT_RET_FLAG,ATPT_ACEL_ID,ATPT_FIN_YEAR,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_RET_LINES").InSchema("nald")
            .WithColumn("ARFL_ARTY_ID").AsString().Nullable()
            .WithColumn("ARFL_DATE_FROM").AsString().Nullable()
            .WithColumn("RET_DATE").AsString().Nullable()
            .WithColumn("RET_QTY").AsString().Nullable()
            .WithColumn("RET_QTY_USABILITY").AsString().Nullable()
            .WithColumn("UNIT_RET_FLAG").AsString().Nullable()
            .WithColumn("ATPT_ACEL_ID").AsString().Nullable()
            .WithColumn("ATPT_FIN_YEAR").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // FORM_PROD_NO,ARTY_ID,ARVN_VERS_NO,LIC_NO,NALD_ERROR,ORACLE_ERROR,FGAC_REGION_CODE,ERR_DATETIME,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_RET_LOG_ERRORS").InSchema("nald")
            .WithColumn("FORM_PROD_NO").AsString().Nullable()
            .WithColumn("ARTY_ID").AsString().Nullable()
            .WithColumn("ARVN_VERS_NO").AsString().Nullable()
            .WithColumn("LIC_NO").AsString().Nullable()
            .WithColumn("NALD_ERROR").AsString().Nullable()
            .WithColumn("ORACLE_ERROR").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("ERR_DATETIME").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AABL_ID,VERS_NO,EFF_ST_DATE,STATUS,FORM_LOGS_REQD,EFF_END_DATE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_RET_VERSIONS").InSchema("nald")
            .WithColumn("AABL_ID").AsString().Nullable()
            .WithColumn("VERS_NO").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("STATUS").AsString().Nullable()
            .WithColumn("FORM_LOGS_REQD").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ID,JOB_TYPE,RUN_FILE,USER_ID,BUS_SUB_DIR,DESCR,DEFER_IND,SUB_DATETIME,ST_DATETIME,END_DATETIME,STATUS,DESNAME,DESTYPE,PARAM_LIST,DESFORMAT,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SCHED_JOBS_FGAC").InSchema("nald")
            .WithColumn("ID").AsString().Nullable()
            .WithColumn("JOB_TYPE").AsString().Nullable()
            .WithColumn("RUN_FILE").AsString().Nullable()
            .WithColumn("USER_ID").AsString().Nullable()
            .WithColumn("BUS_SUB_DIR").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DEFER_IND").AsString().Nullable()
            .WithColumn("SUB_DATETIME").AsString().Nullable()
            .WithColumn("ST_DATETIME").AsString().Nullable()
            .WithColumn("END_DATETIME").AsString().Nullable()
            .WithColumn("STATUS").AsString().Nullable()
            .WithColumn("DESNAME").AsString().Nullable()
            .WithColumn("DESTYPE").AsString().Nullable()
            .WithColumn("PARAM_LIST").AsString().Nullable()
            .WithColumn("DESFORMAT").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,SEAS_ST_DAY,SEAS_ST_MONTH,SEAS_END_DAY,SEAS_END_MONTH,NO_OF_DAYS,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SEAS_FACTORS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("SEAS_ST_DAY").AsString().Nullable()
            .WithColumn("SEAS_ST_MONTH").AsString().Nullable()
            .WithColumn("SEAS_END_DAY").AsString().Nullable()
            .WithColumn("SEAS_END_MONTH").AsString().Nullable()
            .WithColumn("NO_OF_DAYS").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ASFT_CODE,EFF_ST_DATE,VALUE,EFF_END_DATE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SEAS_FACTOR_VALS").InSchema("nald")
            .WithColumn("ASFT_CODE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("VALUE").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SEAS_LIC_AVAILS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // SFT_ID,SFT_NAME,SFT_TYPE,SFT_UPDTYPE,SFT_DEFAULT,SFT_CHANGE,SFT_MANAGER,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SOFTWARE").InSchema("nald")
            .WithColumn("SFT_ID").AsString().Nullable()
            .WithColumn("SFT_NAME").AsString().Nullable()
            .WithColumn("SFT_TYPE").AsString().Nullable()
            .WithColumn("SFT_UPDTYPE").AsString().Nullable()
            .WithColumn("SFT_DEFAULT").AsString().Nullable()
            .WithColumn("SFT_CHANGE").AsString().Nullable()
            .WithColumn("SFT_MANAGER").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // SFT_ID,ROLE_NAME,ROLE_PRIV,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SOFTWARE_PRIVS").InSchema("nald")
            .WithColumn("SFT_ID").AsString().Nullable()
            .WithColumn("ROLE_NAME").AsString().Nullable()
            .WithColumn("ROLE_PRIV").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // SFT_ID,BUTTON_NUMBER,SBT_SEQ,SBT_DEFAULT,SBT_CHANGE,SBT_MANAGER,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SOFT_BUTTONS").InSchema("nald")
            .WithColumn("SFT_ID").AsString().Nullable()
            .WithColumn("BUTTON_NUMBER").AsString().Nullable()
            .WithColumn("SBT_SEQ").AsString().Nullable()
            .WithColumn("SBT_DEFAULT").AsString().Nullable()
            .WithColumn("SBT_CHANGE").AsString().Nullable()
            .WithColumn("SBT_MANAGER").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // BUTTON_NUMBER,SFT_ID,ROLE_NAME,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SOFT_BUTTON_PRIVS").InSchema("nald")
            .WithColumn("BUTTON_NUMBER").AsString().Nullable()
            .WithColumn("SFT_ID").AsString().Nullable()
            .WithColumn("ROLE_NAME").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,NAME,LOCAL_NAME,SRC_TYPE,NGR_SHEET,NGR_EAST,NGR_NORTH,CART_EAST,CART_NORTH,DISABLED,AQUIFER_CLASS,NOTES,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SOURCES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("NAME").AsString().Nullable()
            .WithColumn("LOCAL_NAME").AsString().Nullable()
            .WithColumn("SRC_TYPE").AsString().Nullable()
            .WithColumn("NGR_SHEET").AsString().Nullable()
            .WithColumn("NGR_EAST").AsString().Nullable()
            .WithColumn("NGR_NORTH").AsString().Nullable()
            .WithColumn("CART_EAST").AsString().Nullable()
            .WithColumn("CART_NORTH").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("AQUIFER_CLASS").AsString().Nullable()
            .WithColumn("NOTES").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SRC_FACTORS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ASRF_CODE,EFF_ST_DATE,VALUE,EFF_END_DATE,EIUC_SRCE_VALUE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SRC_FACTOR_VALS").InSchema("nald")
            .WithColumn("ASRF_CODE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("VALUE").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("EIUC_SRCE_VALUE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ASID_DIVISION,CLASS,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_STDIND_CLASSES").InSchema("nald")
            .WithColumn("ASID_DIVISION").AsString().Nullable()
            .WithColumn("CLASS").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // DIVISION,DESCR,DISABLED,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_STDIND_DIVISIONS").InSchema("nald")
            .WithColumn("DIVISION").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AREP_CODE,EFF_ST_DATE,SUC_VALUE,EFF_END_DATE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SUC_VALS").InSchema("nald")
            .WithColumn("AREP_CODE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("SUC_VALUE").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // REGION_CODE,REGION_NAME,INCOME_TYPE,IAS_SYSTEM_CODE,FIRST_BILLING_YEAR,FIN_YEAR_ST_DAY,FIN_YEAR_ST_MONTH,FIN_YEAR_END_DAY,FIN_YEAR_END_MONTH,DFLT_SRC_FACTOR,DFLT_DAYS_GRACE,BANK_SORT_CODE,BANK_ACCOUNT_NO,GIRO_TERMINATOR,OCR_FONT_SWITCH,LAST_IAS_NAME_XFER,FORM_PRODN_MONTH,ENQ_NAME,ENQ_NAME_WELSH,ENQ_TEL_NO,DFLT_VAT_CODE,PRINTER_DEFN_PATH,REPORT_DEST_PATH,REGION_NAME_WELSH,LAST_CUST_FILE_SEQ,LAST_TRANS_FILE_SEQ,FIMS_FILE_FREQUENCY,FIMS_FILE_TIME,FIMS_FILE_DAY,FIMS_FILE_DATE,FIMS_LAST_FILE_CREATED,CUST_FILE_SET,WA_LICS_ENABLED,TEMPORARY_LIC_CHARGEABLE,TRANSFER_LIC_CHARGEABLE,TEMP_LIC_LIMIT,DEREG_HIGH,DEREG_LOW,TLPA_APPLIED,TLPA_APPLIED_DATE,TLPA_FILE_ENABLED,FGAC_REGION_CODE,EIUC_COMP_ON,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_SYSTEM_PARAMS").InSchema("nald")
            .WithColumn("REGION_CODE").AsString().Nullable()
            .WithColumn("REGION_NAME").AsString().Nullable()
            .WithColumn("INCOME_TYPE").AsString().Nullable()
            .WithColumn("IAS_SYSTEM_CODE").AsString().Nullable()
            .WithColumn("FIRST_BILLING_YEAR").AsString().Nullable()
            .WithColumn("FIN_YEAR_ST_DAY").AsString().Nullable()
            .WithColumn("FIN_YEAR_ST_MONTH").AsString().Nullable()
            .WithColumn("FIN_YEAR_END_DAY").AsString().Nullable()
            .WithColumn("FIN_YEAR_END_MONTH").AsString().Nullable()
            .WithColumn("DFLT_SRC_FACTOR").AsString().Nullable()
            .WithColumn("DFLT_DAYS_GRACE").AsString().Nullable()
            .WithColumn("BANK_SORT_CODE").AsString().Nullable()
            .WithColumn("BANK_ACCOUNT_NO").AsString().Nullable()
            .WithColumn("GIRO_TERMINATOR").AsString().Nullable()
            .WithColumn("OCR_FONT_SWITCH").AsString().Nullable()
            .WithColumn("LAST_IAS_NAME_XFER").AsString().Nullable()
            .WithColumn("FORM_PRODN_MONTH").AsString().Nullable()
            .WithColumn("ENQ_NAME").AsString().Nullable()
            .WithColumn("ENQ_NAME_WELSH").AsString().Nullable()
            .WithColumn("ENQ_TEL_NO").AsString().Nullable()
            .WithColumn("DFLT_VAT_CODE").AsString().Nullable()
            .WithColumn("PRINTER_DEFN_PATH").AsString().Nullable()
            .WithColumn("REPORT_DEST_PATH").AsString().Nullable()
            .WithColumn("REGION_NAME_WELSH").AsString().Nullable()
            .WithColumn("LAST_CUST_FILE_SEQ").AsString().Nullable()
            .WithColumn("LAST_TRANS_FILE_SEQ").AsString().Nullable()
            .WithColumn("FIMS_FILE_FREQUENCY").AsString().Nullable()
            .WithColumn("FIMS_FILE_TIME").AsString().Nullable()
            .WithColumn("FIMS_FILE_DAY").AsString().Nullable()
            .WithColumn("FIMS_FILE_DATE").AsString().Nullable()
            .WithColumn("FIMS_LAST_FILE_CREATED").AsString().Nullable()
            .WithColumn("CUST_FILE_SET").AsString().Nullable()
            .WithColumn("WA_LICS_ENABLED").AsString().Nullable()
            .WithColumn("TEMPORARY_LIC_CHARGEABLE").AsString().Nullable()
            .WithColumn("TRANSFER_LIC_CHARGEABLE").AsString().Nullable()
            .WithColumn("TEMP_LIC_LIMIT").AsString().Nullable()
            .WithColumn("DEREG_HIGH").AsString().Nullable()
            .WithColumn("DEREG_LOW").AsString().Nullable()
            .WithColumn("TLPA_APPLIED").AsString().Nullable()
            .WithColumn("TLPA_APPLIED_DATE").AsString().Nullable()
            .WithColumn("TLPA_FILE_ENABLED").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("EIUC_COMP_ON").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_TIMELTD_AVAILS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_TLP_FACTORS").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ASRF_CODE,EFF_ST_DATE,VALUE,EFF_END_DATE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_TLP_FACTOR_VALS").InSchema("nald")
            .WithColumn("ASRF_CODE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("VALUE").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // ACEL_ID,FIN_YEAR,LATEST_RET_DATE,RET_RECD_DATE,RETURN_QTY,BILLABLE_RET_QTY,AUTO_SUM_INDICATOR,RET_ENTRY_INDICATOR,BILLED_DATE,FGAC_REGION_CODE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_TPT_RETURNS").InSchema("nald")
            .WithColumn("ACEL_ID").AsString().Nullable()
            .WithColumn("FIN_YEAR").AsString().Nullable()
            .WithColumn("LATEST_RET_DATE").AsString().Nullable()
            .WithColumn("RET_RECD_DATE").AsString().Nullable()
            .WithColumn("RETURN_QTY").AsString().Nullable()
            .WithColumn("BILLABLE_RET_QTY").AsString().Nullable()
            .WithColumn("AUTO_SUM_INDICATOR").AsString().Nullable()
            .WithColumn("RET_ENTRY_INDICATOR").AsString().Nullable()
            .WithColumn("BILLED_DATE").AsString().Nullable()
            .WithColumn("FGAC_REGION_CODE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_VAT_CODES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // AVAT_CODE,EFF_ST_DATE,VALUE,EFF_END_DATE,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_VAT_RATES").InSchema("nald")
            .WithColumn("AVAT_CODE").AsString().Nullable()
            .WithColumn("EFF_ST_DATE").AsString().Nullable()
            .WithColumn("VALUE").AsString().Nullable()
            .WithColumn("EFF_END_DATE").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_WA_LIC_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();

        // CODE,DESCR,PERIOD_FROM_DAY,PERIOD_FROM_MONTH,PERIOD_TO_DAY,PERIOD_TO_MONTH,DISABLED,DISP_ORD,SOURCE_CODE,BATCH_RUN_DATE
        Create.Table("NALD_YEAR_TYPES").InSchema("nald")
            .WithColumn("CODE").AsString().Nullable()
            .WithColumn("DESCR").AsString().Nullable()
            .WithColumn("PERIOD_FROM_DAY").AsString().Nullable()
            .WithColumn("PERIOD_FROM_MONTH").AsString().Nullable()
            .WithColumn("PERIOD_TO_DAY").AsString().Nullable()
            .WithColumn("PERIOD_TO_MONTH").AsString().Nullable()
            .WithColumn("DISABLED").AsString().Nullable()
            .WithColumn("DISP_ORD").AsString().Nullable()
            .WithColumn("SOURCE_CODE").AsString().Nullable()
            .WithColumn("BATCH_RUN_DATE").AsString().Nullable();
    }
}
