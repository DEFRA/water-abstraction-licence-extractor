using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(57)]
public sealed class AddIsBecauseOfAggregateColumnToLicenceListItemLinkedLicence : Migration
{
    public override void Up()
    {
        Alter.Table("licence_list_item_linked_licence")
            .AddColumn("is_because_of_aggregate").AsBoolean().Nullable();
    }

    public override void Down()
    {
        Delete.Column("is_because_of_aggregate").FromTable("licence_list_item_linked_licence");
    }
}