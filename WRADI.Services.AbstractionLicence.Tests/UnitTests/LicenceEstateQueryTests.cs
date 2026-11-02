using System.Text.RegularExpressions;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class LicenceEstateQueryTests
{
    /// These pin the SQL the builders emit, not the rows it returns - no test project here
    /// reaches a database.

    private static LicenceEstateQuery EveryFilter() =>
        new()
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
            LicenceNumbers = ["01/117"],
            ProjectedStatus = "Live"
        };

    private static string RankedRows(string sql)
    {
        var start = sql.IndexOf("FROM (", StringComparison.Ordinal);
        var end = sql.IndexOf(") AS licence_list_item", StringComparison.Ordinal);

        Assert.InRange(start, 0, int.MaxValue);
        Assert.InRange(end, start, int.MaxValue);

        return sql[start..end];
    }

    [Fact]
    public void WhenRankingTheEstate_ThenThePartitionIsLicenceNumberAlone()
    {
        var ranked = RankedRows(
            PostgresAbstractionLicenceReadService
                .BuildLicenceEstatePageQuery(new LicenceEstateQuery())
                .Sql);

        Assert.Equal(
            "lower(licence_number)",
            Regex.Match(ranked, @"PARTITION BY (.+)").Groups[1].Value.Trim());

        Assert.Equal(
            "process_run_id DESC",
            Regex.Match(ranked, @"ORDER BY (.+)").Groups[1].Value.Trim());
    }

    [Fact]
    public void WhenFilteringTheEstate_ThenNothingNarrowsTheRankedRows()
    {
        var page = PostgresAbstractionLicenceReadService
            .BuildLicenceEstatePageQuery(EveryFilter());

        var ranked = RankedRows(page.Sql);

        Assert.DoesNotContain("@", ranked);
        Assert.DoesNotMatch(@"(?i)\b(and|where)\b", ranked);
    }

    [Fact]
    public void WhenPinningToARun_ThenTheRunNarrowsTheRankedRows()
    {
        var page = PostgresAbstractionLicenceReadService
            .BuildLicenceEstatePageQuery(new LicenceEstateQuery { ProcessRunId = 134 });

        Assert.Contains("@ProcessRunId", RankedRows(page.Sql));
    }

    [Fact]
    public void WhenCountingAndSelectingTheEstate_ThenTheCountAppliesEveryFilterThePageDoes()
    {
        var query = EveryFilter();

        var page = PostgresAbstractionLicenceReadService.BuildLicenceEstatePageQuery(query);
        var count = PostgresAbstractionLicenceReadService.BuildLicenceEstateCountQuery(query);

        var pageAsCount = page.Sql.Replace(
            PostgresAbstractionLicenceReadService.LicenceListItemColumns,
            "COUNT(*)");

        Assert.StartsWith(count.Sql, pageAsCount, StringComparison.Ordinal);

        var orderingAndPaging = pageAsCount[count.Sql.Length..];

        Assert.DoesNotMatch(@"(?i)\b(and|where)\b", orderingAndPaging);

        Assert.Empty(
            count.Parameters.ParameterNames.Except(page.Parameters.ParameterNames));
    }
}
