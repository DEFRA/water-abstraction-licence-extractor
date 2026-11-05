using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(84)]
public class AddSourceLicenceNumberToLicenceSectionVerification : Migration
{
    public override void Up()
    {
        Alter.Table("licence_section_verification")
            .AddColumn("source_licence_number").AsString().Nullable();

        Execute.Sql(
            """
            UPDATE licence_section_verification v
            SET source_licence_number = COALESCE(
                (
                    SELECT l.licence_number
                    FROM licence l
                    WHERE l.file_id = v.licence_file_id
                      AND l.licence_number IS NOT NULL
                    ORDER BY (l.process_run_id = v.process_run_id) DESC, l.licence_id
                    LIMIT 1
                ),
                'UNKNOWN')
            WHERE v.source_licence_number IS NULL;
            """);

        Alter.Column("source_licence_number").OnTable("licence_section_verification")
            .AsString().NotNullable();

        Delete.Index("idx_licence_section_verification_licence_file_id").OnTable("licence_section_verification");

        Create.Index("idx_licence_section_verification_licence_file_id_source_licence_number")
            .OnTable("licence_section_verification")
            .OnColumn("licence_file_id").Ascending()
            .OnColumn("source_licence_number").Ascending();
    }

    public override void Down()
    {
        Delete.Index("idx_licence_section_verification_licence_file_id_source_licence_number")
            .OnTable("licence_section_verification");

        Create.Index("idx_licence_section_verification_licence_file_id")
            .OnTable("licence_section_verification")
            .OnColumn("licence_file_id").Ascending();

        Delete.Column("source_licence_number").FromTable("licence_section_verification");
    }
}
