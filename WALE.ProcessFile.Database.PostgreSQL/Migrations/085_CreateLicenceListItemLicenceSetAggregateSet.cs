using System.Data;
using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(85)]
public sealed class CreateLicenceListItemLicenceSetAggregateSet : Migration
{
    private const string LicenceSetAggregateSetTable =
        "licence_list_item_licence_set_aggregate_set";


    public override void Up()
    {
        CreateLicenceSetAggregateSetTable();
    }

    public override void Down()
    {
        Delete.Table(LicenceSetAggregateSetTable);
    }
    
    private void CreateLicenceSetAggregateSetTable()
    {
        Create.Table(LicenceSetAggregateSetTable)
            .WithColumn("licence_list_item_licence_set_id")
            .AsInt64()
            .NotNullable()

            .WithColumn("licence_list_item_licence_set_aggregate_set_id")
            .AsString()
            .Nullable()
            
            .WithColumn("data")
            .AsCustom("jsonb")
            .Nullable();
    }
}