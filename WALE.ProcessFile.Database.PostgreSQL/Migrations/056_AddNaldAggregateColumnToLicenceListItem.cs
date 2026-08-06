using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(56)]
public sealed class AddNaldAggregateColumnToLicenceListItem : Migration
{
    public override void Up()
    {
        Alter.Table("licence_list_item")
            .AddColumn("nald_aggregate").AsString().Nullable();
    }

    public override void Down()
    {
        Delete.Column("nald_aggregate").FromTable("licence_list_item");
    }
}