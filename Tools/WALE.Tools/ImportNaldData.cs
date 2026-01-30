using System.Globalization;
using System.Text;
using Npgsql;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.Tools.Config;

namespace WALE.Tools;

public static class ImportNaldData
{
    private static readonly Dictionary<string, Dictionary<string, string>> TableColumnTypes = new()
    {
        {
            "NALD_ABSTAT_CATGRIES", new Dictionary<string, string>
            {
                { "ALL_PRIMARY", "string" },
                { "ALL_SECONDARY", "string" },
                { "ALL_USES", "string" },
                { "DISP_ORD", "short" },
                { "INCLUDE_IN_REPORT", "string" },
                { "STAT_CATEGORY", "string" },
                { "STAT_REF", "decimal" },
            }
        },
        {
            "NALD_ABSTAT_CAT_PRIMS", new Dictionary<string, string>
            {
                { "AARC_STAT_REF", "decimal" },
                { "APPR_CODE", "string" },
            }
        },
        {
            "NALD_ABSTAT_CAT_SECS", new Dictionary<string, string>
            {
                { "AARC_STAT_REF", "decimal" },
                { "APSE_CODE", "string" },
            }
        },
        {
            "NALD_ABSTAT_CAT_USES", new Dictionary<string, string>
            {
                { "AARC_STAT_REF", "decimal" },
                { "APUS_CODE_FROM", "short" },
                { "APUS_CODE_TO", "short" },
            }
        },
        {
            "NALD_ABSTAT_EXCEPTIONS", new Dictionary<string, string>
            {
                { "AABL_ID", "int" },
                { "AABV_ID", "int" },
                { "AABV_INCR_NO", "short" },
                { "AABV_ISSUE_NO", "short" },
                { "AAYR_ARYR_CODE", "string" },
                { "AAYR_YEAR", "short" },
                { "ANN_ACT_QTY", "decimal" },
                { "ANN_AUTH_QTY", "decimal" },
                { "APUR_APPR_CODE", "string" },
                { "APUR_APSE_CODE", "string" },
                { "APUR_APUS_CODE", "short" },
                { "ARTY_ID", "int" },
                { "DATESTAMP", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "LIC_NO", "string" },
                { "NMES_MESSAGE_NUMBER", "string" },
            }
        },
        {
            "NALD_ABSTAT_REPORT_DATA", new Dictionary<string, string>
            {
                { "AABL_AREP_LEAP_CODE", "string" },
                { "AARC_STAT_REF", "decimal" },
                { "FGAC_REGION_CODE", "short" },
                { "GW_TOT_ACT_QTY", "long" },
                { "GW_TOT_AUTH_QTY", "long" },
                { "SW_TOT_ACT_QTY", "long" },
                { "SW_TOT_AUTH_QTY", "long" },
                { "TOT_LICENSED_RETURNED", "long" },
                { "TOT_NO_LICENCES", "long" },
                { "TW_TOT_ACT_QTY", "long" },
                { "TW_TOT_AUTH_QTY", "long" },
            }
        },
        {
            "NALD_ABSTAT_TOTALS", new Dictionary<string, string>
            {
                { "AABL_ID", "int" },
                { "AAYR_ARYR_CODE", "string" },
                { "AAYR_YEAR", "short" },
                { "ACT_OVERRIDDEN", "string" },
                { "ANN_ACT_QTY", "decimal" },
                { "ANN_ACT_USABILITY", "string" },
                { "ANN_AUTH_QTY", "decimal" },
                { "ANN_AUTH_USABILITY", "string" },
                { "APUR_APPR_CODE", "string" },
                { "APUR_APSE_CODE", "string" },
                { "APUR_APUS_CODE", "short" },
                { "AUTH_CALC_FROM_DAILY", "string" },
                { "AUTH_OVERRIDDEN", "string" },
                { "DELETED", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "PREV_YEAR_AUTH_USED", "string" },
                { "SOURCE_TYPE", "string" },
                { "USER_NOTES", "string" },
            }
        },
        {
            "NALD_ABSTAT_YEARS", new Dictionary<string, string>
            {
                { "ARYR_CODE", "string" },
                { "SNAPSHOT_DATE", "datetime" },
                { "YEAR", "short" },
            }
        },
        {
            "NALD_ABS_LICENCES", new Dictionary<string, string>
            {
                { "AREP_AREA_CODE", "string" },
                { "AREP_CAMS_CODE", "string" },
                { "AREP_EIUC_CODE", "string" },
                { "AREP_LEAP_CODE", "string" },
                { "AREP_SUC_CODE", "string" },
                { "EXPIRY_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "FOLL_LIC_NO", "string" },
                { "ID", "int" },
                { "LAPSED_DATE", "datetime" },
                { "LIC_NO", "string" },
                { "NOTES", "string" },
                { "ORIG_APP_NO", "string" },
                { "ORIG_EFF_DATE", "datetime" },
                { "ORIG_LIC_NO", "string" },
                { "ORIG_SIG_DATE", "datetime" },
                { "PREV_LIC_NO", "string" },
                { "REV_DATE", "datetime" },
                { "SUSP_FROM_BILLING", "string" },
                { "SUSP_FROM_RETURNS", "string" },
                { "X_REG_IND", "string" },
            }
        },
        {
            "NALD_ABS_LIC_PURPOSES", new Dictionary<string, string>
            {
                { "AABV_AABL_ID", "int" },
                { "AABV_INCR_NO", "short" },
                { "AABV_ISSUE_NO", "short" },
                { "ANNUAL_QTY", "decimal" },
                { "ANNUAL_QTY_USABILITY", "string" },
                { "APUR_APPR_CODE", "string" },
                { "APUR_APSE_CODE", "string" },
                { "APUR_APUS_CODE", "short" },
                { "AREC_CODE", "string" },
                { "DAILY_QTY", "decimal" },
                { "DAILY_QTY_USABILITY", "string" },
                { "DISP_ORD", "short" },
                { "FGAC_REGION_CODE", "short" },
                { "HOURLY_QTY", "decimal" },
                { "HOURLY_QTY_USABILITY", "string" },
                { "ID", "int" },
                { "INST_QTY", "decimal" },
                { "INST_QTY_USABILITY", "string" },
                { "LANDS", "string" },
                { "AMOM_CODE", "string" },
                { "NOTES", "string" },
                { "PERIOD_END_DAY", "short" },
                { "PERIOD_END_MONTH", "short" },
                { "PERIOD_ST_DAY", "short" },
                { "PERIOD_ST_MONTH", "short" },
                { "TIMELTD_END_DATE", "datetime" },
                { "TIMELTD_ST_DATE", "datetime" },
            }
        },
        {
            "NALD_ABS_LIC_QUANTITIES", new Dictionary<string, string>
            {
                { "AABV_AABL_ID", "int" },
                { "AABV_INCR_NO", "short" },
                { "AABV_ISSUE_NO", "short" },
                { "ANN_QTY", "decimal" },
                { "ANN_QTY_USABILITY", "string" },
                { "DAILY_QTY", "decimal" },
                { "DAILY_QTY_USABILITY", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "HOURLY_QTY", "decimal" },
                { "HOURLY_QTY_USABILITY", "string" },
                { "ID", "int" },
                { "INST_QTY", "decimal" },
                { "INST_QTY_USABILITY", "string" },
                { "NOTES", "string" },
                { "PERIOD_END_DAY", "short" },
                { "PERIOD_END_MONTH", "short" },
                { "PERIOD_ST_DAY", "short" },
                { "PERIOD_ST_MONTH", "short" },
            }
        },
        {
            "NALD_ABS_LIC_VERSIONS", new Dictionary<string, string>
            {
                { "AABL_ID", "int" },
                { "ACCL_CODE", "string" },
                { "ACON_AADD_ID", "int" },
                { "ACON_APAR_ID", "int" },
                { "ALTY_CODE", "string" },
                { "APPR_DATE", "datetime" },
                { "ASRC_CODE", "string" },
                { "DEREG_CODE", "string" },
                { "DEREG_DATE", "datetime" },
                { "EFF_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "INCR_NO", "short" },
                { "ISSUE_DATE", "datetime" },
                { "ISSUE_NO", "short" },
                { "LIC_STATUS", "string" },
                { "POST_DATE", "datetime" },
                { "SIG_DATE", "datetime" },
                { "VERSION_STATUS", "string" },
                { "WA_ALTY_CODE", "string" },
            }
        },
        {
            "NALD_ABS_PURP_POINTS", new Dictionary<string, string>
            {
                { "AAIP_ID", "int" },
                { "AABP_ID", "int" },
                { "AMOA_CODE", "string" },
                { "FGAC_REGION_CODE", "short" },
            }
        },
        {
            "NALD_ADDRESSES", new Dictionary<string, string>
            {
                { "APCO_CODE", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
                { "LINE1", "string" },
                { "LINE2", "string" },
                { "LINE3", "string" },
                { "LINE4", "string" },
                { "POSTCODE", "string" },
            }
        },
        {
            "NALD_APP_FORM_HELP", new Dictionary<string, string>
            {
                { "HLP_APPLN", "string" },
                { "HLP_MODTAB_NAME", "string" },
                { "HLP_SEQ", "short" },
                { "HLP_TEXT", "string" },
            }
        },
        {
            "NALD_BANK_CODES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_BILL_CHGVERSIONS", new Dictionary<string, string>
            {
                { "ABRN_BILL_RUN_NO", "short" },
                { "ABRN_FIN_YEAR", "string" },
                { "ACVR_AABL_ID", "int" },
                { "ACVR_VERS_NO", "short" },
                { "FGAC_REGION_CODE", "short" },
            }
        },
        {
            "NALD_BILL_ERRORS", new Dictionary<string, string>
            {
                { "ABRN_BILL_RUN_NO", "short" },
                { "ABRN_FIN_YEAR", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
                { "NMES_MESSAGE_NUMBER", "string" },
            }
        },
        {
            "NALD_BILL_HEADERS", new Dictionary<string, string>
            {
                { "ABHD_ID", "int" },
                { "ABRN_BILL_RUN_NO", "short" },
                { "ABRN_FIN_YEAR", "string" },
                { "ADDR_LINE1", "string" },
                { "ADDR_LINE2", "string" },
                { "ADDR_LINE3", "string" },
                { "ADDR_LINE4", "string" },
                { "ADDR_POSTCODE", "string" },
                { "BANK_ACCOUNT_NO", "string" },
                { "BANK_SORT_CODE", "string" },
                { "CUST_NAME", "string" },
                { "CUST_REF", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "GIRO_OCR", "string" },
                { "IAS_CUST_REF", "string" },
                { "ID", "int" },
                { "LH_ACC_NO", "string" },
                { "TOTAL_BILL_AMT", "decimal" },
                { "TOTAL_VAT_AMT", "decimal" },
            }
        },
        {
            "NALD_BILL_PROCESSES", new Dictionary<string, string>
            {
                { "ABRN_BILL_RUN_NO", "short" },
                { "ABRN_FIN_YEAR", "string" },
                { "END_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "MODULE_NAME", "string" },
                { "START_DATE", "datetime" },
                { "STATUS", "string" },
            }
        },
        {
            "NALD_BILL_RUNS", new Dictionary<string, string>
            {
                { "BILL_RUN_NO", "short" },
                { "BILL_RUN_TYPE", "string" },
                { "FIN_YEAR", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "RUN_DATE", "datetime" },
                { "RUN_STATUS", "string" },
                { "USER_ID", "string" },
            }
        },
        {
            "NALD_BILL_TPT_RETURNS", new Dictionary<string, string>
            {
                { "ABRN_BILL_RUN_NO", "short" },
                { "ABRN_FIN_YEAR", "string" },
                { "ACEL_ID", "int" },
                { "FGAC_REGION_CODE", "short" },
                { "FIN_YEAR", "string" },
            }
        },
        {
            "NALD_BILL_TRANS", new Dictionary<string, string>
            {
                { "ABHD_ID", "int" },
                { "ABRN_BILL_RUN_NO", "short" },
                { "ABRN_FIN_YEAR", "string" },
                { "ACEL_ID", "int" },
                { "BILL_AMT", "decimal" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
                { "LIC_ID", "int" },
                { "LINE_DESCR", "string" },
                { "LINE_TYPE", "string" },
                { "VAT_AMT", "decimal" },
                { "VAT_CODE", "string" },
                { "VERS_NO", "short" },
            }
        },
        {
            "NALD_BILL_YEARS", new Dictionary<string, string>
            {
                { "ABCV_ABRN_BILL_RUN_NO", "short" },
                { "ABCV_ABRN_FIN_YEAR", "string" },
                { "ABCV_ACVR_AABL_ID", "int" },
                { "ABCV_ACVR_VERS_NO", "short" },
                { "FGAC_REGION_CODE", "short" },
                { "FIN_YEAR", "string" },
            }
        },
        {
            "NALD_BUTTONS", new Dictionary<string, string>
            {
                { "BUTTON_NUMBER", "short" },
                { "BUTTON_TEXT", "string" },
                { "DESCRIPTION", "string" },
            }
        },
        {
            "NALD_CHG_AGRMNTS", new Dictionary<string, string>
            {
                { "ACEL_ID", "int" },
                { "AFSA_CODE", "string" },
                { "EFF_END_DATE", "datetime" },
                { "EFF_ST_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "VALUE", "decimal" },
            }
        },
        {
            "NALD_CHG_ELEMENTS", new Dictionary<string, string>
            {
                { "ACVR_AABL_ID", "int" },
                { "ACVR_VERS_NO", "short" },
                { "ALSF_CODE", "string" },
                { "ANNUAL_QTY", "decimal" },
                { "APUR_APPR_CODE", "string" },
                { "APUR_APSE_CODE", "string" },
                { "APUR_APUS_CODE", "short" },
                { "ASFT_CODE", "string" },
                { "ASFT_CODE_DERIVED", "string" },
                { "ASRF_CODE", "string" },
                { "BASE_CHARGE", "decimal" },
                { "CHARGE_STATUS", "string" },
                { "DAILY_QTY", "decimal" },
                { "EFF_END_DATE", "datetime" },
                { "EFF_ST_DATE", "datetime" },
                { "EIUC_COMP_QTY", "decimal" },
                { "FGAC_REGION_CODE", "short" },
                { "HOURLY_QTY", "decimal" },
                { "ID", "int" },
                { "INST_QTY", "decimal" },
                { "MIN_CHARGE", "decimal" },
                { "NOTES", "string" },
                { "PERIOD_END_DAY", "short" },
                { "PERIOD_END_MONTH", "short" },
                { "PERIOD_ST_DAY", "short" },
                { "PERIOD_ST_MONTH", "short" },
                { "TRANS_LOSS", "decimal" },
                { "UNIT_CHARGE", "decimal" },
            }
        },
        {
            "NALD_CHG_VERSIONS", new Dictionary<string, string>
            {
                { "AABL_ID", "int" },
                { "AIIA_ALHA_ACC_NO", "string" },
                { "AIIA_IAS_CUST_REF", "string" },
                { "EFF_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "VERS_NO", "short" },
                { "VERS_STATUS", "string" },
            }
        },
        {
            "NALD_CODE_CONTROLS", new Dictionary<string, string>
            {
                { "COL_NAME", "string" },
                { "LAST_VAL", "int" },
                { "TAB_NAME", "string" },
            }
        },
        {
            "NALD_CONTACTS", new Dictionary<string, string>
            {
                { "AADD_ID", "int" },
                { "APAR_ID", "int" },
                { "FGAC_REGION_CODE", "short" },
                { "SURNAME", "string" },
                { "TITLE", "string" },
                { "FORENAMES", "string" },
            }
        },
        {
            "NALD_CONT_NOS", new Dictionary<string, string>
            {
                { "ACNT_CODE", "string" },
                { "ACON_AADD_ID", "int" },
                { "ACON_APAR_ID", "int" },
                { "CONT_NO", "string" },
                { "FGAC_REGION_CODE", "short" },
            }
        },
        {
            "NALD_CONT_NO_TYPES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_CRIT_CLASSES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_CTRL_FLOWS", new Dictionary<string, string>
            {
                { "AMAN_CODE", "string" },
                { "FLOW_QTY", "decimal" },
                { "SEQ_NO", "short" },
            }
        },
        {
            "NALD_CTRL_LEVELS", new Dictionary<string, string>
            {
                { "AMAN_CODE", "string" },
                { "LEVEL_QTY", "decimal" },
                { "SEQ_NO", "short" },
            }
        },
        {
            "NALD_CTRL_POINT_TYPES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_DEREG_TYPES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_DOCUMENT_REFS", new Dictionary<string, string>
            {
                { "AABL_ID", "int" },
                { "AIMP_ID", "int" },
                { "DOC_REF", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
            }
        },
        {
            "NALD_EIUC_VALS", new Dictionary<string, string>
            {
                { "AREP_CODE", "string" },
                { "EFF_END_DATE", "datetime" },
                { "EFF_ST_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "VALUE", "decimal" },
            }
        },
        {
            "NALD_FIN_AGRMNT_TYPES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_FIN_AGRMNT_VALS", new Dictionary<string, string>
            {
                { "AFSA_CODE", "string" },
                { "EFF_END_DATE", "datetime" },
                { "EFF_ST_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "VALUE", "decimal" },
            }
        },
        {
            "NALD_FORM_HELP", new Dictionary<string, string>
            {
                { "HLP_MODTAB_NAME", "string" },
                { "HLP_TEXT", "string" },
            }
        },
        {
            "NALD_GROUP_LH_ACCS", new Dictionary<string, string>
            {
                { "ACC_NO", "string" },
                { "ACON_AADD_ID", "int" },
                { "ACON_APAR_ID", "int" },
                { "FGAC_REGION_CODE", "short" },
            }
        },
        {
            "NALD_IAS_INVOICE_ACCS", new Dictionary<string, string>
            {
                { "ACON_AADD_ID", "int" },
                { "ACON_APAR_ID", "int" },
                { "ALHA_ACC_NO", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "IAS_CUST_REF", "string" },
            }
        },
        {
            "NALD_IMP_LICENCES", new Dictionary<string, string>
            {
                { "AREA", "string" },
                { "CAMS", "string" },
                { "EXPIRY_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
                { "LEAP", "string" },
                { "LIC_NO", "string" },
                { "NOTES", "string" },
            }
        },
        {
            "NALD_IMP_LIC_PURPOSES", new Dictionary<string, string>
            {
                { "AIMV_AIMP_ID", "int" },
                { "AIMV_INCR_NO", "short" },
                { "AIMV_ISSUE_NO", "short" },
                { "AISI_CODE", "string" },
                { "AMOI_CODE", "string" },
                { "APUR_APPR_CODE", "string" },
                { "APUR_APSE_CODE", "string" },
                { "APUR_APUS_CODE", "short" },
                { "DISP_ORD", "short" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
                { "LANDS", "string" },
                { "NOTES", "string" },
            }
        },
        {
            "NALD_IMP_LIC_VERSIONS", new Dictionary<string, string>
            {
                { "ACCL_CODE", "string" },
                { "ACON_AADD_ID", "int" },
                { "ACON_APAR_ID", "int" },
                { "AIMP_ID", "int" },
                { "ASRC_CODE", "string" },
                { "EFF_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "INCR_NO", "short" },
                { "ISSUE_DATE", "datetime" },
                { "ISSUE_NO", "short" },
                { "LIC_STATUS", "string" },
                { "SIG_DATE", "datetime" },
                { "VERSION_STATUS", "string" },
            }
        },
        {
            "NALD_IMP_PURP_POINTS", new Dictionary<string, string>
            {
                { "AAIP_ID", "int" },
                { "AIPU_ID", "int" },
                { "FGAC_REGION_CODE", "short" },
                { "IMOI_CODE", "string" },
            }
        },
        {
            "NALD_IMP_SITE_STATUSES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_LH_ACCS", new Dictionary<string, string>
            {
                { "ACC_NO", "string" },
                { "ACON_AADD_ID", "int" },
                { "ACON_APAR_ID", "int" },
                { "AGCA_ACC_NO", "string" },
                { "FGAC_REGION_CODE", "short" },
            }
        },
        {
            "NALD_LH_AGRMNTS", new Dictionary<string, string>
            {
                { "AFSA_CODE", "string" },
                { "ALHA_ACC_NO", "string" },
                { "EFF_END_DATE", "datetime" },
                { "EFF_ST_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "VALUE", "decimal" },
            }
        },
        {
            "NALD_LH_REC_TYPES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_LH_SUSP_LOGS", new Dictionary<string, string>
            {
                { "ALHA_ACC_NO", "string" },
                { "AMRE_AMRE_TYPE", "string" },
                { "AMRE_CODE", "string" },
                { "CREATE_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "USER_ID", "string" },
            }
        },
        {
            "NALD_LIC_AGRMNTS", new Dictionary<string, string>
            {
                { "AABP_ID", "int" },
                { "AIPU_ID", "int" },
                { "ALSA_CODE", "string" },
                { "EFF_END_DATE", "datetime" },
                { "EFF_ST_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
                { "VALUE", "string" },
            }
        },
        {
            "NALD_LIC_AGRMNT_TYPES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_LIC_AVAILS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_LIC_CONDITIONS", new Dictionary<string, string>
            {
                { "AABP_ID", "int" },
                { "ACIN_CODE", "string" },
                { "ACIN_SUBCODE", "string" },
                { "AIPU_ID", "int" },
                { "COND_TEXT", "string" },
                { "DISP_ORD", "short" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
            }
        },
        {
            "NALD_LIC_COND_TYPES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
                { "SUBCODE", "string" },
            }
        },
        {
            "NALD_LIC_ROLES", new Dictionary<string, string>
            {
                { "AABL_ID", "int" },
                { "ACON_AADD_ID", "int" },
                { "ACON_APAR_ID", "int" },
                { "AIMP_ID", "int" },
                { "ALRT_CODE", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
            }
        },
        {
            "NALD_LIC_ROLE_TYPES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_LIC_TYPES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_LOSS_FACTORS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_LOSS_FACTOR_VALS", new Dictionary<string, string>
            {
                { "ALSF_CODE", "string" },
                { "EFF_END_DATE", "datetime" },
                { "EFF_ST_DATE", "datetime" },
                { "VALUE", "decimal" },
            }
        },
        {
            "NALD_MAN_REP_CODES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "REPORT_DATETIME", "datetime" },
                { "USER_ID", "string" },
            }
        },
        {
            "NALD_MAN_UNITS", new Dictionary<string, string>
            {
                { "AMAN_CODE", "string" },
                { "AMLA_CODE", "string" },
                { "APFR_CODE", "string" },
                { "APTY_CODE", "string" },
                { "ASLA_CODE", "string" },
                { "ATLL_CODE", "string" },
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
                { "FGAC_REGION_CODE", "short" },
            }
        },
        {
            "NALD_MAN_UNIT_POINTS", new Dictionary<string, string>
            {
                { "AAIP_ID", "int" },
                { "AMAN_CODE", "string" },
                { "FGAC_REGION_CODE", "short" },
            }
        },
        {
            "NALD_MEANS_OF_ABS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_MEANS_OF_IMP", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_MEANS_OF_MEASURE", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_MESSAGES", new Dictionary<string, string>
            {
                { "MESSAGE_NUMBER", "string" },
                { "MESSAGE_TEXT", "string" },
            }
        },
        {
            "NALD_MOD_LOGS", new Dictionary<string, string>
            {
                { "AABL_ID", "int" },
                { "AABV_INCR_NO", "short" },
                { "AABV_ISSUE_NO", "short" },
                { "ACVR_AABL_ID", "int" },
                { "ACVR_VERS_NO", "short" },
                { "AIMP_ID", "int" },
                { "AIMV_AIMP_ID", "int" },
                { "AIMV_INCR_NO", "short" },
                { "AIMV_ISSUE_NO", "short" },
                { "AMRE_AMRE_TYPE", "string" },
                { "AMRE_CODE", "string" },
                { "ARVN_AABL_ID", "int" },
                { "ARVN_VERS_NO", "short" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
                { "MOD_DATE", "datetime" },
                { "MOD_DESCR", "string" },
                { "USER_ID", "string" },
            }
        },
        {
            "NALD_MOD_REASONS", new Dictionary<string, string>
            {
                { "AMRE_TYPE", "string" },
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_NGR_CONVERSIONS", new Dictionary<string, string>
            {
                { "EASTING", "int" },
                { "NGR_SHEET", "string" },
                { "NORTHING", "int" },
            }
        },
        {
            "NALD_NRW_DELETIONS_AUDIT", new Dictionary<string, string>
            {
                { "DELETION_DATE", "datetime" },
                { "ID", "int" },
                { "LIC_NO", "string" },
                { "PK_VALUES", "string" },
                { "TABLE_NAME", "string" },
            }
        },
        {
            "NALD_PARTIES", new Dictionary<string, string>
            {
                { "ASIC_ASID_DIVISION", "string" },
                { "ASIC_CLASS", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
                { "NAME", "string" },
            }
        },
        {
            "NALD_POINTS", new Dictionary<string, string>
            {
                { "AADD_ID", "int" },
                { "AAPC_CODE", "string" },
                { "AAPT_APTP_CODE", "string" },
                { "AAPT_APTS_CODE", "string" },
                { "ABAN_CODE", "string" },
                { "ASRC_CODE", "string" },
                { "BANK_ACCOUNT_NO", "string" },
                { "BANK_SORT_CODE", "string" },
                { "CUST_NAME", "string" },
                { "CUST_REF", "string" },
                { "EASTING", "int" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
                { "NAME", "string" },
                { "NGR", "string" },
                { "NORTHING", "int" },
            }
        },
        {
            "NALD_POINT_CATEGORIES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_POINT_TYPES", new Dictionary<string, string>
            {
                { "APTP_CODE", "string" },
                { "APTS_CODE", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_POINT_TYPE_PRIMS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_POINT_TYPE_SECS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_POSTAL_COUNTIES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_PRES_FLOW_RESTS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_PRINTER_DRIVERS", new Dictionary<string, string>
            {
                { "COMMAND", "string" },
                { "NAME", "string" },
            }
        },
        {
            "NALD_PROC_DETAILS", new Dictionary<string, string>
            {
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
                { "NMES_MESSAGE_NUMBER", "string" },
                { "PROC_DATETIME", "datetime" },
                { "USER_ID", "string" },
            }
        },
        {
            "NALD_PURPOSES", new Dictionary<string, string>
            {
                { "APPR_CODE", "string" },
                { "APSE_CODE", "string" },
                { "APUS_CODE", "short" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_PURP_PRIMS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_PURP_SECS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_PURP_USES", new Dictionary<string, string>
            {
                { "ALSF_CODE", "string" },
                { "CODE", "short" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_REF_CODES", new Dictionary<string, string>
            {
                { "ABBREVIATION", "string" },
                { "DOMAIN", "string" },
                { "LOW_VALUE", "string" },
                { "MEANING", "string" },
            }
        },
        {
            "NALD_REPORTS", new Dictionary<string, string>
            {
                { "FILENAME", "string" },
                { "NAME", "string" },
            }
        },
        {
            "NALD_REPORT_DRIVERS", new Dictionary<string, string>
            {
                { "APDR_NAME", "string" },
                { "ARTS_NAME", "string" },
                { "TRAY", "string" },
            }
        },
        {
            "NALD_REPORT_LICENCES", new Dictionary<string, string>
            {
                { "AABL_ID", "int" },
                { "FGAC_REGION_CODE", "short" },
                { "REPORT_DATETIME", "datetime" },
                { "USER_ID", "string" },
            }
        },
        {
            "NALD_REP_UNITS", new Dictionary<string, string>
            {
                { "ACON_AADD_ID", "int" },
                { "ACON_APAR_ID", "int" },
                { "AREP_CODE", "string" },
                { "ARUT_CODE", "string" },
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
                { "FGAC_REGION_CODE", "short" },
            }
        },
        {
            "NALD_REP_UNIT_POINTS", new Dictionary<string, string>
            {
                { "AAIP_ID", "int" },
                { "AREP_CODE", "string" },
                { "FGAC_REGION_CODE", "short" },
            }
        },
        {
            "NALD_REP_UNIT_TYPES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_RET_AGENCY_FREQS", new Dictionary<string, string>
            {
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
                { "REC_FREQ_CODE", "string" },
                { "RET_FREQ_CODE", "string" },
            }
        },
        {
            "NALD_RET_COL_FREQS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_RET_FMT_POINTS", new Dictionary<string, string>
            {
                { "AAIP_ID", "int" },
                { "ARTY_ID", "int" },
                { "FGAC_REGION_CODE", "short" },
            }
        },
        {
            "NALD_RET_FMT_PURPOSES", new Dictionary<string, string>
            {
                { "APUR_APPR_CODE", "string" },
                { "APUR_APSE_CODE", "string" },
                { "APUR_APUS_CODE", "short" },
                { "ARTY_ID", "int" },
                { "FGAC_REGION_CODE", "short" },
            }
        },
        {
            "NALD_RET_FORMATS", new Dictionary<string, string>
            {
                { "ARTC_CODE", "string" },
                { "ARTC_REC_FREQ_CODE", "string" },
                { "ARTC_RET_FREQ_CODE", "string" },
                { "ARVN_AABL_ID", "int" },
                { "ARVN_VERS_NO", "short" },
                { "DESCR", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "ID", "int" },
                { "LATEST_RET_DATE", "datetime" },
                { "NOTES", "string" },
                { "RET_RECD_DATE", "datetime" },
            }
        },
        {
            "NALD_RET_FORM_LOGS", new Dictionary<string, string>
            {
                { "ACON_AADD_ID_FROM", "int" },
                { "ACON_AADD_ID_TO", "int" },
                { "ACON_APAR_ID_FROM", "int" },
                { "ACON_APAR_ID_TO", "int" },
                { "ALRO_ID", "int" },
                { "ARTY_ID", "int" },
                { "DATE_FROM", "datetime" },
                { "DATE_PRODUCED", "datetime" },
                { "DATE_TO", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "FORM_PROD_NO", "int" },
            }
        },
        {
            "NALD_RET_FREQ_COMBS", new Dictionary<string, string>
            {
                { "ARAF_REC_FREQ_CODE", "string" },
                { "ARAF_RET_FREQ_CODE", "string" },
                { "ARCF_CODE", "string" },
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_RET_LINES", new Dictionary<string, string>
            {
                { "ARFL_ARTY_ID", "int" },
                { "ARFL_DATE_FROM", "datetime" },
                { "ATPT_ACEL_ID", "int" },
                { "ATPT_FIN_YEAR", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "RET_DATE", "datetime" },
                { "RET_QTY", "decimal" },
            }
        },
        {
            "NALD_RET_LOG_ERRORS", new Dictionary<string, string>
            {
                { "ARTY_ID", "int" },
                { "FGAC_REGION_CODE", "short" },
                { "FORM_PROD_NO", "int" },
            }
        },
        {
            "NALD_RET_VERSIONS", new Dictionary<string, string>
            {
                { "AABL_ID", "int" },
                { "FGAC_REGION_CODE", "short" },
                { "VERS_NO", "short" },
                { "VERS_STATUS", "string" },
            }
        },
        {
            "NALD_SCHED_JOBS_FGAC", new Dictionary<string, string>
            {
                { "FGAC_REGION_CODE", "short" },
                { "JOB", "int" },
            }
        },
        {
            "NALD_SEAS_FACTORS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_SEAS_FACTOR_VALS", new Dictionary<string, string>
            {
                { "ASFT_CODE", "string" },
                { "EFF_END_DATE", "datetime" },
                { "EFF_ST_DATE", "datetime" },
                { "VALUE", "decimal" },
            }
        },
        {
            "NALD_SEAS_LIC_AVAILS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_SOFTWARE", new Dictionary<string, string>
            {
                { "SFT_DESCRIPTION", "string" },
                { "SFT_ID", "string" },
            }
        },
        {
            "NALD_SOFTWARE_PRIVS", new Dictionary<string, string>
            {
                { "ROLE_NAME", "string" },
                { "SFT_ID", "string" },
            }
        },
        {
            "NALD_SOFT_BUTTONS", new Dictionary<string, string>
            {
                { "BUTTON_NUMBER", "short" },
                { "SFT_ID", "string" },
            }
        },
        {
            "NALD_SOFT_BUTTON_PRIVS", new Dictionary<string, string>
            {
                { "BUTTON_NUMBER", "short" },
                { "ROLE_NAME", "string" },
                { "SFT_ID", "string" },
            }
        },
        {
            "NALD_SOURCES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
                { "FGAC_REGION_CODE", "short" },
            }
        },
        {
            "NALD_SRC_FACTORS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_SRC_FACTOR_VALS", new Dictionary<string, string>
            {
                { "ASRF_CODE", "string" },
                { "EFF_END_DATE", "datetime" },
                { "EFF_ST_DATE", "datetime" },
                { "VALUE", "decimal" },
            }
        },
        {
            "NALD_STDIND_CLASSES", new Dictionary<string, string>
            {
                { "ASID_DIVISION", "string" },
                { "CLASS", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_STDIND_DIVISIONS", new Dictionary<string, string>
            {
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
                { "DIVISION", "string" },
            }
        },
        {
            "NALD_SUC_VALS", new Dictionary<string, string>
            {
                { "AREP_CODE", "string" },
                { "EFF_END_DATE", "datetime" },
                { "EFF_ST_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "VALUE", "decimal" },
            }
        },
        {
            "NALD_SYSTEM_PARAMS", new Dictionary<string, string>
            {
                { "BANK_ACCOUNT_NO", "string" },
                { "BANK_SORT_CODE", "string" },
                { "CUST_FILE_SET", "string" },
                { "DEREG_HIGH", "decimal" },
                { "DEREG_LOW", "decimal" },
                { "DFLT_DAYS_GRACE", "short" },
                { "DFLT_SRC_FACTOR", "string" },
                { "DFLT_VAT_CODE", "string" },
                { "EIUC_COMP_ON", "string" },
                { "ENQ_NAME", "string" },
                { "ENQ_NAME_WELSH", "string" },
                { "ENQ_TEL_NO", "string" },
                { "FGAC_REGION_CODE", "short" },
                { "FIMS_FILE_DATE", "short" },
                { "FIMS_FILE_DAY", "string" },
                { "FIMS_FILE_FREQUENCY", "string" },
                { "FIMS_FILE_TIME", "string" },
                { "FIMS_LAST_FILE_CREATED", "datetime" },
                { "FORM_PRODN_MONTH", "short" },
                { "GIRO_TERMINATOR", "string" },
                { "LAST_CUST_FILE_SEQ", "int" },
                { "LAST_IAS_NAME_XFER", "datetime" },
                { "LAST_TRANS_FILE_SEQ", "int" },
                { "OCR_FONT_SWITCH", "string" },
                { "PRINTER_DEFN_PATH", "string" },
                { "REGION_CODE", "string" },
                { "REGION_NAME", "string" },
                { "REGION_NAME_WELSH", "string" },
                { "REPORT_DEST_PATH", "string" },
                { "TEMPORARY_LIC_CHARGEABLE", "string" },
                { "TEMP_LIC_LIMIT", "decimal" },
                { "TLPA_APPLIED", "string" },
                { "TLPA_APPLIED_DATE", "datetime" },
                { "TLPA_FILE_ENABLED", "string" },
                { "TRANSFER_LIC_CHARGEABLE", "string" },
                { "WA_LICS_ENABLED", "string" },
            }
        },
        {
            "NALD_TIMELTD_AVAILS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_TLP_FACTORS", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_TLP_FACTOR_VALS", new Dictionary<string, string>
            {
                { "ASRF_CODE", "string" },
                { "EFF_END_DATE", "datetime" },
                { "EFF_ST_DATE", "datetime" },
                { "VALUE", "decimal" },
            }
        },
        {
            "NALD_TPT_RETURNS", new Dictionary<string, string>
            {
                { "ACEL_ID", "int" },
                { "AUTO_SUM_INDICATOR", "string" },
                { "BILLABLE_RET_QTY", "decimal" },
                { "BILLED_DATE", "datetime" },
                { "FGAC_REGION_CODE", "short" },
                { "FIN_YEAR", "string" },
                { "LATEST_RET_DATE", "datetime" },
                { "RET_ENTRY_INDICATOR", "string" },
                { "RET_RECD_DATE", "datetime" },
                { "RETURN_QTY", "decimal" },
            }
        },
        {
            "NALD_VAT_CODES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_VAT_RATES", new Dictionary<string, string>
            {
                { "AVAT_CODE", "string" },
                { "EFF_END_DATE", "datetime" },
                { "EFF_ST_DATE", "datetime" },
                { "VALUE", "decimal" },
            }
        },
        {
            "NALD_WA_LIC_TYPES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
            }
        },
        {
            "NALD_YEAR_TYPES", new Dictionary<string, string>
            {
                { "CODE", "string" },
                { "DESCR", "string" },
                { "DISABLED", "string" },
                { "DISP_ORD", "short" },
                { "PERIOD_FROM_DAY", "short" },
                { "PERIOD_FROM_MONTH", "short" },
                { "PERIOD_TO_DAY", "short" },
                { "PERIOD_TO_MONTH", "short" },
            }
        },
    };

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

        await using var fkConnection = await dataSource.OpenConnectionAsync();
        await DropForeignKeysAsync(fkConnection);

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            Console.WriteLine($"Importing {fileName}...");

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

    private static async Task WriteRowAsync(NpgsqlBinaryImporter writer, List<string> values, string[] allColumnsInFile,
        string[] columnsToImport, string tableName)
    {
        if (values.Count != allColumnsInFile.Length)
        {
            // Only log if it's not just an empty line
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

            if (TableColumnTypes.TryGetValue(tableName, out var tableMapping) &&
                tableMapping.TryGetValue(columnName, out var type))
            {
                try
                {
                    switch (type)
                    {
                        case "short":
                            await writer.WriteAsync(short.Parse(value, CultureInfo.InvariantCulture),
                                NpgsqlTypes.NpgsqlDbType.Smallint);
                            break;
                        case "int":
                            await writer.WriteAsync(int.Parse(value, CultureInfo.InvariantCulture),
                                NpgsqlTypes.NpgsqlDbType.Integer);
                            break;
                        case "long":
                            await writer.WriteAsync(long.Parse(value, CultureInfo.InvariantCulture),
                                NpgsqlTypes.NpgsqlDbType.Bigint);
                            break;
                        case "decimal":
                            await writer.WriteAsync(decimal.Parse(value, CultureInfo.InvariantCulture),
                                NpgsqlTypes.NpgsqlDbType.Numeric);
                            break;
                        case "datetime":
                            string[] formats =
                            [
                                "dd/MM/yyyy",
                                "d/M/yyyy",
                                "yyyy-MM-dd",
                                "yyyy-MM-dd HH:mm:ss",
                                "yyyyMMddHHmmss"
                            ];
                            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                                    DateTimeStyles.None, out var result))
                            {
                                await writer.WriteAsync(result, NpgsqlTypes.NpgsqlDbType.Timestamp);
                            }
                            else
                            {
                                await writer.WriteAsync(DateTime.Parse(value, CultureInfo.InvariantCulture),
                                    NpgsqlTypes.NpgsqlDbType.Timestamp);
                            }

                            break;
                        default:
                            await writer.WriteAsync(value, NpgsqlTypes.NpgsqlDbType.Text);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    string message =
                        $"Error parsing value '{value}' for column '{columnName}' in table '{tableName}' as type '{type}': {ex.Message}. Writing as null.";
                    Console.WriteLine(message);
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