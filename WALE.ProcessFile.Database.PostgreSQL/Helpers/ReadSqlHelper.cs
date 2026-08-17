using System.Text;
using Dapper;

namespace WALE.ProcessFile.Database.PostgreSQL.Helpers;

public static class ReadSqlHelper
{
    public static void AddSearchTermFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return;
        }

        sql.AppendLine(
            """
              AND search_text ILIKE @SearchTerm
            """);

        parameters.Add(
            "SearchTerm",
            $"%{searchTerm.Trim()}%");
    }
    
    public static void AddStringFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string columnName,
        string parameterName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        sql.AppendLine(
            $"  AND {columnName} = @{parameterName}");

        parameters.Add(
            parameterName,
            value.Trim());
    }
    
    public static void AddBooleanFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string columnName,
        string parameterName,
        bool? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        sql.AppendLine(
            $"  AND {columnName} = @{parameterName}");

        parameters.Add(
            parameterName,
            value.Value);
    }
    
    public static void AddCountEmptyFilter(
        StringBuilder sql,
        bool? empty,
        string countColumn)
    {
        if (!empty.HasValue)
        {
            return;
        }

        sql.AppendLine(
            empty.Value
                ? $"  AND {countColumn} = 0"
                : $"  AND {countColumn} > 0");
    }
    
    public static void AddAggregatesFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string? aggregatesState)
    {
        if (string.IsNullOrEmpty(aggregatesState))
        {
            return;
        }
        
        var parts = aggregatesState.Split('_');
        var docAggregatesText = parts[0];
        var docAggregatesTextIsNull = docAggregatesText.Equals("null", StringComparison.OrdinalIgnoreCase);
        
        if (!docAggregatesTextIsNull)
        {
            var docAggregates = bool.Parse(docAggregatesText);
            var docAggregatesEmpty = !docAggregates;
            
            AddCountEmptyFilter(sql, docAggregatesEmpty, "aggregates_count");
        }

        var naldAggregatesText = parts[1];
        var naldAggregatesTextIsNull = naldAggregatesText.Equals("null", StringComparison.OrdinalIgnoreCase);

        if (!naldAggregatesTextIsNull)
        {
            var naldAggregates = bool.Parse(naldAggregatesText);

            sql.AppendLine(
                """
                  AND nald_aggregate = @NaldAggregate
                """);

            parameters.Add("NaldAggregate", naldAggregates);
        }
    }
    
    public static void AddIssueYearFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string? issueYear)
    {
        if (!short.TryParse(issueYear, out var year))
        {
            return;
        }

        sql.AppendLine(
            """
              AND issue_year = @IssueYear
            """);

        parameters.Add("IssueYear", year);
    }
    
    public static void AddLinkedLicencesTypeFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string? linkedLicencesType)
    {
        if (string.IsNullOrWhiteSpace(linkedLicencesType))
        {
            return;
        }

        if (string.Equals(
                linkedLicencesType,
                "ImplicitBackLink",
                StringComparison.OrdinalIgnoreCase))
        {
            sql.AppendLine(
                """
                AND EXISTS
                (
                    SELECT 1
                    FROM licence_list_item_linked_licence linked_licence
                    INNER JOIN licence_list_item_link_location link_location
                        ON link_location.linked_licence_id =
                           linked_licence.linked_licence_id
                    WHERE linked_licence.licence_list_item_id =
                          licence_list_item.licence_list_item_id
                      AND link_location.direction = 'Incoming'
                )
                """);

            return;
        }

        parameters.Add(
            "LinkedLicencesType",
            linkedLicencesType.Trim());

        sql.AppendLine(
            """
            AND EXISTS
            (
                SELECT 1
                FROM licence_list_item_linked_licence linked_licence
                INNER JOIN licence_list_item_link_location link_location
                    ON link_location.linked_licence_id =
                       linked_licence.linked_licence_id
                WHERE linked_licence.licence_list_item_id =
                      licence_list_item.licence_list_item_id
                  AND link_location.direction = 'Outgoing'
                  AND link_location.section_name =
                      @LinkedLicencesType
            )
            """);
    }
    
    public static void AddLicenceSetFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string? shortLicenceSetId)
    {
        if (string.IsNullOrWhiteSpace(shortLicenceSetId))
        {
            return;
        }

        sql.AppendLine(
            """
              AND EXISTS (
                  SELECT 1
                  FROM licence_list_item_licence_set licence_set
                  WHERE licence_set.process_run_id =
                        licence_list_item.process_run_id
                    AND licence_set.licence_list_item_id =
                        licence_list_item.licence_list_item_id
                    AND licence_set.short_licence_set_id =
                        @ShortLicenceSetId
              )
            """);

        parameters.Add(
            "ShortLicenceSetId",
            shortLicenceSetId.Trim());
    }
    
    public static void AddVerificationTypeFilter(
        StringBuilder sql,
        DynamicParameters parameters,
        string? verificationType)
    {
        if (string.IsNullOrWhiteSpace(verificationType))
        {
            return;
        }

        if (verificationType.Equals(
                "Flagged",
                StringComparison.OrdinalIgnoreCase))
        {
            sql.AppendLine(
                """
                  AND EXISTS (
                      SELECT 1
                      FROM licence_list_item_verification_section section
                      INNER JOIN licence_list_item_verification_item item
                          ON item.verification_section_id =
                             section.verification_section_id
                      WHERE section.licence_list_item_id =
                            licence_list_item.licence_list_item_id
                        AND item.scraped_data_is_different = true
                  )
                """);

            return;
        }

        sql.AppendLine(
            """
              AND EXISTS (
                  SELECT 1
                  FROM licence_list_item_verification_section section
                  INNER JOIN licence_list_item_verification_item item
                      ON item.verification_section_id =
                         section.verification_section_id
                  WHERE section.licence_list_item_id =
                        licence_list_item.licence_list_item_id
                    AND lower(item.current_verification_type) = lower(@VerificationType)
              )
            """);

        parameters.Add(
            "VerificationType",
            verificationType.Trim());
    }
    
    public static void AddOrdering(
        StringBuilder sql,
        string? sortField,
        bool sortAscending)
    {
        var column = sortField?.Trim().ToLowerInvariant() switch
        {
            "filename" => "filename",
            "licencenumber" => "licence_number",
            "licenceholder" => "licence_holder",
            "limitscount" => "limits_count",
            "aggregatescount" => "aggregates_count",
            "ocr" => "ocr",
            "issuedate" => "issue_date",
            "issueyear" => "issue_year",
            "issuer" => "issuer",
            "meansfound" => "means_found",
            "status" => "status",
            "purposescount" => "purposes_count",
            "pointscount" => "points_count",
            "linkedlicencescount" => "linked_licences_count",
            "licencesetscount" => "licence_sets_count",
            "verificationsectionscount" =>
                "verification_sections_count",
            "verificationitemscount" =>
                "verification_items_count",
            _ => "licence_list_item_id"
        };

        var direction = sortAscending
            ? "ASC"
            : "DESC";

        sql.AppendLine(
            $"""
             ORDER BY {column} {direction},
                      licence_list_item_id ASC
             """);
    }
}