using FluentMigrator;

namespace WALE.ProcessFile.Database.PostgreSQL.Migrations;

[Migration(1)]
public class InitialSchema : Migration
{
    public override void Up()
    {
        Create.Table("process_run")
            .WithColumn("process_run_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("description").AsString().Nullable()
            .WithColumn("start_date_time_utc").AsDateTime().NotNullable()
            .WithColumn("end_date_time_utc").AsDateTime().Nullable()
            .WithColumn("number_of_files").AsInt32().NotNullable();

        Create.Table("licence_set")
            .WithColumn("licence_set_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("schema_licence_set_id").AsString().NotNullable()
            .WithColumn("short_licence_set_id").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();

        Create.Table("licence")
            .WithColumn("licence_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("filename").AsString().NotNullable()
            .WithColumn("data").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable()
            .WithColumn("licence_number").AsString().Nullable();

        Create.Table("licence_set_type")
            .WithColumn("licence_set_id").AsInt32().NotNullable()
            .WithColumn("licence_set_type").AsInt32().NotNullable();

        Create.Table("no_ocr_page_text_cache")
            .WithColumn("no_ocr_page_text_cache_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("filename").AsString().NotNullable()
            .WithColumn("page_number").AsInt32().NotNullable()
            .WithColumn("no_ocr_service_name").AsString().NotNullable()
            .WithColumn("data").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();

        Create.Table("no_ocr_pages_metadata_cache")
            .WithColumn("no_ocr_pages_metadata_cache_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("filename").AsString().NotNullable()
            .WithColumn("no_ocr_service_name").AsString().NotNullable()
            .WithColumn("response").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();

        Create.Table("page_screenshot")
            .WithColumn("page_screenshot_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("filename").AsString().NotNullable()
            .WithColumn("page_number").AsInt32().NotNullable()
            .WithColumn("no_ocr_service_name").AsString().NotNullable()
            .WithColumn("data").AsBinary(int.MaxValue).NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();

        Create.Table("all_pages_text")
            .WithColumn("all_pages_text_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("filename").AsString().NotNullable()
            .WithColumn("no_ocr_service_name").AsString().NotNullable()
            .WithColumn("data").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();

        Create.Table("no_ocr_images_metadata_cache")
            .WithColumn("no_ocr_images_metadata_cache_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("filename").AsString().NotNullable()
            .WithColumn("no_ocr_service_name").AsString().NotNullable()
            .WithColumn("response").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();

        Create.Table("image_on_page")
            .WithColumn("image_on_page_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("filename").AsString().NotNullable()
            .WithColumn("no_ocr_service_name").AsString().NotNullable()
            .WithColumn("data").AsBinary(int.MaxValue).NotNullable()
            .WithColumn("page_number").AsInt32().NotNullable()
            .WithColumn("image_number").AsInt32().NotNullable()
            .WithColumn("extension").AsString(5).NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();

        Create.Table("ocr_image_text_cache")
            .WithColumn("ocr_image_text_cache_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("filename").AsString().NotNullable()
            .WithColumn("page_number").AsInt32().NotNullable()
            .WithColumn("image_number").AsInt32().NotNullable()
            .WithColumn("ocr_service_name").AsString().NotNullable()
            .WithColumn("data").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();

        Create.Table("matches_result")
            .WithColumn("matches_result_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("filename").AsString().NotNullable()
            .WithColumn("data").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();

        Create.Table("licence_set_licence")
            .WithColumn("licence_set_licence_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("licence_set_id").AsInt32().NotNullable()
            .WithColumn("licence_number").AsString().NotNullable()
            .WithColumn("licence_version_id").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable()
            .WithColumn("licence_id").AsInt32().Nullable();

        Create.Table("aggregate_set")
            .WithColumn("aggregate_set_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("licence_set_id").AsInt32().NotNullable()
            .WithColumn("schema_aggregate_set_id").AsString().NotNullable()
            .WithColumn("data").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();

        Create.Table("ocr_screenshot_text_cache")
            .WithColumn("ocr_image_text_cache_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("process_run_id").AsInt32().NotNullable()
            .WithColumn("filename").AsString().NotNullable()
            .WithColumn("page_number").AsInt32().NotNullable()
            .WithColumn("ocr_service_name").AsString().NotNullable()
            .WithColumn("data").AsString().NotNullable()
            .WithColumn("date_time_utc").AsDateTime().NotNullable();

        Create.Table("match")
            .WithColumn("match_id").AsInt32().PrimaryKey().Identity()
            .WithColumn("matches_result_id").AsInt32().NotNullable()
            .WithColumn("label_name").AsString().Nullable()
            .WithColumn("label_group_name").AsString().Nullable()
            .WithColumn("data").AsString().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("match");
        Delete.Table("ocr_screenshot_text_cache");
        Delete.Table("aggregate_set");
        Delete.Table("licence_set_licence");
        Delete.Table("matches_result");
        Delete.Table("ocr_image_text_cache");
        Delete.Table("image_on_page");
        Delete.Table("no_ocr_images_metadata_cache");
        Delete.Table("all_pages_text");
        Delete.Table("page_screenshot");
        Delete.Table("no_ocr_pages_metadata_cache");
        Delete.Table("no_ocr_page_text_cache");
        Delete.Table("licence_set_type");
        Delete.Table("licence");
        Delete.Table("licence_set");
        Delete.Table("process_run");
    }
}