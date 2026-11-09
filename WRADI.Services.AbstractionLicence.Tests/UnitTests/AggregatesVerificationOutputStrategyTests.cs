using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Core.AbstractionLicence.Strategies;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class AggregatesVerificationOutputStrategyTests
{
    private const string LicenceNumber = "03/28/60/0130";

    private static readonly Guid FileId = Guid.NewGuid();

    [Fact]
    public void WhenThereAreNoVerifications_ThenScrapedAggregatesAreKept()
    {
        // Arrange
        var scraped = CreateAggregate(PrimaryType.InLicence);
        var listRow = CreateListRow(scraped);

        // Act
        HandleVerifications(listRow);

        // Assert
        Assert.Equal([PrimaryType.InLicence], GetPrimaryTypes(listRow));
    }

    [Fact]
    public void WhenAggregateIsRemoved_ThenItIsDropped()
    {
        // Arrange
        var scraped = CreateAggregate(PrimaryType.InLicence);
        var listRow = CreateListRow(scraped);

        // Act
        HandleVerifications(listRow, CreateVerification("Removed", scraped.Id, scraped, scraped));

        // Assert
        Assert.Empty(GetPrimaryTypes(listRow));
        Assert.Empty(listRow.aggregateIds!);
    }

    [Fact]
    public void WhenAggregateIsAdded_ThenItsTypeIsPresent()
    {
        // Arrange
        var scraped = CreateAggregate(PrimaryType.InLicence);
        var added = CreateAggregate(PrimaryType.LicenceToLicence);
        var listRow = CreateListRow(scraped);

        // Act
        HandleVerifications(listRow, CreateVerification("Added", added.Id, added));

        // Assert
        Assert.Equal([PrimaryType.InLicence, PrimaryType.LicenceToLicence], GetPrimaryTypes(listRow));
    }

    [Fact]
    public void WhenTypeChangeIsSavedAsRemovedAndAdded_ThenOnlyTheNewTypeRemains()
    {
        // Arrange
        var scraped = CreateAggregate(PrimaryType.InLicence);
        var changed = CreateAggregate(PrimaryType.LicenceToLicence);
        var listRow = CreateListRow(scraped);

        // Act
        HandleVerifications(
            listRow,
            CreateVerification("Removed", scraped.Id, scraped, scraped),
            CreateVerification("Added", changed.Id, changed, createdOffsetMinutes: 1));

        // Assert
        Assert.Equal([PrimaryType.LicenceToLicence], GetPrimaryTypes(listRow));
        Assert.Equal([changed.Id], listRow.aggregateIds!);
    }

    [Fact]
    public void WhenEditKeepsTheIdButChangesTheType_ThenTheTypeComesFromTheVerification()
    {
        // Arrange
        var scraped = CreateAggregate(PrimaryType.InLicence);
        var edited = CreateAggregate(PrimaryType.LicenceToLicence);
        var listRow = CreateListRow(scraped);

        // Act
        HandleVerifications(listRow, CreateVerification("Edited", scraped.Id, edited, scraped));

        // Assert
        Assert.Equal([PrimaryType.LicenceToLicence], GetPrimaryTypes(listRow));
    }

    private static Aggregate CreateAggregate(PrimaryType primaryType)
    {
        return new Aggregate
        {
            SourceLicenceNumber = LicenceNumber,
            SourceLicenceVersionId = "LV20190619",
            PrimaryType = primaryType,
            SubType = SubType.PointToPoint
        };
    }

    private static OutputListDataItem CreateListRow(params Aggregate[] scraped)
    {
        return new OutputListDataItem
        {
            aggregateIds = scraped.Select(a => a.Id).ToArray(),
            aggregates = scraped
        };
    }

    private static LicenceSectionVerification CreateVerification(
        string verificationType,
        string itemId,
        Aggregate value,
        Aggregate? scrapedValue = null,
        int createdOffsetMinutes = 0)
    {
        return new LicenceSectionVerification
        {
            VerificationType = verificationType,
            LicenceSectionItemId = itemId,
            LicenceSectionOverrideValue = JsonSerializer.Serialize(value, JsonHelper.GetSerializerOptions()),
            LicenceSectionScrapedValue = scrapedValue == null
                ? null
                : JsonSerializer.Serialize(scrapedValue, JsonHelper.GetSerializerOptions()),
            CreatedDateTimeUtc = DateTime.UtcNow.AddMinutes(createdOffsetMinutes)
        };
    }

    private static void HandleVerifications(
        OutputListDataItem listRow,
        params LicenceSectionVerification[] verifications)
    {
        var lookups = new LicenceVerificationLookups();

        if (verifications.Length > 0)
        {
            lookups.BySourceLicence[(FileId, LicenceNumber)] = verifications.ToList();
        }

        new AggregatesVerificationOutputStrategy().HandleVerifications(listRow, lookups, FileId, LicenceNumber);
    }

    private static PrimaryType[] GetPrimaryTypes(OutputListDataItem listRow)
    {
        return listRow.aggregates!.Select(a => a.PrimaryType).ToArray();
    }
}
