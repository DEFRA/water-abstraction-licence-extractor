using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Configuration;
using WRADI.DocumentType.AbstractionLicence.Converters;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class PdfPigNoOcrPdfTests
{
    [Fact]
    public async Task When3LicencesEachLinkingToEachOther_SameLicences_AllValuesMatch_ThenAllGood()
    {
        var licence1MatchesResult = GetLicenceMatchesResult(
            "1/01/01/001",
            "30 cubic meters per day in aggregate with 1/01/01/002 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/002", "1/01/01/003"]);
        
        var licence2MatchesResult = GetLicenceMatchesResult(
            "1/01/01/002",
            "30 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/003"]);
        
        var licence3MatchesResult = GetLicenceMatchesResult(
            "1/01/01/003",
            "30 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/002",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/002"]);

        var data = new Dictionary<string, MatchesResult>
        {
            { "1/01/01/002", licence2MatchesResult },
            { "1/01/01/003", licence3MatchesResult }
        };
        
        var lookupConfig = new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            [],
            null,
            new TestCacheService(),
            null,
            new TestLicenceNumberServiceCore(),
            null,
            null,
            new TestDmsLookupService(),
            -1,
            DateTime.UtcNow);

        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            licence1MatchesResult,
            new TestPdfDataExtractorService(data),
            -1,
            lookupConfig,
            null,
            new TestNaldDataLookupService(),
            dmsDataForFile: new DmsFileData());
        
        // Check aggregate sets in the big group
        const string expectedLicenceSetId = "10101001-10101002-10101003";
        
        Assert.NotNull(licenceSets);
        Assert.Single(licenceSets);
        Assert.Equal(expectedLicenceSetId, licenceSets[0].LicenceSetId);
        
        // Aggregate licence set group
        Assert.Equal(3, licenceSets[0].Licences.Length);

        // Check licence limits in the big group
        var licence1 = licenceSets[0].Licences[0];
        Assert.Equal("1/01/01/001", licence1.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence1.LinkedLicences.Length);
        Assert.Equal("1/01/01/002", licence1.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence1.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[1].ContainedIn![1].Direction);
        
        Assert.NotNull(licence1.AbstractionLimits.Aggregates);
        Assert.Single(licence1.AbstractionLimits.Aggregates);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0]);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence1.AbstractionLimits.Aggregates[0].Id);
        Assert.False(licence1.AbstractionLimits.Aggregates[0].ContainsLegalText);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/002", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/003", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence1.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence1.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence1.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licence1.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.Equal("Different source licence / different order", licence1.AbstractionLimits.Aggregates[0].Variations![0].Difference);
        Assert.Equal("Different source licence / different order", licence1.AbstractionLimits.Aggregates[0].Variations![1].Difference);
        Assert.Single(licence1.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence1.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence1.LicenceSets[0].LicenceSetType);
        
        var licence2 = licenceSets[0].Licences[1];
        Assert.Equal("1/01/01/002", licence2.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence2.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence2.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence2.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[1].ContainedIn![1].Direction);
        
        Assert.NotNull(licence2.AbstractionLimits.Aggregates);
        Assert.Single(licence2.AbstractionLimits.Aggregates);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0]);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence2.AbstractionLimits.Aggregates[0].Id);
        Assert.False(licence2.AbstractionLimits.Aggregates[0].ContainsLegalText);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence2.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/003", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence2.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence2.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence2.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(2, licence2.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence2.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licence2.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.Equal("Different source licence / different order", licence2.AbstractionLimits.Aggregates[0].Variations![0].Difference);
        Assert.Equal("Different source licence / different order", licence2.AbstractionLimits.Aggregates[0].Variations![1].Difference);
        Assert.Equal(expectedLicenceSetId, licence2.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence2.LicenceSets[0].LicenceSetType);
        
        var licence3 = licenceSets[0].Licences[2];
        Assert.Equal("1/01/01/003", licence3.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence3.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence3.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/002", licence3.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[1].ContainedIn![1].Direction);

        Assert.NotNull(licence3.AbstractionLimits.Aggregates);
        Assert.Single(licence3.AbstractionLimits.Aggregates);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0]);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licence3.AbstractionLimits.Aggregates[0].Id);
        Assert.False(licence3.AbstractionLimits.Aggregates[0].ContainsLegalText);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence3.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/002", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence3.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence3.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(2, licence3.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence3.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence3.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.Equal("Different source licence / different order", licence3.AbstractionLimits.Aggregates[0].Variations![0].Difference);
        Assert.Equal("Different source licence / different order", licence3.AbstractionLimits.Aggregates[0].Variations![1].Difference);
        Assert.Equal(expectedLicenceSetId, licence3.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence3.LicenceSets[0].LicenceSetType);
        
        // Check aggregate sets
        Assert.NotNull(licenceSets[0].AggregateSets!);
        Assert.Single(licenceSets[0].AggregateSets!);
        Assert.Single(licenceSets[0].AggregateSets!);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licenceSets[0].AggregateSets![0].Id);
        Assert.Equal("1/01/01/001", licenceSets[0].AggregateSets![0].SourceLicenceNumber);
        Assert.False(licenceSets[0].AggregateSets![0].ContainsLegalText);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Limits[0].Value);
        Assert.NotNull(licenceSets[0].AggregateSets![0].Variations);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].Variations!.Length);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licenceSets[0].AggregateSets![0].Variations![0].Id);
        Assert.Equal("1/01/01/002", licenceSets[0].AggregateSets![0].Variations![0].SourceLicenceNumber);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].Variations![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Variations![0].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Variations![0].Limits[0].Value);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licenceSets[0].AggregateSets![0].Variations![1].Id);
        Assert.Equal("1/01/01/003", licenceSets[0].AggregateSets![0].Variations![1].SourceLicenceNumber);
        Assert.False(licenceSets[0].AggregateSets![0].Variations![1].ContainsLegalText);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].Variations![1].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Variations![1].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Variations![1].Limits[0].Value);
    }
    
    [Fact]
    public async Task When3LicencesEachLinkingToEachOther_SameLicencesWithLegalText_AllValuesMatch_ThenAllGood()
    {
        var licence1MatchesResult = GetLicenceMatchesResult(
            "1/01/01/001",
            "30 cubic meters per day in aggregate with 1/01/01/002 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/002", "1/01/01/003"],
            true);
        
        var licence2MatchesResult = GetLicenceMatchesResult(
            "1/01/01/002",
            "30 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/003"],
            true);
        
        var licence3MatchesResult = GetLicenceMatchesResult(
            "1/01/01/003",
            "30 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/002",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/002"],
            true);

        var data = new Dictionary<string, MatchesResult>
        {
            { "1/01/01/002", licence2MatchesResult },
            { "1/01/01/003", licence3MatchesResult }
        };
        
        var lookupConfig = new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            [],
            null,
            new TestCacheService(),
            null,
            new TestLicenceNumberServiceCore(),
            null,
            null,
            new TestDmsLookupService(),
            -1,
            DateTime.UtcNow);

        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            licence1MatchesResult,
            new TestPdfDataExtractorService(data),
            -1,
            lookupConfig,
            null,
            new TestNaldDataLookupService(),
            dmsDataForFile: new DmsFileData());
        
        // Check aggregate sets in the big group
        const string expectedLicenceSetId = "10101001-10101002-10101003";
        
        Assert.NotNull(licenceSets);
        Assert.Single(licenceSets);
        Assert.Equal(expectedLicenceSetId, licenceSets[0].LicenceSetId);

        // Aggregate licence set group
        Assert.Equal(3, licenceSets[0].Licences.Length);

        // Check licence limits in the big group
        var licence1 = licenceSets[0].Licences[0];
        Assert.Equal("1/01/01/001", licence1.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence1.LinkedLicences.Length);
        Assert.Equal("1/01/01/002", licence1.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence1.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[1].ContainedIn![1].Direction);

        Assert.NotNull(licence1.AbstractionLimits.Aggregates);
        Assert.Single(licence1.AbstractionLimits.Aggregates);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0]);
        Assert.True(licence1.AbstractionLimits.Aggregates[0].ContainsLegalText);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/002", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/003", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence1.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence1.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence1.AbstractionLimits.Aggregates[0].Id);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence1.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licence1.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.Equal("Different source licence / different order", licence1.AbstractionLimits.Aggregates[0].Variations![0].Difference);
        Assert.Equal("Different source licence / different order", licence1.AbstractionLimits.Aggregates[0].Variations![1].Difference);
        Assert.Single(licence1.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence1.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence1.LicenceSets[0].LicenceSetType);
        
        var licence2 = licenceSets[0].Licences[1];
        Assert.Equal("1/01/01/002", licence2.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence2.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence2.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence2.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[1].ContainedIn![1].Direction);

        Assert.NotNull(licence2.AbstractionLimits.Aggregates);
        Assert.Single(licence2.AbstractionLimits.Aggregates);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0]);
        Assert.True(licence2.AbstractionLimits.Aggregates[0].ContainsLegalText);
        Assert.Single(licence2.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence2.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence2.LicenceSets[0].LicenceSetType);
        
        var licence3 = licenceSets[0].Licences[2];
        Assert.Equal("1/01/01/003", licence3.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence3.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence3.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/002", licence3.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[1].ContainedIn![1].Direction);

        Assert.NotNull(licence3.AbstractionLimits.Aggregates);
        Assert.Single(licence3.AbstractionLimits.Aggregates);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0]);
        Assert.True(licence3.AbstractionLimits.Aggregates[0].ContainsLegalText);
        Assert.Single(licence3.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence3.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence3.LicenceSets[0].LicenceSetType);
        
        // Check aggregate sets
        Assert.NotNull(licenceSets[0].AggregateSets!);
        Assert.Single(licenceSets[0].AggregateSets!);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licenceSets[0].AggregateSets![0].Id);
        Assert.Equal("1/01/01/001", licenceSets[0].AggregateSets![0].SourceLicenceNumber);
        Assert.True(licenceSets[0].AggregateSets![0].ContainsLegalText);
    }
    
    [Fact]
    public async Task When3LicencesEachLinkingToEachOther_SameLicences_1ValueDoesNotMatch_ThenSingleLicenceSetWithSingleAggregateSetAnd2AggregatesMade()
    {
        var licence1MatchesResult = GetLicenceMatchesResult(
            "1/01/01/001",
            "30 cubic meters per day in aggregate with 1/01/01/002 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/002", "1/01/01/003"]);
        
        var licence2MatchesResult = GetLicenceMatchesResult(
            "1/01/01/002",
            "30 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/003"]);
        
        var licence3MatchesResult = GetLicenceMatchesResult(
            "1/01/01/003",
            "60 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/002",
            "60",
            "cubic meters",
            ["1/01/01/001", "1/01/01/002"]);

        var data = new Dictionary<string, MatchesResult>
        {
            { "1/01/01/002", licence2MatchesResult },
            { "1/01/01/003", licence3MatchesResult }
        };
        
        var lookupConfig = new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            [],
            null,
            new TestCacheService(),
            null,
            new TestLicenceNumberServiceCore(),
            null,
            null,
            new TestDmsLookupService(),
            -1,
            DateTime.UtcNow);

        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            licence1MatchesResult,
            new TestPdfDataExtractorService(data),
            -1,
            lookupConfig,
            null,
            new TestNaldDataLookupService(),
            dmsDataForFile: new DmsFileData());

        const string expectedLicenceSetId = "10101001-10101002-10101003";
        
        // Check aggregate sets in the big group
        Assert.NotNull(licenceSets);
        Assert.Single(licenceSets);
        Assert.Equal(expectedLicenceSetId, licenceSets[0].LicenceSetId);

        Assert.Equal(3, licenceSets[0].Licences.Length); // Aggregate licence set group

        var licence1 = licenceSets[0].Licences[0];
        Assert.Equal("1/01/01/001", licence1.LicenceNumber!.Value);
        
        Assert.Equal(2, licence1.LinkedLicences.Length);
        Assert.Equal("1/01/01/002", licence1.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence1.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[1].ContainedIn![1].Direction);
        
        Assert.NotNull(licence1.AbstractionLimits.Aggregates);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates.Length);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0]);
        Assert.False(licence1.AbstractionLimits.Aggregates[0].ContainsLegalText);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/002", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/003", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence1.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence1.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Single(licence1.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence1.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence1.LicenceSets[0].LicenceSetType);
        
        var licence2 = licenceSets[0].Licences[1];
        Assert.Equal("1/01/01/002", licence2.LicenceNumber!.Value);
        
        Assert.Equal(2, licence2.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence2.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence2.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[1].ContainedIn![1].Direction);

        Assert.NotNull(licence2.AbstractionLimits.Aggregates);
        Assert.Equal(2, licence2.AbstractionLimits.Aggregates.Length);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0]);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence2.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/003", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence2.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence2.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence2.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Single(licence2.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence2.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence2.LicenceSets[0].LicenceSetType);
        
        var licence3 = licenceSets[0].Licences[2];
        Assert.Equal("1/01/01/003", licence3.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence3.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence3.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/002", licence3.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[0].ContainedIn![1].Direction);

        Assert.NotNull(licence3.AbstractionLimits.Aggregates);
        Assert.Equal(2, licence3.AbstractionLimits.Aggregates.Length);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0]);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence3.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/002", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence3.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(60, licence3.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Single(licence3.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence3.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence3.LicenceSets[0].LicenceSetType);
        
        // Check aggregate sets
        Assert.NotNull(licenceSets[0].AggregateSets!);
        Assert.Equal(2, licenceSets[0].AggregateSets!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licenceSets[0].AggregateSets![0].Id);
        Assert.Equal("1/01/01/001", licenceSets[0].AggregateSets![0].SourceLicenceNumber);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Limits[0].Value);
        Assert.NotNull(licenceSets[0].AggregateSets![0].Variations);
        Assert.Single(licenceSets[0].AggregateSets![0].Variations!);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licenceSets[0].AggregateSets![0].Variations![0].Id);
        Assert.Equal("1/01/01/002", licenceSets[0].AggregateSets![0].Variations![0].SourceLicenceNumber);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].Variations![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Variations![0].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Variations![0].Limits[0].Value);
        
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C60", licenceSets[0].AggregateSets![1].Id);
        Assert.Equal("1/01/01/003", licenceSets[0].AggregateSets![1].SourceLicenceNumber);
        Assert.Equal(2, licenceSets[0].AggregateSets![1].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![1].Limits);
        Assert.Equal(60, licenceSets[0].AggregateSets![1].Limits[0].Value);
        Assert.Null(licenceSets[0].AggregateSets![1].Variations);
    }
    
    [Fact]
    public async Task When3LicencesEachLinkingToEachOther_1HasAnExtraLicence_ValuesMatch_ThenSupersetGroupIsUsedWithVariations()
    {
        var licence1MatchesResult = GetLicenceMatchesResult(
            "1/01/01/001",
            "30 cubic meters per day in aggregate with 1/01/01/002 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/002", "1/01/01/003"],
            true);
        
        var licence2MatchesResult = GetLicenceMatchesResult(
            "1/01/01/002",
            "30 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/003"],
            true);
        
        var licence3MatchesResult = GetLicenceMatchesResult(
            "1/01/01/003",
            "30 cubic meters per day in aggregate with 1/01/01/001, 1/01/01/002 and 1/01/01/004",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/002", "1/01/01/004"],
            true);
        
        var licence4MatchesResult = GetLicenceMatchesResult(
            "1/01/01/004",
            null,
            null,
            null,
            []);

        var data = new Dictionary<string, MatchesResult>
        {
            { "1/01/01/002", licence2MatchesResult },
            { "1/01/01/003", licence3MatchesResult },
            { "1/01/01/004", licence4MatchesResult }
        };
        
        var lookupConfig = new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            [],
            null,
            new TestCacheService(),
            null,
            new TestLicenceNumberServiceCore(),
            null,
            null,
            new TestDmsLookupService(),
            -1,
            DateTime.UtcNow);

        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            licence1MatchesResult,
            new TestPdfDataExtractorService(data),
            -1,
            lookupConfig,
            null,
            new TestNaldDataLookupService(),
            dmsDataForFile: new DmsFileData());
        
        const string expectedLicenceSetId = "10101001-10101002-10101003-10101004";
        
        Assert.NotNull(licenceSets);
        Assert.Single(licenceSets);
        Assert.Equal(expectedLicenceSetId, licenceSets[0].LicenceSetId);
        
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licenceSets[0].LicenceSetType);
        Assert.Equal(4, licenceSets[0].Licences.Length);
        
        var licence1 = licenceSets[0].Licences[1];
        Assert.Equal("1/01/01/001", licence1.LicenceNumber!.Value);            
        
        Assert.Equal(3, licence1.LinkedLicences.Length);
        Assert.Equal("1/01/01/002", licence1.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence1.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[1].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/004", licence1.LinkedLicences[2].LicenceNumber);
        Assert.Single(licence1.LinkedLicences[2].ContainedIn!);
        Assert.Equal(InformationDirection.InSameGroup, licence1.LinkedLicences[2].ContainedIn![0].Direction);

        Assert.NotNull(licence1.AbstractionLimits.Aggregates);
        Assert.Single(licence1.AbstractionLimits.Aggregates);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0]);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-10101004-C30", licence1.AbstractionLimits.Aggregates[0].Id);
        Assert.True(licence1.AbstractionLimits.Aggregates[0].ContainsLegalText);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(3, licence1.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/002", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.Equal("1/01/01/004", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![2]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence1.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence1.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence1.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence1.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.Single(licence1.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence1.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence1.LicenceSets[0].LicenceSetType);
        
        var licence2 = licenceSets[0].Licences[2];
        Assert.Equal("1/01/01/002", licence2.LicenceNumber!.Value);            
        
        Assert.Equal(3, licence2.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence2.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence2.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[1].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/004", licence2.LinkedLicences[2].LicenceNumber);
        Assert.Single(licence2.LinkedLicences[2].ContainedIn!);
        Assert.Equal(InformationDirection.InSameGroup, licence2.LinkedLicences[2].ContainedIn![0].Direction);

        Assert.NotNull(licence2.AbstractionLimits.Aggregates);
        Assert.Single(licence2.AbstractionLimits.Aggregates);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0]);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-10101004-C30", licence2.AbstractionLimits.Aggregates[0].Id);
        Assert.Equal("1/01/01/003", licence2.AbstractionLimits.Aggregates[0].SourceLicenceNumber);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(3, licence2.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/002", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.Equal("1/01/01/004", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![2]);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence2.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence2.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence2.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(2, licence2.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence2.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence2.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.Single(licence2.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence2.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence2.LicenceSets[0].LicenceSetType);
        
        var licence3 = licenceSets[0].Licences[0];
        Assert.Equal("1/01/01/003", licence3.LicenceNumber!.Value);            
        
        Assert.Equal(3, licence3.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence3.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/002", licence3.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[1].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/004", licence3.LinkedLicences[2].LicenceNumber);
        Assert.Single(licence3.LinkedLicences[2].ContainedIn!);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[2].ContainedIn![0].Direction);

        Assert.NotNull(licence3.AbstractionLimits.Aggregates);
        Assert.Single(licence3.AbstractionLimits.Aggregates);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0]);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-10101004-C30", licence3.AbstractionLimits.Aggregates[0].Id);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(3, licence3.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/002", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.Equal("1/01/01/004", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![2]);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence3.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence3.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(2, licence3.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence3.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence3.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.Single(licence3.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence3.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence3.LicenceSets[0].LicenceSetType);
        
        // Check aggregate sets
        Assert.NotNull(licenceSets[0].AggregateSets!);
        Assert.Single(licenceSets[0].AggregateSets!);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-10101004-C30", licenceSets[0].AggregateSets![0].Id);
        Assert.True(licenceSets[0].AggregateSets![0].ContainsLegalText);
        Assert.Equal("1/01/01/003", licenceSets[0].AggregateSets![0].SourceLicenceNumber);
        Assert.Equal(3, licenceSets[0].AggregateSets![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Limits[0].Value);
        Assert.NotNull(licenceSets[0].AggregateSets![0].Variations);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].Variations!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licenceSets[0].AggregateSets![0].Variations![0].Id);
        Assert.Equal("1/01/01/001", licenceSets[0].AggregateSets![0].Variations![0].SourceLicenceNumber);
        Assert.True(licenceSets[0].AggregateSets![0].Variations![0].ContainsLegalText);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].Variations![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Variations![0].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Variations![0].Limits[0].Value);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licenceSets[0].AggregateSets![0].Variations![1].Id);
        Assert.Equal("1/01/01/002", licenceSets[0].AggregateSets![0].Variations![1].SourceLicenceNumber);
        Assert.True(licenceSets[0].AggregateSets![0].Variations![1].ContainsLegalText);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].Variations![1].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Variations![1].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Variations![1].Limits[0].Value);
    }
    
    [Fact]
    public async Task When3LicencesEachLinkingToEachOther_1HasAnExtraLicenceThatThenAlsoHasAnExtraLicence_ValuesMatch_ThenSupersetGroupIsUsedWithVariations()
    {
        var licence1MatchesResult = GetLicenceMatchesResult(
            "1/01/01/001",
            "30 cubic meters per day in aggregate with 1/01/01/002 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/002", "1/01/01/003"],
            true);
        
        var licence2MatchesResult = GetLicenceMatchesResult(
            "1/01/01/002",
            "30 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/003"],
            true);
        
        var licence3MatchesResult = GetLicenceMatchesResult(
            "1/01/01/003",
            "30 cubic meters per day in aggregate with 1/01/01/001, 1/01/01/002 and 1/01/01/004",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/002", "1/01/01/004"],
            true);
        
        var licence4MatchesResult = GetLicenceMatchesResult(
            "1/01/01/004",
            "30 cubic meters per day in aggregate with 1/01/01/001, 1/01/01/002, 1/01/01/003 and 1/01/01/005",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/002", "1/01/01/003", "1/01/01/005"],
            true);

        /*var licence5MatchesResult = GetLicenceMatchesResult(
            "1/01/01/005",
            null,
            null,
            null,
            []);*/
        
        var data = new Dictionary<string, MatchesResult>
        {
            { "1/01/01/002", licence2MatchesResult },
            { "1/01/01/003", licence3MatchesResult },
            { "1/01/01/004", licence4MatchesResult },
            //{ "1/01/01/005", licence5MatchesResult }
        };
        
        var lookupConfig = new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            [],
            null,
            new TestCacheService(),
            null,
            new TestLicenceNumberServiceCore(),
            null,
            null,
            new TestDmsLookupService(),
            -1,
            DateTime.UtcNow);

        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            licence1MatchesResult,
            new TestPdfDataExtractorService(data),
            -1,
            lookupConfig,
            null,
            new TestNaldDataLookupService(),
            dmsDataForFile: new DmsFileData());
        
        const string expectedLicenceSetId = "10101001-10101002-10101003-10101004-10101005";
        
        Assert.NotNull(licenceSets);
        Assert.Single(licenceSets);
        Assert.Equal(expectedLicenceSetId, licenceSets[0].LicenceSetId);
        
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licenceSets[0].LicenceSetType);
        Assert.Equal(5, licenceSets[0].Licences.Length);
        
        var licence1 = licenceSets[0].Licences[1];
        Assert.Equal("1/01/01/001", licence1.LicenceNumber!.Value);            
        
        Assert.Equal(4, licence1.LinkedLicences.Length);
        Assert.Equal("1/01/01/002", licence1.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence1.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[1].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/004", licence1.LinkedLicences[2].LicenceNumber);
        Assert.Single(licence1.LinkedLicences[2].ContainedIn!);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[2].ContainedIn![0].Direction);
        Assert.Equal("1/01/01/005", licence1.LinkedLicences[3].LicenceNumber);
        Assert.Single(licence1.LinkedLicences[3].ContainedIn!);
        Assert.Equal(InformationDirection.InSameGroup, licence1.LinkedLicences[3].ContainedIn![0].Direction);        

        Assert.NotNull(licence1.AbstractionLimits.Aggregates);
        Assert.Single(licence1.AbstractionLimits.Aggregates);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0]);
        Assert.Equal("10101004-LVUNKNOWN-LL-10101001-10101002-10101003-10101005-C30", licence1.AbstractionLimits.Aggregates[0].Id);
        Assert.Equal("1/01/01/004", licence1.AbstractionLimits.Aggregates[0].SourceLicenceNumber);
        Assert.True(licence1.AbstractionLimits.Aggregates[0].ContainsLegalText);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(4, licence1.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/002", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.Equal("1/01/01/003", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![2]);
        Assert.Equal("1/01/01/005", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![3]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence1.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence1.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(3, licence1.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence1.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence1.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-10101004-C30", licence1.AbstractionLimits.Aggregates[0].Variations![2].Id);
        Assert.Single(licence1.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence1.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence1.LicenceSets[0].LicenceSetType);
        
        var licence2 = licenceSets[0].Licences[2];
        Assert.Equal("1/01/01/002", licence2.LicenceNumber!.Value);            
        
        Assert.Equal(4, licence2.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence2.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence2.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[1].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/004", licence2.LinkedLicences[2].LicenceNumber);
        Assert.Single(licence2.LinkedLicences[2].ContainedIn!);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[2].ContainedIn![0].Direction);
        Assert.Equal("1/01/01/005", licence2.LinkedLicences[3].LicenceNumber);
        Assert.Single(licence2.LinkedLicences[3].ContainedIn!);
        Assert.Equal(InformationDirection.InSameGroup, licence2.LinkedLicences[3].ContainedIn![0].Direction);

        Assert.NotNull(licence2.AbstractionLimits.Aggregates);
        Assert.Single(licence2.AbstractionLimits.Aggregates);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0]);
        Assert.Equal("10101004-LVUNKNOWN-LL-10101001-10101002-10101003-10101005-C30", licence2.AbstractionLimits.Aggregates[0].Id);        
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(4, licence2.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/002", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.Equal("1/01/01/003", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![2]);
        Assert.Equal("1/01/01/005", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![3]);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence2.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence2.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence2.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(3, licence2.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence2.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence2.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-10101004-C30", licence2.AbstractionLimits.Aggregates[0].Variations![2].Id);
        Assert.Single(licence2.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence2.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence2.LicenceSets[0].LicenceSetType);
        
        var licence3 = licenceSets[0].Licences[3];
        Assert.Equal("1/01/01/003", licence3.LicenceNumber!.Value);            
        
        Assert.Equal(4, licence3.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence3.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/002", licence3.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[1].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/004", licence3.LinkedLicences[2].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[2].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[2].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[2].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/005", licence3.LinkedLicences[3].LicenceNumber);
        Assert.Single(licence3.LinkedLicences[3].ContainedIn!);
        Assert.Equal(InformationDirection.InSameGroup, licence3.LinkedLicences[3].ContainedIn![0].Direction);        

        Assert.NotNull(licence3.AbstractionLimits.Aggregates);
        Assert.Single(licence3.AbstractionLimits.Aggregates);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0]);
        Assert.Equal("10101004-LVUNKNOWN-LL-10101001-10101002-10101003-10101005-C30", licence3.AbstractionLimits.Aggregates[0].Id);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(4, licence3.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/002", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.Equal("1/01/01/003", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![2]);
        Assert.Equal("1/01/01/005", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![3]);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence3.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence3.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(3, licence3.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence3.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence3.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-10101004-C30", licence3.AbstractionLimits.Aggregates[0].Variations![2].Id);
        Assert.Single(licence3.LicenceSets);
        Assert.Equal(expectedLicenceSetId, licence3.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence3.LicenceSets[0].LicenceSetType);
        
        // Check aggregate sets
        Assert.NotNull(licenceSets[0].AggregateSets!);
        Assert.Single(licenceSets[0].AggregateSets!);
        Assert.Equal("10101004-LVUNKNOWN-LL-10101001-10101002-10101003-10101005-C30", licenceSets[0].AggregateSets![0].Id);
        Assert.True(licenceSets[0].AggregateSets![0].ContainsLegalText);
        Assert.Equal("1/01/01/004", licenceSets[0].AggregateSets![0].SourceLicenceNumber);
        Assert.Equal(4, licenceSets[0].AggregateSets![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Limits[0].Value);
        Assert.NotNull(licenceSets[0].AggregateSets![0].Variations);
        Assert.Equal(3, licenceSets[0].AggregateSets![0].Variations!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licenceSets[0].AggregateSets![0].Variations![0].Id);
        Assert.Equal("1/01/01/001", licenceSets[0].AggregateSets![0].Variations![0].SourceLicenceNumber);
        Assert.True(licenceSets[0].AggregateSets![0].Variations![0].ContainsLegalText);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].Variations![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Variations![0].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Variations![0].Limits[0].Value);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licenceSets[0].AggregateSets![0].Variations![1].Id);
        Assert.Equal("1/01/01/002", licenceSets[0].AggregateSets![0].Variations![1].SourceLicenceNumber);
        Assert.True(licenceSets[0].AggregateSets![0].Variations![1].ContainsLegalText);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].Variations![1].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Variations![1].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Variations![1].Limits[0].Value);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-10101004-C30", licenceSets[0].AggregateSets![0].Variations![2].Id);
        Assert.Equal("1/01/01/003", licenceSets[0].AggregateSets![0].Variations![2].SourceLicenceNumber);
        Assert.True(licenceSets[0].AggregateSets![0].Variations![2].ContainsLegalText);
        Assert.Equal(3, licenceSets[0].AggregateSets![0].Variations![2].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Variations![2].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Variations![2].Limits[0].Value);
    }
    
    [Fact]
    public async Task When2GroupsOf3LicencesEachLinkingToEachOther_Then2SeperateAggregateSetsMade()
    {
        var licence1MatchesResultPartA = GetLicenceMatchesResult(
            "1/01/01/001",
            "30 cubic meters per day in aggregate with 1/01/01/002 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/002", "1/01/01/003"],
            true);
        
        var licence1MatchesResultPartB = GetLicenceMatchesResult(
            "1/01/01/001",
            "60 cubic meters per day in aggregate with 1/01/01/004 and 1/01/01/005",
            "60",
            "cubic meters",
            ["1/01/01/004", "1/01/01/005"],
            true);

        var licence1Matches = licence1MatchesResultPartA.Matches!.ToList();
        var combinedLimitsSubResults = licence1Matches
            .Single(x => x.LabelGroupName == "AbstractionLimits")
            .SubResults
            .ToList();

        var newAbstractionLimits = licence1MatchesResultPartB
            .Matches!
            .Single(x => x.LabelGroupName == "AbstractionLimits");
        
        var newSubResults = newAbstractionLimits.SubResults;
        combinedLimitsSubResults.AddRange(newSubResults);
        
        var licence1AbstractionLimits = licence1Matches
            .Single(x => x.LabelGroupName == "AbstractionLimits");
            
        licence1AbstractionLimits.SubResults = combinedLimitsSubResults;

        var combinedText = licence1AbstractionLimits.Text!.ToList();
        combinedText.AddRange(newAbstractionLimits.Text!);

        licence1AbstractionLimits.Text = combinedText;
        
        var licence1MatchesResult = new MatchesResult
        {
            Matches = licence1Matches
        };

        var licence2MatchesResult = GetLicenceMatchesResult(
            "1/01/01/002",
            "30 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/003"],
            true);
        
        var licence3MatchesResult = GetLicenceMatchesResult(
            "1/01/01/003",
            "30 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/002",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/002"],
            true);
        
        var licence4MatchesResult = GetLicenceMatchesResult(
            "1/01/01/004",
            "60 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/005",
            "60",
            "cubic meters",
            ["1/01/01/001", "1/01/01/005"],
            true);
        
        var licence5MatchesResult = GetLicenceMatchesResult(
            "1/01/01/005",
            "60 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/004",
            "60",
            "cubic meters",
            ["1/01/01/001", "1/01/01/004"],
            true);

        var data = new Dictionary<string, MatchesResult>
        {
            { "1/01/01/002", licence2MatchesResult },
            { "1/01/01/003", licence3MatchesResult },
            { "1/01/01/004", licence4MatchesResult },
            { "1/01/01/005", licence5MatchesResult }
        };
        
        var lookupConfig = new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            [],
            null,
            new TestCacheService(),
            null,
            new TestLicenceNumberServiceCore(),
            null,
            null,
            new TestDmsLookupService(),
            -1,
            DateTime.UtcNow);

        var licenceSets = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            licence1MatchesResult,
            new TestPdfDataExtractorService(data),
            -1,
            lookupConfig,
            null,
            new TestNaldDataLookupService(),
            dmsDataForFile: new DmsFileData());
        
        Assert.NotNull(licenceSets);
        Assert.Equal(2, licenceSets.Count);
        
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licenceSets[0].LicenceSetType);
        Assert.Equal("10101001-10101002-10101003", licenceSets[0].LicenceSetId);
        Assert.Equal(3, licenceSets[0].Licences.Length);
        
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licenceSets[1].LicenceSetType);
        Assert.Equal("10101001-10101004-10101005", licenceSets[1].LicenceSetId);
        Assert.Equal(3, licenceSets[1].Licences.Length);
        
        var licence1 = licenceSets[0].Licences[0];
        Assert.Equal("1/01/01/001", licence1.LicenceNumber!.Value);            
        
        Assert.Equal(4, licence1.LinkedLicences.Length);
        Assert.Equal("1/01/01/002", licence1.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence1.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[1].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/004", licence1.LinkedLicences[2].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[2].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[2].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[2].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/005", licence1.LinkedLicences[3].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[3].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[3].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[3].ContainedIn![1].Direction);

        Assert.NotNull(licence1.AbstractionLimits.Aggregates);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates.Length);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0]);
        Assert.True(licence1.AbstractionLimits.Aggregates[0].ContainsLegalText);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/002", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/003", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence1.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence1.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence1.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licence1.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[1]);
        Assert.True(licence1.AbstractionLimits.Aggregates[1].ContainsLegalText);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[1].LinkedLicences);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[1].LinkedLicences!.Length);
        Assert.Equal("1/01/01/004", licence1.AbstractionLimits.Aggregates[1].LinkedLicences![0]);
        Assert.Equal("1/01/01/005", licence1.AbstractionLimits.Aggregates[1].LinkedLicences![1]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[1].Limits);
        Assert.Single(licence1.AbstractionLimits.Aggregates[1].Limits);
        Assert.Equal("cubic meters", licence1.AbstractionLimits.Aggregates[1].Limits[0].Units);
        Assert.Equal(60, licence1.AbstractionLimits.Aggregates[1].Limits[0].Value);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[1].Variations);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[1].Variations!.Length);
        Assert.Equal("10101004-LVUNKNOWN-LL-10101001-10101005-C60", licence1.AbstractionLimits.Aggregates[1].Variations![0].Id);
        Assert.Equal("10101005-LVUNKNOWN-LL-10101001-10101004-C60", licence1.AbstractionLimits.Aggregates[1].Variations![1].Id);
        Assert.Equal(2, licence1.LicenceSets.Length);
        Assert.Equal("10101001-10101002-10101003", licence1.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence1.LicenceSets[0].LicenceSetType);
        Assert.Equal("10101001-10101004-10101005", licence1.LicenceSets[1].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence1.LicenceSets[1].LicenceSetType);
        
        var licence2 = licenceSets[0].Licences[1];
        Assert.Equal("1/01/01/002", licence2.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence2.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence2.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence2.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[1].ContainedIn![1].Direction);

        Assert.NotNull(licence2.AbstractionLimits.Aggregates);
        Assert.Single(licence2.AbstractionLimits.Aggregates);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0]);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence2.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/003", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence2.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence2.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence2.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(2, licence2.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence2.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licence2.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.Single(licence2.LicenceSets);
        Assert.Equal("10101001-10101002-10101003", licence2.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence2.LicenceSets[0].LicenceSetType);
        
        var licence3 = licenceSets[0].Licences[2];
        Assert.Equal("1/01/01/003", licence3.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence3.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence3.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/002", licence3.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[1].ContainedIn![1].Direction);

        Assert.NotNull(licence3.AbstractionLimits.Aggregates);
        Assert.Single(licence3.AbstractionLimits.Aggregates);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0]);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence3.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/002", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence3.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence3.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Single(licence3.LicenceSets);
        Assert.Equal("10101001-10101002-10101003", licence3.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence3.LicenceSets[0].LicenceSetType);
        
        // Check aggregate sets
        Assert.NotNull(licenceSets[0].AggregateSets);
        Assert.Single(licenceSets[0].AggregateSets!);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licenceSets[0].AggregateSets![0].Id);
        Assert.True(licenceSets[0].AggregateSets![0].ContainsLegalText);
        Assert.Equal("1/01/01/001", licenceSets[0].AggregateSets![0].SourceLicenceNumber);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Limits[0].Value);
        Assert.NotNull(licenceSets[0].AggregateSets![0].Variations);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].Variations!.Length);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licenceSets[0].AggregateSets![0].Variations![0].Id);
        Assert.Equal("1/01/01/002", licenceSets[0].AggregateSets![0].Variations![0].SourceLicenceNumber);
        Assert.True(licenceSets[0].AggregateSets![0].Variations![0].ContainsLegalText);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].Variations![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Variations![0].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Variations![0].Limits[0].Value);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licenceSets[0].AggregateSets![0].Variations![1].Id);
        Assert.Equal("1/01/01/003", licenceSets[0].AggregateSets![0].Variations![1].SourceLicenceNumber);
        Assert.True(licenceSets[0].AggregateSets![0].Variations![1].ContainsLegalText);
        Assert.Equal(2, licenceSets[0].AggregateSets![0].Variations![1].LinkedLicences!.Length);
        Assert.Single(licenceSets[0].AggregateSets![0].Variations![1].Limits);
        Assert.Equal(30, licenceSets[0].AggregateSets![0].Variations![1].Limits[0].Value);
        
        Assert.NotNull(licenceSets[1].AggregateSets);
        Assert.Single(licenceSets[1].AggregateSets!);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101004-10101005-C60", licenceSets[1].AggregateSets![0].Id);
        Assert.True(licenceSets[1].AggregateSets![0].ContainsLegalText);
        Assert.Equal("1/01/01/001", licenceSets[1].AggregateSets![0].SourceLicenceNumber);
        Assert.Equal(2, licenceSets[1].AggregateSets![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[1].AggregateSets![0].Limits);
        Assert.Equal(60, licenceSets[1].AggregateSets![0].Limits[0].Value);
        Assert.NotNull(licenceSets[1].AggregateSets![0].Variations);
        Assert.Equal(2, licenceSets[1].AggregateSets![0].Variations!.Length);
        Assert.Equal("10101004-LVUNKNOWN-LL-10101001-10101005-C60", licenceSets[1].AggregateSets![0].Variations![0].Id);
        Assert.Equal("1/01/01/004", licenceSets[1].AggregateSets![0].Variations![0].SourceLicenceNumber);
        Assert.True(licenceSets[1].AggregateSets![0].Variations![0].ContainsLegalText);
        Assert.Equal(2, licenceSets[1].AggregateSets![0].Variations![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[1].AggregateSets![0].Variations![0].Limits);
        Assert.Equal(60, licenceSets[1].AggregateSets![0].Variations![0].Limits[0].Value);
        Assert.Equal("10101005-LVUNKNOWN-LL-10101001-10101004-C60", licenceSets[1].AggregateSets![0].Variations![1].Id);
        Assert.Equal("1/01/01/005", licenceSets[1].AggregateSets![0].Variations![1].SourceLicenceNumber);
        Assert.True(licenceSets[1].AggregateSets![0].Variations![1].ContainsLegalText);
        Assert.Equal(2, licenceSets[1].AggregateSets![0].Variations![1].LinkedLicences!.Length);
        Assert.Single(licenceSets[1].AggregateSets![0].Variations![1].Limits);
        Assert.Equal(60, licenceSets[1].AggregateSets![0].Variations![1].Limits[0].Value);
    }
    
    [Fact]
    public async Task When2GroupsOf3LicencesEachLinkingToEachSeperateScrapes_Then2SeperateAggregateSetsMade()
    {
        var licence1MatchesResult = GetLicenceMatchesResult(
            "1/01/01/001",
            "30 cubic meters per day in aggregate with 1/01/01/002 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/002", "1/01/01/003"],
            true);
        
        var licence2MatchesResult = GetLicenceMatchesResult(
            "1/01/01/002",
            "30 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/003",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/003"],
            true);
        
        var licence3MatchesResult = GetLicenceMatchesResult(
            "1/01/01/003",
            "30 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/002",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/002"],
            true);
        
        var data1 = new Dictionary<string, MatchesResult>
        {
            { "1/01/01/002", licence2MatchesResult },
            { "1/01/01/003", licence3MatchesResult }
        };
        
        var lookupConfig = new LookupConfiguration(
            AbstractionLicenceLabelConfiguration.GetLabels(),
            [],
            null,
            new TestCacheService(),
            null,
            new TestLicenceNumberServiceCore(),
            null,
            null,
            new TestDmsLookupService(),
            -1,
            DateTime.UtcNow);

        var licenceSets1 = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            licence1MatchesResult,
            new TestPdfDataExtractorService(data1),
            -1,
            lookupConfig,
            null,
            new TestNaldDataLookupService(),
            dmsDataForFile: new DmsFileData());

        var licence4MatchesResult = GetLicenceMatchesResult(
            "1/01/01/004",
            "60 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/005",
            "60",
            "cubic meters",
            ["1/01/01/001", "1/01/01/005"],
            true);
        
        var licence5MatchesResult = GetLicenceMatchesResult(
            "1/01/01/005",
            "60 cubic meters per day in aggregate with 1/01/01/001 and 1/01/01/004",
            "60",
            "cubic meters",
            ["1/01/01/001", "1/01/01/004"],
            true);
        
        var data2 = new Dictionary<string, MatchesResult>
        {
            { "1/01/01/001", licence1MatchesResult }, // TODO do the condition where this isnt here
            { "1/01/01/005", licence5MatchesResult }
        };
        
        var licenceSets2 = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            licence4MatchesResult,
            new TestPdfDataExtractorService(data2),
            -1,
            lookupConfig,
            null,
            new TestNaldDataLookupService(),
            dmsDataForFile: new DmsFileData());
        
        var licenceSetGroups = new List<IReadOnlyList<LicenceSet>>
        {
            licenceSets1,
            licenceSets2
        };

        var allLicenceSets = await AbstractionLicenceSchemaConverter.AddAdditionalLicenceSetsAsync(
            licenceSetGroups,
            lookupConfig,
            new TestNaldDataLookupService());
        
        AbstractionLicenceSchemaConverter.CalculateCombinedAggregates(allLicenceSets);
        
        // TODO add a test where its the 3rd party that mentions these licences
        
        Assert.NotNull(licenceSets1);
        Assert.Single(licenceSets1);
        
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licenceSets1[0].LicenceSetType);
        Assert.Equal("10101001-10101002-10101003", licenceSets1[0].LicenceSetId);
        Assert.Equal(3, licenceSets1[0].Licences.Length);
        
        var licence1 = licenceSets1[0].Licences[0];
        Assert.Equal("1/01/01/001", licence1.LicenceNumber!.Value);            
        
        Assert.Equal(4, licence1.LinkedLicences.Length);
        Assert.Equal("1/01/01/002", licence1.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence1.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence1.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence1.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[1].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/004", licence1.LinkedLicences[2].LicenceNumber);
        Assert.Single(licence1.LinkedLicences[2].ContainedIn!);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[2].ContainedIn![0].Direction);
        Assert.Equal("1/01/01/005", licence1.LinkedLicences[3].LicenceNumber);
        Assert.Single(licence1.LinkedLicences[3].ContainedIn!);
        Assert.Equal(InformationDirection.Incoming, licence1.LinkedLicences[3].ContainedIn![0].Direction);

        Assert.NotNull(licence1.AbstractionLimits.Aggregates);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates.Length);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0]);
        Assert.True(licence1.AbstractionLimits.Aggregates[0].ContainsLegalText);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/002", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/003", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence1.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence1.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence1.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licence1.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[1]);
        Assert.Equal("10101004-LVUNKNOWN-LL-10101001-10101005-C60", licence1.AbstractionLimits.Aggregates[1].Id);
        Assert.Equal("1/01/01/004", licence1.AbstractionLimits.Aggregates[1].SourceLicenceNumber);
        Assert.True(licence1.AbstractionLimits.Aggregates[1].ContainsLegalText);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[1].LinkedLicences);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[1].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence1.AbstractionLimits.Aggregates[1].LinkedLicences![0]);
        Assert.Equal("1/01/01/005", licence1.AbstractionLimits.Aggregates[1].LinkedLicences![1]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[1].Limits);
        Assert.Single(licence1.AbstractionLimits.Aggregates[1].Limits);
        Assert.Equal("cubic meters", licence1.AbstractionLimits.Aggregates[1].Limits[0].Units);
        Assert.Equal(60, licence1.AbstractionLimits.Aggregates[1].Limits[0].Value);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[1].Variations);
        Assert.Single(licence1.AbstractionLimits.Aggregates[1].Variations!);
        Assert.Equal("10101005-LVUNKNOWN-LL-10101001-10101004-C60", licence1.AbstractionLimits.Aggregates[1].Variations![0].Id);
        Assert.Equal(2, licence1.LicenceSets.Length);
        Assert.Equal("10101001-10101002-10101003", licence1.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence1.LicenceSets[0].LicenceSetType);
        Assert.Equal("10101001-10101004-10101005", licence1.LicenceSets[1].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence1.LicenceSets[1].LicenceSetType);
        
        Assert.NotNull(licenceSets2);
        Assert.Single(licenceSets2);
        
        var licence2 = licenceSets1[0].Licences[1];
        Assert.Equal("1/01/01/002", licence2.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence2.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence2.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/003", licence2.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence2.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence2.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence2.LinkedLicences[1].ContainedIn![1].Direction);

        Assert.NotNull(licence2.AbstractionLimits.Aggregates);
        Assert.Single(licence2.AbstractionLimits.Aggregates);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0]);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence2.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/003", licence2.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence2.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence2.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence2.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].Variations);
        Assert.Equal(2, licence2.AbstractionLimits.Aggregates[0].Variations!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence2.AbstractionLimits.Aggregates[0].Variations![0].Id);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licence2.AbstractionLimits.Aggregates[0].Variations![1].Id);
        Assert.Single(licence2.LicenceSets);
        Assert.Equal("10101001-10101002-10101003", licence2.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence2.LicenceSets[0].LicenceSetType);
        
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licenceSets2[0].LicenceSetType);
        Assert.Equal("10101001-10101004-10101005", licenceSets2[0].LicenceSetId);
        Assert.Equal(3, licenceSets2[0].Licences.Length);
        
        var licence3 = licenceSets1[0].Licences[2];
        Assert.Equal("1/01/01/003", licence3.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence3.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence3.LinkedLicences[0].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[0].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[0].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[0].ContainedIn![1].Direction);
        Assert.Equal("1/01/01/002", licence3.LinkedLicences[1].LicenceNumber);
        Assert.Equal(2, licence3.LinkedLicences[1].ContainedIn!.Length);
        Assert.Equal(InformationDirection.Outgoing, licence3.LinkedLicences[1].ContainedIn![0].Direction);
        Assert.Equal(InformationDirection.Incoming, licence3.LinkedLicences[1].ContainedIn![1].Direction);

        Assert.NotNull(licence3.AbstractionLimits.Aggregates);
        Assert.Single(licence3.AbstractionLimits.Aggregates);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0]);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence3.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/002", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence3.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence3.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Single(licence3.LicenceSets);
        Assert.Equal("10101001-10101002-10101003", licence3.LicenceSets[0].LicenceSetId);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, licence3.LicenceSets[0].LicenceSetType);
        
        // Check aggregate sets
        Assert.NotNull(licenceSets1[0].AggregateSets);
        Assert.Single(licenceSets1[0].AggregateSets!);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licenceSets1[0].AggregateSets![0].Id);
        Assert.True(licenceSets1[0].AggregateSets![0].ContainsLegalText);
        Assert.Equal("1/01/01/001", licenceSets1[0].AggregateSets![0].SourceLicenceNumber);
        Assert.Equal(2, licenceSets1[0].AggregateSets![0].LinkedLicences!.Length);
        Assert.Single(licenceSets1[0].AggregateSets![0].Limits);
        Assert.Equal(30, licenceSets1[0].AggregateSets![0].Limits[0].Value);
        Assert.NotNull(licenceSets1[0].AggregateSets![0].Variations);
        Assert.Equal(2, licenceSets1[0].AggregateSets![0].Variations!.Length);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licenceSets1[0].AggregateSets![0].Variations![0].Id);
        Assert.Equal("1/01/01/002", licenceSets1[0].AggregateSets![0].Variations![0].SourceLicenceNumber);
        Assert.True(licenceSets1[0].AggregateSets![0].Variations![0].ContainsLegalText);
        Assert.Equal(2, licenceSets1[0].AggregateSets![0].Variations![0].LinkedLicences!.Length);
        Assert.Single(licenceSets1[0].AggregateSets![0].Variations![0].Limits);
        Assert.Equal(30, licenceSets1[0].AggregateSets![0].Variations![0].Limits[0].Value);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licenceSets1[0].AggregateSets![0].Variations![1].Id);
        Assert.Equal("1/01/01/003", licenceSets1[0].AggregateSets![0].Variations![1].SourceLicenceNumber);
        Assert.True(licenceSets1[0].AggregateSets![0].Variations![1].ContainsLegalText);
        Assert.Equal(2, licenceSets1[0].AggregateSets![0].Variations![1].LinkedLicences!.Length);
        Assert.Single(licenceSets1[0].AggregateSets![0].Variations![1].Limits);
        Assert.Equal(30, licenceSets1[0].AggregateSets![0].Variations![1].Limits[0].Value);
        
        Assert.NotNull(licenceSets1[1].AggregateSets);
        Assert.Single(licenceSets1[1].AggregateSets!);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101004-10101005-C60", licenceSets1[1].AggregateSets![0].Id);
        Assert.True(licenceSets1[1].AggregateSets![0].ContainsLegalText);
        Assert.Equal("1/01/01/001", licenceSets1[1].AggregateSets![0].SourceLicenceNumber);
        Assert.Equal(2, licenceSets1[1].AggregateSets![0].LinkedLicences!.Length);
        Assert.Single(licenceSets1[1].AggregateSets![0].Limits);
        Assert.Equal(60, licenceSets1[1].AggregateSets![0].Limits[0].Value);
        Assert.NotNull(licenceSets1[1].AggregateSets![0].Variations);
        Assert.Equal(2, licenceSets1[1].AggregateSets![0].Variations!.Length);
        Assert.Equal("10101004-LVUNKNOWN-LL-10101001-10101005-C60", licenceSets1[1].AggregateSets![0].Variations![0].Id);
        Assert.Equal("1/01/01/004", licenceSets1[1].AggregateSets![0].Variations![0].SourceLicenceNumber);
        Assert.True(licenceSets1[1].AggregateSets![0].Variations![0].ContainsLegalText);
        Assert.Equal(2, licenceSets1[1].AggregateSets![0].Variations![0].LinkedLicences!.Length);
        Assert.Single(licenceSets1[1].AggregateSets![0].Variations![0].Limits);
        Assert.Equal(60, licenceSets1[1].AggregateSets![0].Variations![0].Limits[0].Value);
        Assert.Equal("10101005-LVUNKNOWN-LL-10101001-10101004-C60", licenceSets1[1].AggregateSets![0].Variations![1].Id);
        Assert.Equal("1/01/01/005", licenceSets1[1].AggregateSets![0].Variations![1].SourceLicenceNumber);
        Assert.True(licenceSets1[1].AggregateSets![0].Variations![1].ContainsLegalText);
        Assert.Equal(2, licenceSets1[1].AggregateSets![0].Variations![1].LinkedLicences!.Length);
        Assert.Single(licenceSets1[1].AggregateSets![0].Variations![1].Limits);
        Assert.Equal(60, licenceSets1[1].AggregateSets![0].Variations![1].Limits[0].Value);
    }

    private static MatchesResult GetLicenceMatchesResult(
        string sourceLicenceNumber,
        string? abstractionLimitsText,
        string? value,
        string? units,
        List<string> linkedLicenceNumbers,
        bool containsLegalText = false)
    {
        var limitLines = !string.IsNullOrEmpty(abstractionLimitsText)
            ? TextToLines(abstractionLimitsText)
            : [];
        
        var baseSubResults = !string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(units)
            ? new List<LabelGroupResult>
            {
                new()
                {
                    MatchedLabelName = "PerDayValue",
                    MatchedLabelRelatedName = "PerDayUnits",
                    Text = TextToLines(value),
                    MatchedLabelTextFirstLine = "per day"
                },
                new()
                {
                    MatchedLabelName = "PerDayUnits",
                    Text = TextToLines(units)
                }
            }
            : [];

        if (containsLegalText)
        {
            baseSubResults.Add(new LabelGroupResult
            {
                MatchedLabelName = "LegalText",
                Text = TextToLines("As may be renewed from time to time")
            });
        }
        
        baseSubResults.AddRange(linkedLicenceNumbers
            .Select(linkedLicenceNumber => new LabelGroupResult
            {
                MatchedLabelName = "LinkedLicenceNumber",
                Text = TextToLines(linkedLicenceNumber)
            }));
        
        return new MatchesResult
        {
            Matches = [
                new LabelGroupResult
                {
                  LabelGroupName = "LicenceNumber",
                  Text = TextToLines(sourceLicenceNumber)
                },
                new LabelGroupResult
                {
                    LabelGroupName = "AbstractionLimits",
                    Text = limitLines,
                    SubResults = [
                        new LabelGroupResult
                        {
                            MatchedLabelName = "AbstractionLimitPoint",
                            Text = limitLines,
                            SubResults = [
                                new()
                                {
                                    MatchedLabelName = "AbstractionLimitPointSub",
                                    Text = limitLines,
                                    SubResults = baseSubResults
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    public static List<DocumentLine> TextToLines(string text)
    {
        return
        [
            new()
            {
                Columns =
                [
                    new DocumentLineColumn
                    {
                        Words = text
                            .Split(' ')
                            .Select(word => new DocumentLineWord { Text = word })
                            .ToList()
                    }
                ]
            }
        ];
    }
}