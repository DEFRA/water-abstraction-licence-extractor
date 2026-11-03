using System.Text.RegularExpressions;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class LicenceRunHistoryQueryTests
{
    /// A name that does not match fails in Postgres, not in the build.
    [Fact]
    public void WhenAQueryDerivesTheQueue_ThenItSuppliesTheNameTheDerivationCorrelatesOn()
    {
        var runHistory = PostgresAbstractionLicenceReadService
            .BuildLicenceRunHistoryQuery("01/117").Sql;

        // Filters that correlate, or the estate query declares no alias but its own and the
        // check passes without testing anything.
        var estate = PostgresAbstractionLicenceReadService
            .BuildLicenceEstatePageQuery(
                new LicenceEstateQuery
                {
                    ShortLicenceSetId = "GR-GR-G",
                    LinkedLicencesType = "NaldOnly",
                    VerificationType = "NoVerifications"
                })
            .Sql;

        foreach (var sql in new[] { runHistory, estate })
        {
            var correlations = Regex
                .Matches(sql, @"=\s*(\w+)\.licence_list_item_id")
                .Select(match => match.Groups[1].Value)
                .Distinct();

            foreach (var alias in correlations)
            {
                // Matched as a declaration, not a bare name: the estate's alias shadows the
                // table it wraps, so the name appears either way.
                Assert.Matches(
                    $@"(?:\)\s*(?:AS\s+)?|FROM\s+\w+\s+){alias}\b",
                    sql);
            }
        }
    }

    [Fact]
    public void WhenReadingARunHistory_ThenTheNewestRunComesFirst()
    {
        var sql = PostgresAbstractionLicenceReadService
            .BuildLicenceRunHistoryQuery("01/117").Sql;

        Assert.Contains("ORDER BY process_run_id DESC", sql);
    }

    /// An unlowered parameter matches nothing rather than failing.
    [Fact]
    public void WhenReadingARunHistory_ThenTheLicenceNumberIsLowered()
    {
        var query = PostgresAbstractionLicenceReadService
            .BuildLicenceRunHistoryQuery("  NE/027/0022/049  ");

        Assert.Contains("lower(licence_number) = @LicenceNumber", query.Sql);

        Assert.Equal(
            "ne/027/0022/049",
            ((Dapper.SqlMapper.IParameterLookup)query.Parameters)["LicenceNumber"]);
    }
}
