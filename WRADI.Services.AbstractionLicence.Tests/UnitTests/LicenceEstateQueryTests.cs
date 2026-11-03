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

    /// What row_number() sees - the queue derivation and the filters both sit outside it.
    private static string RankedRows(string sql)
    {
        var start = sql.LastIndexOf("FROM (", StringComparison.Ordinal);
        var end = sql.IndexOf(") ranked", StringComparison.Ordinal);

        Assert.InRange(start, 0, int.MaxValue);
        Assert.InRange(end, start, int.MaxValue);

        return sql[start..end];
    }

    /// The filter clauses alone, so the comparison does not depend on the projection.
    private static string FilterClauses(string sql)
    {
        var start = sql.IndexOf(") AS licence_list_item", StringComparison.Ordinal);

        Assert.InRange(start, 0, int.MaxValue);

        var clauses = sql[start..];
        var tail = clauses.IndexOf("ORDER BY", StringComparison.Ordinal);

        return tail < 0 ? clauses : clauses[..tail];
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

        Assert.Equal(FilterClauses(page.Sql), FilterClauses(count.Sql));

        Assert.Empty(
            count.Parameters.ParameterNames.Except(page.Parameters.ParameterNames));
    }

    [Fact]
    public void WhenDerivingTheQueue_ThenTheBranchesRunWorstFirst()
    {
        var page = PostgresAbstractionLicenceReadService
            .BuildLicenceEstatePageQuery(new LicenceEstateQuery())
            .Sql;

        var branches = Regex
            .Matches(page, @"THEN (\d)")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToArray();

        // 8 is the ELSE, which is why it is absent here rather than last.
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 9], branches);
        Assert.Contains("ELSE 8", page);
    }

    [Fact]
    public void WhenCountingTheQueues_ThenTheQueueSelectionIsNotApplied()
    {
        var query = new LicenceEstateQuery { Queue = 5, ProjectedStatus = "Live" };

        var page = PostgresAbstractionLicenceReadService
            .BuildLicenceEstatePageQuery(query).Sql;

        var counts = PostgresAbstractionLicenceReadService
            .BuildLicenceEstateQueueCountsQuery(query).Sql;

        Assert.Contains("queue = @Queue", page);
        Assert.DoesNotContain("queue = @Queue", counts);

        Assert.Contains("status = @ProjectedStatus", page);
        Assert.Contains("status = @ProjectedStatus", counts);
    }
}