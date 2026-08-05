using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(55)]
public sealed class AddNaldConditionsIndex : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                        CREATE INDEX ix_nald_lic_conditions_join
                        ON nald."NALD_LIC_CONDITIONS" (
                            "AABP_ID",
                            "FGAC_REGION_CODE",
                            "ACIN_CODE"
                        );
                    """);
    }

    public override void Down()
    {
        Execute.Sql("""
                        DROP INDEX IF EXISTS nald.ix_nald_lic_conditions_join;
                    """);
    }
}