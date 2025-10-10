using System.Collections;
using System.Globalization;
using System.Text.Json;
using CsvHelper;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.Tools.Models;

namespace WALE.Tools;

public static class GenerateCsvForTesting
{
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly Dictionary<string, string> FileLicenceMapping = new() {{"", ""}};
    
    public static async Task GenerateCsvForTestingAsync()
    {
        var pdfDataExtractor = new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>
            {
                new AzureAiVisionOcrDataExtractorService(
                    KeyConfig.AiVisionEndpoint,
                    KeyConfig.AiVisionKey,
                    CacheService)
            },
            CacheService,
            OutputService,
            KeyConfig.PdfFolder);

        var data = await GetYorkshire70DataAsync(pdfDataExtractor);
        //var data = await GetYorkshire6DataAsync(pdfDataExtractor);

        await using var writer = new StreamWriter($"Yorkshire-{DateTime.Today:yyyyMMdd}.csv");
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csv.WriteRecordsAsync((IEnumerable)data);
    }

    static Task<MatchesResult> GetMatchesAsync(string fileName, PdfDataExtractorService pdfDataExtractor)
    {
        var pdfFolder = KeyConfig.PdfFolder;
        
        return pdfDataExtractor.GetMatchesAsync(
            pdfFolder + fileName,
            new LookupConfiguration(
                LabelConfiguration.GetLabels(),
                FileLicenceMapping),
            [pdfFolder + fileName]);
    }

    static async Task<List<CsvLine>> GetYorkshire70DataAsync(PdfDataExtractorService pdfDataExtractor)
    {
        var yorkshire = YorkshireFiles();
        
        var pdfFilePaths = Directory
            .GetFiles(KeyConfig.PdfFolder)
            .Where(fileName => fileName.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
            .Where(x =>
            {
                var filename = x.Split('/').Last();
                return yorkshire.Contains(filename, StringComparer.InvariantCultureIgnoreCase);
                
            })
            .Select(x => x.Split('/').Last())
            .OrderBy(x => x).ToList();

        var returnList = new List<CsvLine>();
        var licenceSetGroups = new List<IReadOnlyList<LicenceSet>>();

        foreach (var pdfFilePath in pdfFilePaths)
        {
            var internalJson = await GetMatchesAsync(pdfFilePath, pdfDataExtractor);
            var licenceSets = await SchemaConverter.ToLicenceSetsAsync(
                internalJson,
                FileLicenceMapping,
                pdfDataExtractor,
                OutputService,
                CacheService,
                KeyConfig.PdfFolder
            );

            licenceSetGroups.Add(licenceSets);
        }

        SchemaConverter.AddGroupLicenceSetDetails(licenceSetGroups);

        foreach (var licenceSets in licenceSetGroups)
        {
            var file = licenceSets[0].Licences[0];
            
            returnList.Add(new()
            {
                Filename = file.Filename,
                LicenceNumber = file.LicenceNumber,
                HasAggregate = file.AbstractionLimits.Aggregates?.Length > 0,
                AggregateData = JsonSerializer.Serialize(file.AbstractionLimits.Aggregates, JsonHelper.GetSerializerOptions()),
                IndividualLimits = JsonSerializer.Serialize(file.AbstractionLimits.Individual, JsonHelper.GetSerializerOptions()),
                Data = JsonSerializer.Serialize(file, JsonHelper.GetSerializerOptions()),
                LicenceSets = JsonSerializer.Serialize(licenceSets, JsonHelper.GetSerializerOptions())
            });
        }

        return returnList;
    }

    static async Task<List<CsvLine>> GetYorkshire6DataAsync(PdfDataExtractorService pdfDataExtractor)
    {
        var licenceSetGroups = new List<IReadOnlyList<LicenceSet>>();
        
        var internalJson = await GetMatchesAsync("2-26-32-126 6937559.PDF", pdfDataExtractor);
        var licenceSets1 = await SchemaConverter.ToLicenceSetsAsync(internalJson, FileLicenceMapping, pdfDataExtractor,
            OutputService, CacheService, KeyConfig.PdfFolder);
        
        licenceSetGroups.Add(licenceSets1);
        var file1 = licenceSets1[0].Licences[0];

        internalJson = await GetMatchesAsync("2-27-29-012 7003124.PDF", pdfDataExtractor);
        var licenceSets2 = await SchemaConverter.ToLicenceSetsAsync(internalJson, FileLicenceMapping, pdfDataExtractor,
            OutputService, CacheService, KeyConfig.PdfFolder);
        
        licenceSetGroups.Add(licenceSets2);
        var file2 = licenceSets2[0].Licences[0];

        internalJson = await GetMatchesAsync("Application - New - Licence Issued 30092021.pdf", pdfDataExtractor);
        var licenceSets3 = await SchemaConverter.ToLicenceSetsAsync(internalJson, FileLicenceMapping, pdfDataExtractor,
            OutputService, CacheService, KeyConfig.PdfFolder);
        
        licenceSetGroups.Add(licenceSets3);
        var file3 = licenceSets3[0].Licences[0];

        internalJson = await GetMatchesAsync("Application Formal Variation Issued Licence 07032023 (1).pdf", pdfDataExtractor);
        var licenceSets4 = await SchemaConverter.ToLicenceSetsAsync(internalJson, FileLicenceMapping, pdfDataExtractor,
            OutputService, CacheService, KeyConfig.PdfFolder);
        
        licenceSetGroups.Add(licenceSets4);
        var file4 = licenceSets4[0].Licences[0];
        
        internalJson = await GetMatchesAsync("Application Formal Variation Issued Licence 07032023.pdf", pdfDataExtractor);
        var licenceSets5 = await SchemaConverter.ToLicenceSetsAsync(internalJson, FileLicenceMapping, pdfDataExtractor,
            OutputService, CacheService, KeyConfig.PdfFolder);
        
        licenceSetGroups.Add(licenceSets5);
        var file5 = licenceSets5[0].Licences[0];
        
        internalJson = await GetMatchesAsync("Application Minor Variation Issued Licence 03.10.24.pdf", pdfDataExtractor);
        var licenceSets6 = await SchemaConverter.ToLicenceSetsAsync(internalJson, FileLicenceMapping, pdfDataExtractor,
            OutputService, CacheService, KeyConfig.PdfFolder);
        
        licenceSetGroups.Add(licenceSets6);
        var file6 = licenceSets6[0].Licences[0];
        
        SchemaConverter.AddGroupLicenceSetDetails(licenceSetGroups);
        
        return
        [
            new()
            {
                Filename = file1.Filename,
                LicenceNumber = file1.LicenceNumber,
                HasAggregate = file1.AbstractionLimits.Aggregates?.Length > 0,
                AggregateData = JsonSerializer.Serialize(file1.AbstractionLimits.Aggregates),
                IndividualLimits = JsonSerializer.Serialize(file1.AbstractionLimits.Individual, JsonHelper.GetSerializerOptions()),
                Data = JsonSerializer.Serialize(file1,  JsonHelper.GetSerializerOptions()),
                LicenceSets = JsonSerializer.Serialize(licenceSets1,  JsonHelper.GetSerializerOptions()),
            },
            new()
            {
                Filename = file2.Filename,
                LicenceNumber = file2.LicenceNumber,
                HasAggregate = file2.AbstractionLimits.Aggregates?.Length > 0,
                AggregateData = JsonSerializer.Serialize(file2.AbstractionLimits.Aggregates),
                IndividualLimits = JsonSerializer.Serialize(file2.AbstractionLimits.Individual, JsonHelper.GetSerializerOptions()),
                Data = JsonSerializer.Serialize(file2,  JsonHelper.GetSerializerOptions()),
                LicenceSets = JsonSerializer.Serialize(licenceSets2,  JsonHelper.GetSerializerOptions())
            },
            new()
            {
                Filename = file3.Filename,
                LicenceNumber = file3.LicenceNumber,
                HasAggregate = file3.AbstractionLimits.Aggregates?.Length > 0,
                AggregateData = JsonSerializer.Serialize(file3.AbstractionLimits.Aggregates),
                IndividualLimits = JsonSerializer.Serialize(file3.AbstractionLimits.Individual, JsonHelper.GetSerializerOptions()),
                Data = JsonSerializer.Serialize(file3,  JsonHelper.GetSerializerOptions()),
                LicenceSets = JsonSerializer.Serialize(licenceSets3,  JsonHelper.GetSerializerOptions())
            },
            new()
            {
                Filename = file4.Filename,
                LicenceNumber = file4.LicenceNumber,
                HasAggregate = file4.AbstractionLimits.Aggregates?.Length > 0,
                AggregateData = JsonSerializer.Serialize(file4.AbstractionLimits.Aggregates),
                IndividualLimits = JsonSerializer.Serialize(file4.AbstractionLimits.Individual, JsonHelper.GetSerializerOptions()),
                Data = JsonSerializer.Serialize(file4,  JsonHelper.GetSerializerOptions()),
                LicenceSets = JsonSerializer.Serialize(licenceSets4,  JsonHelper.GetSerializerOptions())
            },
            new()
            {
                Filename = file5.Filename,
                LicenceNumber = file5.LicenceNumber,
                HasAggregate = file5.AbstractionLimits.Aggregates?.Length > 0,
                AggregateData = JsonSerializer.Serialize(file5.AbstractionLimits.Aggregates),
                IndividualLimits = JsonSerializer.Serialize(file5.AbstractionLimits.Individual, JsonHelper.GetSerializerOptions()),
                Data = JsonSerializer.Serialize(file5,  JsonHelper.GetSerializerOptions()),
                LicenceSets = JsonSerializer.Serialize(licenceSets5,  JsonHelper.GetSerializerOptions())
            },
            new()
            {
                Filename = file6.Filename,
                LicenceNumber = file6.LicenceNumber,
                HasAggregate = file6.AbstractionLimits.Aggregates?.Length > 0,
                AggregateData = JsonSerializer.Serialize(file6.AbstractionLimits.Aggregates),
                IndividualLimits = JsonSerializer.Serialize(file6.AbstractionLimits.Individual, JsonHelper.GetSerializerOptions()),
                Data = JsonSerializer.Serialize(file6,  JsonHelper.GetSerializerOptions()),
                LicenceSets = JsonSerializer.Serialize(licenceSets6,  JsonHelper.GetSerializerOptions())
            }
        ];
    }

    static List<string> YorkshireFiles()
    {
        return
        [
            "22713185__Non-Application Licence Documents (20.12.1996).pdf",
            "22714090r01__Application Transfer Issued Licence 12 6 24 12 6 24.pdf",
            "22718033__Application - Minor Variation - Issued Licence - 16022023.pdf",
            "22718045__Application - Reduction -Application New Licence Issued 24_06_2019 00_00_00 10897641.pdf",
            "22718125R01__Application - NA Formal Variation - Issued Licence 31.03.21 11764153.pdf",
            "22718131r01__Application -New   licence - Issued Licence  - PDR- 15.12.2022.pdf",
            "22724197__Application - NA Formal Variation - Issued Licence 02112022.pdf",
            "NE0270012011__Application - New - Issued Licence 02.12.2013 8110044.pdf",
            "NE0270012049__Application – New Full   – Issued Licence 23122022.pdf",
            "ne0270018009__Application – Formal Variation – Issued Licence 19122022.pdf",
            "ne0270018020__Application - Minor Variation - Issued Licence - 16022023.pdf",
            "ne0270018023__Application - Minor Variation -Issued Licence - 08.11.2022.pdf",
            "ne0270018033__Application – Formal Variation – Issued Licence 1512022.pdf",
            "NE0270018041__Application NA New Issued Licence 26 03 2021 11761845.pdf",
            "22725124__Non-Application Licence Document (09.10.2008).pdf",
            "22727116__Application Formal Variation Issued Licence - 26092023.pdf",
            "22727278__Non-Application Licence Document (26.01.2009).pdf",
            "22727279__Non-Application Licence Document (26.01.2009).pdf",
            "ne0270025032__Application New Issued Licence 16.05.23.pdf",
            "NE0270025037__Application Formal Variation Issued Licence 16.05.23.pdf",
            "NE0270026005R01__Application Renewal Licence Issued - (25092024).pdf",
            "ne0270027009__Application Formal Variation Issued Licence 03.05.23.pdf",
            "ne0270028073__Application – NA New – Issued Licence 27092022.pdf",
            "NE0270028081__Application New License - License Issued - 18102024.pdf",
            "22704027r01__Application Formal Variation Issued Licence - [issued date] - (07062024).pdf",
            "22707004__Application - Transfer - Issued Licence 28.04.2017 9774748.pdf",
            "22708092__Application – NA Formal Variation – Issued Licence-10082022.pdf",
            "22709099__Application Minor Variation Licence issued 21.12.2018 10629856.pdf",
            "22709196r01__Application New Licence Issued - [22.03.2024] - (22.03.2024).pdf",
            "NE0270005031__Application New Issued Licence 17.04.23.pdf",
            "NE0270029007R01__Application Renewal Licence Issued - [issued date] - (11042024).pdf",
            "22631093__Application - Issued Licence [23-10-1978] 6075944.pdf",
            "22631097__Non-Application Licence Document (09.03.1988).pdf",
            "22631114__Application Formal Variation Issued Licence - [issued date] - (29082024).pdf",
            "22631168R01__Application Renewal Licence Issued - [issued date] - (09052024).pdf",
            "22632004__Application Minor Variation Issued Licence - 06122023.pdf",
            "22632235__Application Renewal - Licence Issued - 11112024.pdf",
            "22632344__Application - NA Formal Variation - Issued Licence 27102022.pdf",
            "22634031__Application - NA Formal Variation - Issued Licence 27102022.pdf",
            "22724007__Application minor variation issued Licence 22724007 11600563.pdf",
            "NE0260030016R01__Application Renewal - Licence Issued - 20112024.pdf",
            "NE0260031035__Application New Issued Licence 28.04.2023.pdf",
            "ne0260032055__Application - NA New - Issued Licence 15112022.pdf",
            "NE0260032058__Application NA New Licence Issued (Public Register) - 02122022 .pdf",
            "NE0260032074__Application  new  -licence issued  (08072024).pdf",
            "NE0260033011__Application - New -Application New Licence Issued 24_03_2020 00_00_00 11292824.pdf",
            "NE0260033017__Application Formal Variation - Licence Issued - (23052024).pdf",
            "NE0260034006__Application - Formal Variation -Application New Licence Issued 08_08_2019 00_00_00 10974057.pdf",
            "NE0260034018__Application Minor Variation Issued Licence 11.12.2019 11149535.pdf",
            "NE0260034052__Application Apportionment Issued Licence 11.12.2019 11149440.pdf",
            "NE0260034056__Application New Issued Licence 10.09.2020 11497061.pdf",
            "NE0270024021R02__Application Renewal Licence Issued - 20062024.pdf",
            "22721238__Non-Application Licence Document (25.07.1977).pdf",
            "22721348r01__Application – NA Formal Variation – Issued Licence 13.07.2022.pdf",
            "22721356R01__Application Formal Variation Issued Licence 13.9.18 10487468.pdf",
            "22722128__Non-Application Licence Document (15.08.1988).pdf",
            "22722323__Non-Application Licence Document - Issued Licence - 22101998.pdf",
            "22722395A__Non-Application Licence Document (22.10.2001).pdf",
            "22722452__Non-Application Licence Document [Issued Licence] (26.2.01).pdf",
            "22722460__Application New Licence Issued [17.1.1992] (26.7.2010).pdf",
            "22722580r01__Application Transfer - Issued Licence 24092021.pdf",
            "22723556__Application - Formal Variation -Application New Licence Issued 12_04_2019 00_00_00 10797059.pdf",
            "ne0270021016__Application - Minor Variation -Application New Licence Issued 12_03_2021 00_00_00 11736007.pdf",
            "NE0270022058__Application New Issued Licence 18.05.23.pdf",
            "NE0270023043__Application New Licence Issued 18.12.2018 10623801.pdf",
            "NE0270023047__Application - New -Application New Licence Issued 06_04_2020 00_00_00 11303354.pdf",
            "22719149__Application Formal Variation - Issued Licence [04-09-2018] 10474343.pdf",
            "22719156__Application Formal Variation Licence Issued - 12102023.pdf",
            "22720093__Non-Application Licence Document (02.02.1998).pdf",
            "22720211__Non-Application Licence Document (01.12.1990).pdf",
            "22724371r01__Application NA Formal Variation Issued Licence 21122021.pdf",
            "NE0270020038__Application - New Licence Issued - Licence Issued - PDF - 28.10.2022.pdf",
            "NE0270020044__Application New Licence Issued - 20112024.pdf"
        ];
    }
}