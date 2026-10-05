using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
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
        const string expectedAggregateSetId = "10101001-LVUNKNOWN-10101002-LVUNKNOWN-10101003-LVUNKNOWN-C30";
        
        Assert.NotNull(licenceSets);
        Assert.Equal(2, licenceSets.Count);

        // Single licence set group
        Assert.Single(licenceSets[0].Licences);

        // Check aggregate sets for single licence set group (none)
        Assert.Null(licenceSets[0].AggregateSets);
        
        // Aggregate licence set group
        Assert.Equal(3, licenceSets[1].Licences.Length);

        // Check licence limits in the big group
        var licence1 = licenceSets[1].Licences[0];
        Assert.Equal("1/01/01/001", licence1.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence1.LinkedLicences.Length);
        Assert.Equal("1/01/01/002", licence1.LinkedLicences[0].LicenceNumber);
        Assert.Equal("1/01/01/003", licence1.LinkedLicences[1].LicenceNumber);

        Assert.NotNull(licence1.AbstractionLimits.Aggregates);
        Assert.Single(licence1.AbstractionLimits.Aggregates);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/002", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/003", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence1.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence1.AbstractionLimits.Aggregates[0].Limits[0].Value);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence1.AbstractionLimits.Aggregates[0].Id);
        Assert.Equal(expectedAggregateSetId, licence1.AbstractionLimits.Aggregates[0].AggregateSetId);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].OtherVersions);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].OtherVersions!.Length);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence1.AbstractionLimits.Aggregates[0].OtherVersions![0].Id);
        Assert.Equal(expectedAggregateSetId, licence1.AbstractionLimits.Aggregates[0].OtherVersions![0].AggregateSetId);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licence1.AbstractionLimits.Aggregates[0].OtherVersions![1].Id);
        Assert.Equal(expectedAggregateSetId, licence1.AbstractionLimits.Aggregates[0].OtherVersions![1].AggregateSetId);
        Assert.Equal("Different order", licence1.AbstractionLimits.Aggregates[0].OtherVersions![0].Difference);
        Assert.Equal("Different order", licence1.AbstractionLimits.Aggregates[0].OtherVersions![1].Difference);
        
        var licence2 = licenceSets[1].Licences[1];
        Assert.Equal("1/01/01/002", licence2.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence2.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence2.LinkedLicences[0].LicenceNumber);
        Assert.Equal("1/01/01/003", licence2.LinkedLicences[1].LicenceNumber);

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
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence2.AbstractionLimits.Aggregates[0].Id);
        Assert.Equal(expectedAggregateSetId, licence2.AbstractionLimits.Aggregates[0].AggregateSetId);
        Assert.NotNull(licence2.AbstractionLimits.Aggregates[0].OtherVersions);
        Assert.Equal(2, licence2.AbstractionLimits.Aggregates[0].OtherVersions!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence2.AbstractionLimits.Aggregates[0].OtherVersions![0].Id);
        Assert.Equal(expectedAggregateSetId, licence2.AbstractionLimits.Aggregates[0].OtherVersions![0].AggregateSetId);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licence2.AbstractionLimits.Aggregates[0].OtherVersions![1].Id);
        Assert.Equal(expectedAggregateSetId, licence2.AbstractionLimits.Aggregates[0].OtherVersions![1].AggregateSetId);
        Assert.Equal("Different order", licence2.AbstractionLimits.Aggregates[0].OtherVersions![0].Difference);
        Assert.Equal("Different order", licence2.AbstractionLimits.Aggregates[0].OtherVersions![1].Difference);
        
        var licence3 = licenceSets[1].Licences[2];
        Assert.Equal("1/01/01/003", licence3.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence3.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence3.LinkedLicences[0].LicenceNumber);
        Assert.Equal("1/01/01/002", licence3.LinkedLicences[1].LicenceNumber);

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
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licence3.AbstractionLimits.Aggregates[0].Id);
        Assert.Equal(expectedAggregateSetId, licence3.AbstractionLimits.Aggregates[0].AggregateSetId);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].OtherVersions);
        Assert.Equal(2, licence3.AbstractionLimits.Aggregates[0].OtherVersions!.Length);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licence3.AbstractionLimits.Aggregates[0].OtherVersions![0].Id);
        Assert.Equal(expectedAggregateSetId, licence3.AbstractionLimits.Aggregates[0].OtherVersions![0].AggregateSetId);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licence3.AbstractionLimits.Aggregates[0].OtherVersions![1].Id);
        Assert.Equal(expectedAggregateSetId, licence3.AbstractionLimits.Aggregates[0].OtherVersions![1].AggregateSetId);
        Assert.Equal("Different order", licence3.AbstractionLimits.Aggregates[0].OtherVersions![0].Difference);
        Assert.Equal("Different order", licence3.AbstractionLimits.Aggregates[0].OtherVersions![1].Difference);
        
        // Check aggregate sets
        Assert.NotNull(licenceSets[1].AggregateSets!);
        Assert.Single(licenceSets[1].AggregateSets!);
        Assert.Equal(expectedAggregateSetId, licenceSets[1].AggregateSets![0].AggregateSetId);
        Assert.Single(licenceSets[1].AggregateSets![0].Aggregates);
        Assert.Equal(expectedAggregateSetId, licenceSets[1].AggregateSets![0].Aggregates[0].AggregateSetId);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licenceSets[1].AggregateSets![0].Aggregates[0].Id);
        Assert.Equal("1/01/01/001", licenceSets[1].AggregateSets![0].Aggregates[0].SourceLicenceNumber);
        Assert.Equal(2, licenceSets[1].AggregateSets![0].Aggregates[0].LinkedLicences!.Length);
        Assert.Single(licenceSets[1].AggregateSets![0].Aggregates[0].Limits);
        Assert.Equal(30, licenceSets[1].AggregateSets![0].Aggregates[0].Limits[0].Value);
        Assert.NotNull(licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions);
        Assert.Equal(2, licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions!.Length);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![0].Id);
        Assert.Equal("1/01/01/002", licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![0].SourceLicenceNumber);
        Assert.Equal(2, licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![0].Limits);
        Assert.Equal(30, licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![0].Limits[0].Value);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C30", licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![1].Id);
        Assert.Equal("1/01/01/003", licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![1].SourceLicenceNumber);
        Assert.Equal(2, licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![1].LinkedLicences!.Length);
        Assert.Single(licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![1].Limits);
        Assert.Equal(30, licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![1].Limits[0].Value);
    }
    
    [Fact]
    public async Task When3LicencesEachLinkingToEachOther_SameLicences_1ValueDoesNotMatch_ThenSomething()
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
        
        // Check aggregate sets in the big group
        Assert.NotNull(licenceSets);
        Assert.Equal(2, licenceSets.Count);

        Assert.Single(licenceSets[0].Licences); // Single licence set group
        Assert.Equal(3, licenceSets[1].Licences.Length); // Aggregate licence set group

        var licence1 = licenceSets[1].Licences[0];
        Assert.Equal("1/01/01/001", licence1.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence1.LinkedLicences.Length);
        Assert.Equal("1/01/01/002", licence1.LinkedLicences[0].LicenceNumber);
        Assert.Equal("1/01/01/003", licence1.LinkedLicences[1].LicenceNumber);

        Assert.NotNull(licence1.AbstractionLimits.Aggregates);
        Assert.Single(licence1.AbstractionLimits.Aggregates);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/002", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/003", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence1.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence1.AbstractionLimits.Aggregates[0].Limits[0].Value);
        
        var licence2 = licenceSets[1].Licences[1];
        Assert.Equal("1/01/01/002", licence2.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence2.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence2.LinkedLicences[0].LicenceNumber);
        Assert.Equal("1/01/01/003", licence2.LinkedLicences[1].LicenceNumber);

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
        
        var licence3 = licenceSets[1].Licences[2];
        Assert.Equal("1/01/01/003", licence3.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence3.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence3.LinkedLicences[0].LicenceNumber);
        Assert.Equal("1/01/01/002", licence3.LinkedLicences[1].LicenceNumber);

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
        Assert.Equal(60, licence3.AbstractionLimits.Aggregates[0].Limits[0].Value);
        
        // Check aggregate sets
        Assert.NotNull(licenceSets[1].AggregateSets!);
        Assert.Single(licenceSets[1].AggregateSets!);
        Assert.Equal("10101001-LVUNKNOWN-10101002-LVUNKNOWN-10101003-LVUNKNOWN-C90", licenceSets[1].AggregateSets![0].AggregateSetId);
        Assert.Equal(2, licenceSets[1].AggregateSets![0].Aggregates.Length);
        Assert.Equal("10101001-LVUNKNOWN-10101002-LVUNKNOWN-10101003-LVUNKNOWN-C90", licenceSets[1].AggregateSets![0].Aggregates[0].AggregateSetId);
        Assert.Equal("10101001-LVUNKNOWN-LL-10101002-10101003-C30", licenceSets[1].AggregateSets![0].Aggregates[0].Id);
        Assert.Equal("1/01/01/001", licenceSets[1].AggregateSets![0].Aggregates[0].SourceLicenceNumber);
        Assert.Equal(2, licenceSets[1].AggregateSets![0].Aggregates[0].LinkedLicences!.Length);
        Assert.Single(licenceSets[1].AggregateSets![0].Aggregates[0].Limits);
        Assert.Equal(30, licenceSets[1].AggregateSets![0].Aggregates[0].Limits[0].Value);
        Assert.NotNull(licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions);
        Assert.Single(licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions!);
        Assert.Equal("10101002-LVUNKNOWN-LL-10101001-10101003-C30", licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![0].Id);
        Assert.Equal("1/01/01/002", licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![0].SourceLicenceNumber);
        Assert.Equal(2, licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![0].LinkedLicences!.Length);
        Assert.Single(licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![0].Limits);
        Assert.Equal(30, licenceSets[1].AggregateSets![0].Aggregates[0].OtherVersions![0].Limits[0].Value);
        
        Assert.Equal("10101001-LVUNKNOWN-10101002-LVUNKNOWN-10101003-LVUNKNOWN-C90", licenceSets[1].AggregateSets![0].Aggregates[1].AggregateSetId);
        Assert.Equal("10101003-LVUNKNOWN-LL-10101001-10101002-C60", licenceSets[1].AggregateSets![0].Aggregates[1].Id);
        Assert.Equal("1/01/01/003", licenceSets[1].AggregateSets![0].Aggregates[1].SourceLicenceNumber);
        Assert.Equal(2, licenceSets[1].AggregateSets![0].Aggregates[1].LinkedLicences!.Length);
        Assert.Single(licenceSets[1].AggregateSets![0].Aggregates[1].Limits);
        Assert.Equal(60, licenceSets[1].AggregateSets![0].Aggregates[1].Limits[0].Value);
        Assert.Null(licenceSets[1].AggregateSets![0].Aggregates[1].OtherVersions);
    }
    
    [Fact]
    public async Task When3LicencesEachLinkingToEachOther_1HasAnExtraLicence_ValuesMatch_ThenSomething()
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
            "30 cubic meters per day in aggregate with 1/01/01/001, 1/01/01/002 and 1/01/01/004",
            "30",
            "cubic meters",
            ["1/01/01/001", "1/01/01/002", "1/01/01/004"]);

        var data = new Dictionary<string, MatchesResult>
        {
            { "1/01/01/002", licence2MatchesResult },
            { "1/01/01/003", licence3MatchesResult },
            { "1/01/01/004", licence3MatchesResult }
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

        var licenceSet = await AbstractionLicenceSchemaConverter.ToLicenceSetsAsync(
            licence1MatchesResult,
            new TestPdfDataExtractorService(data),
            -1,
            lookupConfig,
            null,
            new TestNaldDataLookupService(),
            dmsDataForFile: new DmsFileData());
        
        Assert.NotNull(licenceSet);
        Assert.Equal(2, licenceSet.Count);

        Assert.Single(licenceSet[0].Licences); // Single licence set group
        Assert.Equal(3, licenceSet[1].Licences.Length); // Aggregate licence set group

        var licence1 = licenceSet[1].Licences[0];
        Assert.Equal("1/01/01/001", licence1.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence1.LinkedLicences.Length);
        Assert.Equal("1/01/01/002", licence1.LinkedLicences[0].LicenceNumber);
        Assert.Equal("1/01/01/003", licence1.LinkedLicences[1].LicenceNumber);

        Assert.NotNull(licence1.AbstractionLimits.Aggregates);
        Assert.Single(licence1.AbstractionLimits.Aggregates);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(2, licence1.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/002", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/003", licence1.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.NotNull(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence1.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence1.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence1.AbstractionLimits.Aggregates[0].Limits[0].Value);
        
        var licence2 = licenceSet[1].Licences[1];
        Assert.Equal("1/01/01/002", licence2.LicenceNumber!.Value);            
        
        Assert.Equal(2, licence2.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence2.LinkedLicences[0].LicenceNumber);
        Assert.Equal("1/01/01/003", licence2.LinkedLicences[1].LicenceNumber);

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
        
        var licence3 = licenceSet[1].Licences[2];
        Assert.Equal("1/01/01/003", licence3.LicenceNumber!.Value);            
        
        Assert.Equal(3, licence3.LinkedLicences.Length);
        Assert.Equal("1/01/01/001", licence3.LinkedLicences[0].LicenceNumber);
        Assert.Equal("1/01/01/002", licence3.LinkedLicences[1].LicenceNumber);
        Assert.Equal("1/01/01/004", licence3.LinkedLicences[2].LicenceNumber);

        Assert.NotNull(licence3.AbstractionLimits.Aggregates);
        Assert.Single(licence3.AbstractionLimits.Aggregates);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0]);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].LinkedLicences);
        Assert.Equal(3, licence3.AbstractionLimits.Aggregates[0].LinkedLicences!.Length);
        Assert.Equal("1/01/01/001", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![0]);
        Assert.Equal("1/01/01/002", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![1]);
        Assert.Equal("1/01/01/004", licence3.AbstractionLimits.Aggregates[0].LinkedLicences![2]);
        Assert.NotNull(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Single(licence3.AbstractionLimits.Aggregates[0].Limits);
        Assert.Equal("cubic meters", licence3.AbstractionLimits.Aggregates[0].Limits[0].Units);
        Assert.Equal(30, licence3.AbstractionLimits.Aggregates[0].Limits[0].Value);
    }

    private static MatchesResult GetLicenceMatchesResult(
        string sourceLicenceNumber,
        string abstractionLimitsText,
        string value,
        string units,
        List<string> linkedLicenceNumbers)
    {
        var limitLines = TextToLines(abstractionLimitsText);
        
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
                                    SubResults = [
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
                                        },
                                        ..linkedLicenceNumbers
                                            .Select(linkedLicenceNumber => new LabelGroupResult
                                            {
                                                MatchedLabelName = "LinkedLicenceNumber",
                                                Text = TextToLines(linkedLicenceNumber)
                                            })
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static List<DocumentLine> TextToLines(string text)
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