using WALE.ProcessFile.Database.PostgreSQL.Services;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Database.PostgreSQL.AbstractionLicence.Services;
using WRADI.DocumentType.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Services;
using WRADI.Services.Cache.AbstractionLicence;
using WRADI.Services.Output.AbstractionLicence;

namespace WRADI.Services.AbstractionLicence.Tests.IntegrationTests.RealNaldData;

public class RealNaldDataNaldPurposesHelperTests
{
    private static readonly INaldDataLookupService NaldDataLookupService;
    
    private static IAbstractionLicenceDatabaseReadService ReadService =>
        new PostgresAbstractionLicenceReadService(NpgsqlDataSourceProvider);

    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider =
        new(TestConfig.PostgresHost,
            TestConfig.PostgresPort,
            TestConfig.PostgresDbName,
            TestConfig.PostgresUsername,
            TestConfig.PostgresPassword);

    static RealNaldDataNaldPurposesHelperTests()
    {
        var realAbsLicCacheService = new DatabaseAbstractionLicenceCacheService(
            ReadService,
            null!);
        
        var realAbsLicOutputService = new DatabaseAbstractionLicenceOutputService(null!, ReadService, null!, null!);
        NaldDataLookupService = new NaldDataLookupService(realAbsLicCacheService, realAbsLicOutputService);
    }
    
    [Fact]
    public async Task WhenX1_ThenY()
    {
        // From 1.3-licence-07.02.2023
        
        var naldAbstractionDataLine = await NaldDataLookupService.GetNaldAbstractionDataLineAsync(
            "SW/047/0051/003",
            5);
        
        var naldPurposesForLicence =
            WRADI.DocumentType.AbstractionLicence.Services.NaldDataLookupService.ToNaldPurposeData(
                naldAbstractionDataLine?.Purposes);
        
        Assert.Single(naldPurposesForLicence);
        
        var documentDescription =
            "Transfer for the purpose of filling a reservoir for subsequent abstraction for public water supply.";
        
        var (relevantNaldPurposes, matchType) = await NaldDataLookupService.GetRelevantNaldPurposesAsync(
            naldPurposesForLicence,
            documentDescription,
            [],
            "[DOENSNT_MATTER]",
            false);

        Assert.Equal("OnlyOne", matchType);
        Assert.Single(relevantNaldPurposes);
        Assert.Equal("W", relevantNaldPurposes[0].PrimaryCategoryCode);
        Assert.Equal("WAT", relevantNaldPurposes[0].SecondaryCategoryCode);
        Assert.Equal(650, relevantNaldPurposes[0].UseCode);
        Assert.Equal("W-WAT-650", relevantNaldPurposes[0].CombinedCode);
        Assert.Equal("Water Supply", relevantNaldPurposes[0].PrimaryCategoryDescription);
        Assert.Equal("Water Supply Related", relevantNaldPurposes[0].SecondaryCategoryDescription);
        Assert.Equal("Transfer Between Sources (Post Water Act 2003)", relevantNaldPurposes[0].UseDescription);
        Assert.Equal("6000000_40000_2000_556", relevantNaldPurposes[0].QuantityIdentifier);
        Assert.Equal("10082040", relevantNaldPurposes[0].Id);
    }
    
    [Fact]
    public async Task WhenX2_ThenY()
    {
        // From 12100073R01__Application - New - Issued Licence 31.03.2015 8814302.pdf
        
        var naldAbstractionDataLine = await NaldDataLookupService.GetNaldAbstractionDataLineAsync(
            "1/21/00/073/R01",
            5);
        
        var naldPurposesForLicence =
            WRADI.DocumentType.AbstractionLicence.Services.NaldDataLookupService.ToNaldPurposeData(
                naldAbstractionDataLine?.Purposes);
        
        Assert.Equal(2, naldPurposesForLicence.Count);
        
        var documentDescription1 = "Mineral washing.";
        
        var (relevantNaldPurposes1, matchType1) = await NaldDataLookupService.GetRelevantNaldPurposesAsync(
            naldPurposesForLicence,
            documentDescription1,
            [],
            "[DOENSNT_MATTER]",
            false);
        
        Assert.Equal("DescriptionMatchesDescription", matchType1);
        Assert.Single(relevantNaldPurposes1);
        Assert.Equal("I", relevantNaldPurposes1[0].PrimaryCategoryCode);
        Assert.Equal("EXT", relevantNaldPurposes1[0].SecondaryCategoryCode);
        Assert.Equal(300, relevantNaldPurposes1[0].UseCode);
        Assert.Equal("I-EXT-300", relevantNaldPurposes1[0].CombinedCode);
        Assert.Equal("Industrial, Commercial And Public Services", relevantNaldPurposes1[0].PrimaryCategoryDescription);
        Assert.Equal("Extractive", relevantNaldPurposes1[0].SecondaryCategoryDescription);
        Assert.Equal("Mineral Washing", relevantNaldPurposes1[0].UseDescription);
        Assert.Equal("13000_50_3_0.59", relevantNaldPurposes1[0].QuantityIdentifier);
        Assert.Equal("10083252", relevantNaldPurposes1[0].Id);
        
        var documentDescription2 = "Industrial (Groundwater augmentation).";
        
        var (relevantNaldPurposes2, matchType2) = await NaldDataLookupService.GetRelevantNaldPurposesAsync(
            naldPurposesForLicence,
            documentDescription2,
            [relevantNaldPurposes1[0].Id!],
            "[DOENSNT_MATTER]",
            false);
        
        Assert.Equal("OnlyOne", matchType2);
        Assert.Single(relevantNaldPurposes2);
        Assert.Equal("I", relevantNaldPurposes2[0].PrimaryCategoryCode);
        Assert.Equal("EXT", relevantNaldPurposes2[0].SecondaryCategoryCode);
        Assert.Equal(650, relevantNaldPurposes2[0].UseCode);
        Assert.Equal("I-EXT-650", relevantNaldPurposes2[0].CombinedCode);
        Assert.Equal("Industrial, Commercial And Public Services", relevantNaldPurposes2[0].PrimaryCategoryDescription);
        Assert.Equal("Extractive", relevantNaldPurposes2[0].SecondaryCategoryDescription);
        Assert.Equal("Transfer Between Sources (Post Water Act 2003)", relevantNaldPurposes2[0].UseDescription);
        Assert.Equal("13000_50_3_0.59", relevantNaldPurposes2[0].QuantityIdentifier);
    }
}