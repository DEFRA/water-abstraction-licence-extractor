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
                        'Agriculture',
                        'General Agriculture',
                        'General Farming & Domestic',
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
                        'Water Supply',
                        'Water Supply Related',
                        'Transfer Between Sources (Pre Water Act 2003)',
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
                        'Water Supply',
                        'Private Water Supply',
                        'General Use Relating To Secondary Category (Very Low Loss)',
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
                        'Water Supply',
                        'Public Water Supply',
                        'General Use Relating To Secondary Category (Low Loss)',
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
                        'Water Supply',
                        'Public Water Supply',
                        'General Use Relating To Secondary Category (Medium Loss)',
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
                        'Water Supply',
                        'Public Water Supply',
                        'General Use Relating To Secondary Category (High Loss)',
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
                        'Industrial, Commercial And Public Services',
                        'Retail',
                        'Drinking, Cooking, Sanitary, Washing, (Small Garden) - Commercial/Industrial/Public Services',
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
                        'Agriculture',
                        'General Agriculture',
                        'Heat Pump',
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
                        'Industrial, Commercial And Public Services',
                        'Retail',
                        'Drinking, Cooking, Sanitary, Washing, (Small Garden) - Commercial/Industrial/Public Services',
                        'ExplicitMapping'
                    );
                    """);
    }

    public override void Down()
    {
    }
}