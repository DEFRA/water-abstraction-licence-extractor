using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(87)]
public sealed class AddOverrideMasterTable : Migration
{
    private const string OverrideMasterTable = "override_master";

    public override void Up()
    {
        Create.Table(OverrideMasterTable)
            .WithColumn("permit_number").AsString().Nullable()
            .WithColumn("licence_reference").AsString().Nullable()
            .WithColumn("file_url").AsString().Nullable()
            .WithColumn("nald_issue_no").AsString().Nullable()
            .WithColumn("nald_increment_no").AsString().Nullable()
            .WithColumn("override_type").AsString().Nullable()
            .WithColumn("file_id").AsString().Nullable();

        // An override is looked up by either key, whichever the NALD record carries.
        Create.Index("ix_override_master_permit_number")
            .OnTable(OverrideMasterTable)
            .OnColumn("permit_number").Ascending();

        Create.Index("ix_override_master_licence_reference")
            .OnTable(OverrideMasterTable)
            .OnColumn("licence_reference").Ascending();
    }

    public override void Down()
    {
        Delete.Index("ix_override_master_licence_reference").OnTable(OverrideMasterTable);
        Delete.Index("ix_override_master_permit_number").OnTable(OverrideMasterTable);
        Delete.Table(OverrideMasterTable);
    }
}
