using System.Reflection;
using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class LicenceListItemQueryTests
{
    /// search_text is only ever a predicate, nothing writes licence_list_item.source_data, and
    /// Queue, DocumentCount and RunCount are derived by the estate query, not stored.
    private static readonly string[] DeliberatelyNotSelected =
    [
        "SearchText",
        "SourceData",
        "Queue",
        "DocumentCount",
        "RunCount"
    ];

    /// A column missing from the SELECT leaves its property at the default - Dapper does not fail.
    [Fact]
    public void WhenSelectingLicenceListItems_ThenEveryMappedPropertyHasAColumn()
    {
        var aliases = Regex
            .Matches(
                PostgresAbstractionLicenceReadService.LicenceListItemColumns,
                @"\bAS\s+(\w+)",
                RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value)
            .OrderBy(alias => alias)
            .ToArray();

        var expected = typeof(LicenceListItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Except(DeliberatelyNotSelected)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(expected, aliases);
    }

    [Fact]
    public void WhenCountingAndSelectingTheSameQuery_ThenTheCountAppliesEveryFilterThePageDoes()
    {
        var query = new ProcessRunQuery
        {
            SearchTerm = "9/40",
            Issuer = "Environment Agency",
            OcrScan = true,
            MeansFound = false,
            IsLicenceNumberFlagged = true,
            IsIssueDateFlagged = true,
            LimitsEmpty = true,
            PointsEmpty = true,
            PurposesEmpty = true,
            IssueYear = 1965,
            AggregatesFilter = "true_false",
            ShortLicenceSetId = "GR-GR-G",
            LinkedLicencesType = "NaldOnly",
            VerificationType = "NoVerifications",
            LicenceNumbers = ["01/117"]
        };

        var page = PostgresAbstractionLicenceReadService.BuildLicenceListItemPageQuery(
            175,
            query);

        var count = PostgresAbstractionLicenceReadService.BuildLicenceListItemCountQuery(
            175,
            query);

        var pageAsCount = page.Sql.Replace(
            PostgresAbstractionLicenceReadService.LicenceListItemColumns,
            "COUNT(*)");

        Assert.StartsWith(count.Sql, pageAsCount, StringComparison.Ordinal);

        var orderingAndPaging = pageAsCount[count.Sql.Length..];

        Assert.DoesNotMatch(@"(?i)\b(and|where)\b", orderingAndPaging);

        Assert.Empty(
            count.Parameters.ParameterNames.Except(page.Parameters.ParameterNames));
    }

    [Theory]
    [InlineData(true, "ASC NULLS FIRST")]
    [InlineData(false, "DESC NULLS LAST")]
    public void WhenSortingByVerified_ThenOrdersByLatestNonDeletedVerificationWithUnverifiedAsOldest(
        bool sortAscending,
        string expectedDirection)
    {
        var query = new ProcessRunQuery
        {
            SortField = "latestLicenceSectionVerifications",
            SortAscending = sortAscending
        };

        var page = PostgresAbstractionLicenceReadService.BuildLicenceListItemPageQuery(
            175,
            query);

        var ordering = page.Sql[page.Sql.IndexOf("ORDER BY", StringComparison.Ordinal)..];

        Assert.Contains("max(v.created_date_time_utc)", ordering);
        Assert.Contains("v.deleted_date_time_utc IS NULL", ordering);
        Assert.Contains("v.process_run_id <= licence_list_item.process_run_id", ordering);
        Assert.Contains($") {expectedDirection},", ordering);
    }
}
