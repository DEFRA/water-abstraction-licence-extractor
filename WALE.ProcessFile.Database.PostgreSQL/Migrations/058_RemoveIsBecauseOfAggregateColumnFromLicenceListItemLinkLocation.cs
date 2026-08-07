using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(58)]
public sealed class RemoveIsBecauseOfAggregateColumnFromLicenceListItemLinkLocation : Migration
{
    public override void Up()
    {
        Delete.Column("is_because_of_aggregate").FromTable("licence_list_item_link_location");
    }

    public override void Down()
    {
        Alter.Table("licence_list_item_link_location")
            .AddColumn("is_because_of_aggregate").AsBoolean().Nullable();
    }
}