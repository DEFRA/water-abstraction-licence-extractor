using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(61)]
public sealed class AddAggregateIdsColumnToLicenceListItem : Migration
{
    public override void Up()
    {
        Alter.Table("licence_list_item")
            .AddColumn("aggregate_ids")
            .AsCustom("text[]")
            .NotNullable()
            .WithDefaultValue(
                RawSql.Insert(
                    "ARRAY[]::text[]"));
    }

    public override void Down()
    {
        Delete.Column("aggregate_ids").FromTable("licence_list_item");
    }
}
