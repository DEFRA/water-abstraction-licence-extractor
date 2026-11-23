using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(93)]
public class AddRegionIdToLicenceListItem : Migration
{
    private const string LicenceListItemTable = "licence_list_item";

    public override void Up()
    {
        Alter.Table(LicenceListItemTable)
            .AddColumn("region_id")
            .AsInt32()
            .Nullable();

        Execute.Sql("""
                        UPDATE licence_list_item lli
                        SET region_id = (l.data::jsonb ->> 'regionId')::int
                        FROM licence l
                        WHERE l.licence_id = lli.licence_id
                          AND l.data IS NOT NULL
                          AND pg_input_is_valid(l.data, 'jsonb')
                          AND (l.data::jsonb ->> 'regionId') ~ '^[0-9]+$';
                    """);
    }

    public override void Down()
    {
        Delete.Column("region_id")
            .FromTable(LicenceListItemTable);
    }
}
