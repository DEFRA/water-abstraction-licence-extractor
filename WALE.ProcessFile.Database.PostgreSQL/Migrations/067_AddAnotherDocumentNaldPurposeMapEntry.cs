using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(67)]
public class AddAnotherDocumentNaldPurposeMapEntry : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                        insert into public.document_nald_purpose_map (document_purpose, nald_purpose_primary_category_code, nald_purpose_secondary_category_code, nald_purpose_use_code, match_type) VALUES ('Lake compentation','I','HOL',280,'OnlyOne');
                    """);
    }

    public override void Down()
    {
    }
}