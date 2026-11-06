using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(88)]
public class AddIndexToNaldAbsLicenceLicenceNumberColumn : Migration
{
    public override void Up()
    {
        Create.Index("idx_nald_abs_licences_licence_number")
            .OnTable("NALD_ABS_LICENCES")
            .InSchema("nald")
            .OnColumn("LIC_NO").Ascending()
            .OnColumn("FGAC_REGION_CODE").Ascending();
    }

    public override void Down()
    {
        Delete.Index("idx_nald_abs_licences_licence_number")
            .OnTable("NALD_ABS_LICENCES")
            .InSchema("nald");
    }
}