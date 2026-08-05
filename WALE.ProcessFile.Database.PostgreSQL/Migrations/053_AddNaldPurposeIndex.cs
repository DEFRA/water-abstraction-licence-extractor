using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(53)]
public sealed class AddNaldPurposeIndex : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                        CREATE INDEX ix_nald_abs_lic_purposes_ver_join
                        ON nald."NALD_ABS_LIC_PURPOSES" (
                            "AABV_ISSUE_NO",
                            "AABV_INCR_NO",
                            "AABV_AABL_ID",
                            "FGAC_REGION_CODE",
                            "ID"
                        );
                    """);
    }

    public override void Down()
    {
        Execute.Sql("""
                        DROP INDEX IF EXISTS nald.ix_nald_abs_lic_purposes_ver_join;
                    """);
    }
}