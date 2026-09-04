using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(66)]
public class SeedInitialDocumentNaldPurposeMapTableData : Migration
{
    public override void Up()
    {
        Execute.Sql("""
                        INSERT INTO public.document_nald_purpose_map (
                        document_purpose,
                        nald_purpose_primary_category_code,
                        nald_purpose_secondary_category_code,
                        nald_purpose_use_code,
                        match_type)
                    VALUES (
                        'Agriculture (other than spray irrigation)',
                        'A',
                        'AGR',
                        140,
                        'ExplicitMapping'
                    );
                    
                    INSERT INTO public.document_nald_purpose_map (
                        document_purpose,
                        nald_purpose_primary_category_code,
                        nald_purpose_secondary_category_code,
                        nald_purpose_use_code,
                        match_type)
                    VALUES (
                        'Reservoir storage for subsequent stream compensation',
                        'W',
                        'WAT',
                        450,
                        'ExplicitMapping'
                    );
                    
                    INSERT INTO public.document_nald_purpose_map (
                        document_purpose,
                        nald_purpose_primary_category_code,
                        nald_purpose_secondary_category_code,
                        nald_purpose_use_code,
                        match_type)
                    VALUES (
                        'Private water supply',
                        'W',
                        'PWS',
                        180,
                        'ExplicitMapping'
                    );
                    
                    INSERT INTO public.document_nald_purpose_map (
                        document_purpose,
                        nald_purpose_primary_category_code,
                        nald_purpose_secondary_category_code,
                        nald_purpose_use_code,
                        match_type)
                    VALUES (
                        'Private water supply',
                        'W',
                        'PWS',
                        170,
                        'ExplicitMapping'
                    );
                    
                    INSERT INTO public.document_nald_purpose_map (
                        document_purpose,
                        nald_purpose_primary_category_code,
                        nald_purpose_secondary_category_code,
                        nald_purpose_use_code,
                        match_type)
                    VALUES (
                       'Private water supply',
                        'W',
                        'PWS',
                        160,
                        'ExplicitMapping'
                    );
                    
                    INSERT INTO public.document_nald_purpose_map (
                        document_purpose,
                        nald_purpose_primary_category_code,
                        nald_purpose_secondary_category_code,
                        nald_purpose_use_code,
                        match_type)
                    VALUES (
                        'Private water supply',
                        'W',
                        'PWS',
                        150,
                        'ExplicitMapping'
                    );
                    
                    INSERT INTO public.document_nald_purpose_map (
                        document_purpose,
                        nald_purpose_primary_category_code,
                        nald_purpose_secondary_category_code,
                        nald_purpose_use_code,
                        match_type)
                    VALUES (
                        'Domestic & sanitation',
                        'I',
                        'RET',
                        40,
                        'ExplicitMapping'
                    );
                    
                    INSERT INTO public.document_nald_purpose_map (
                        document_purpose,
                        nald_purpose_primary_category_code,
                        nald_purpose_secondary_category_code,
                        nald_purpose_use_code,
                        match_type)
                    VALUES (
                        'Ground source heating and cooling pump',
                        'A',
                        'AGR',
                        200,
                        'ExplicitMapping'
                    );
                    
                    INSERT INTO public.document_nald_purpose_map (
                        document_purpose,
                        nald_purpose_primary_category_code,
                        nald_purpose_secondary_category_code,
                        nald_purpose_use_code,
                        match_type)
                    VALUES (
                        'Domestic',
                        'I',
                        'RET',
                        40,
                        'ExplicitMapping'
                    );
                    """);
    }

    public override void Down()
    {
    }
}