using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(54)]
public sealed class AddNaldVersionIndex : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                        CREATE INDEX ix_nald_abs_lic_versions_join
                        ON nald."NALD_ABS_LIC_VERSIONS" (
                            "ISSUE_NO",
                            "INCR_NO",
                            "AABL_ID",
                            "FGAC_REGION_CODE"
                        );
                    """);
    }

    public override void Down()
    {
        Execute.Sql("""
                        DROP INDEX IF EXISTS nald.ix_nald_abs_lic_versions_join;
                    """);
    }
}