using FluentMigrator;

namespace WRADI.Database.Migrations;

[Migration(70)]
public class VerificationBackupTables : Migration
{
    public override void Up()
    {
        Create.Table("licence_section_verification_backup_version")
            .WithColumn("backup_version")
                .AsInt32()
                .PrimaryKey()
                .Identity()
            .WithColumn("backup_date_time_utc")
                .AsDateTime()
                .NotNullable()
                .WithDefault(SystemMethods.CurrentUTCDateTime);

        Create.Table("licence_section_verification_backup")
            .WithColumn("licence_section_verification_backup_id")
                .AsInt32()
                .PrimaryKey()
                .Identity()
            .WithColumn("backup_version")
                .AsInt32()
                .NotNullable()
            .WithColumn("licence_section_verification_id")
                .AsInt32()
                .NotNullable()
            .WithColumn("licence_file_id")
                .AsGuid()
                .NotNullable()
            .WithColumn("process_run_id")
                .AsInt32()
                .NotNullable()
            .WithColumn("licence_section_name")
                .AsString()
                .NotNullable()
            .WithColumn("verification_type")
                .AsString()
                .NotNullable()
            .WithColumn("created_date_time_utc")
                .AsDateTime()
                .NotNullable()
            .WithColumn("licence_section_scraped_value")
                .AsCustom("jsonb")
                .Nullable()
            .WithColumn("licence_section_override_value")
                .AsCustom("jsonb")
                .Nullable()
            .WithColumn("notes")
                .AsString()
                .Nullable()
            .WithColumn("licence_section_item_id")
                .AsString()
                .Nullable()
            .WithColumn("licence_section_snapshot_value")
                .AsCustom("jsonb")
                .Nullable()
            .WithColumn("deleted_date_time_utc")
                .AsDateTime()
                .Nullable();

        Create.ForeignKey("fk_licence_section_verification_backup_version")
            .FromTable("licence_section_verification_backup")
            .ForeignColumn("backup_version")
            .ToTable("licence_section_verification_backup_version")
            .PrimaryColumn("backup_version");

        Create.Index("idx_licence_section_verification_backup_version")
            .OnTable("licence_section_verification_backup")
            .OnColumn("backup_version")
            .Ascending();

        Create.Index("idx_licence_section_verification_backup_licence_file_id")
            .OnTable("licence_section_verification_backup")
            .OnColumn("licence_file_id")
            .Ascending();

        Create.Index("idx_licence_section_verification_backup_licence_section_item_id")
            .OnTable("licence_section_verification_backup")
            .OnColumn("licence_section_item_id")
            .Ascending();

        Create.Index("idx_licence_section_verification_backup_original_id")
            .OnTable("licence_section_verification_backup")
            .OnColumn("licence_section_verification_id")
            .Ascending();

        Create.Index("idx_licence_section_verification_backup_process_run_id")
            .OnTable("licence_section_verification_backup")
            .OnColumn("process_run_id")
            .Ascending();

        Create.Index("uq_licence_section_verification_backup_version_verification")
            .OnTable("licence_section_verification_backup")
            .OnColumn("backup_version").Ascending()
            .OnColumn("licence_section_verification_id").Ascending()
            .WithOptions()
            .Unique();
    }

    public override void Down()
    {
        Delete.Table("licence_section_verification_backup");
        Delete.Table("licence_section_verification_backup_version");
    }
}