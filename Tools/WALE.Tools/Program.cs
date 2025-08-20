using System.Collections;
using System.Globalization;
using System.Text.Json;
using CsvHelper;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.Tools;

var pdfDataExtractor = new PdfDataExtractorService(
    new PdfPigNoOcrDataExtractorService(),
    new List<IOcrDataExtractorService>
    {
        new AzureAiVisionOcrDataExtractorService(
            KeyConfig.AiVisionEndpoint,
            KeyConfig.AiVisionKey)
    },
    KeyConfig.PdfFolder);

var internalJson = await GetMatchesAsync("2-26-32-126 6937559.PDF");
var file1 = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];

internalJson = await GetMatchesAsync("2-27-29-012 7003124.PDF");
var file2 = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];

internalJson = await GetMatchesAsync("Application - New - Licence Issued 30092021.pdf");
var file3 = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];

internalJson = await GetMatchesAsync("Application Formal Variation Issued Licence 07032023 (1).pdf");
var file4 = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];

internalJson = await GetMatchesAsync("Application Formal Variation Issued Licence 07032023.pdf");
var file5 = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];

internalJson = await GetMatchesAsync("Application Minor Variation Issued Licence 03.10.24.pdf");
var file6 = SchemaConverter.ToLicenceGroup(internalJson).Licences[0];

var data = new List<CsvLine>
{
    new() { Filename = file1.Filename, LicenceNumber = file1.LicenceNumber, Data = JsonSerializer.Serialize(file1) },
    new() { Filename = file2.Filename, LicenceNumber = file2.LicenceNumber, Data = JsonSerializer.Serialize(file2) },
    new() { Filename = file3.Filename, LicenceNumber = file3.LicenceNumber, Data = JsonSerializer.Serialize(file3) },
    new() { Filename = file4.Filename, LicenceNumber = file4.LicenceNumber, Data = JsonSerializer.Serialize(file4) },
    new() { Filename = file5.Filename, LicenceNumber = file5.LicenceNumber, Data = JsonSerializer.Serialize(file5) },
    new() { Filename = file6.Filename, LicenceNumber = file6.LicenceNumber, Data = JsonSerializer.Serialize(file6) }
};

await using var writer = new StreamWriter("Yorkshire-6-20250820.csv");
await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

csv.WriteRecords((IEnumerable)data);
return;

Task<MatchesResult> GetMatchesAsync(string fileName)
{
    Dictionary<string, string> fileLicenceMapping = new() {{"", ""}};
    var pdfFolder = KeyConfig.PdfFolder;
    
    return pdfDataExtractor.GetMatchesAsync(
        pdfFolder + fileName,
        new LookupConfiguration(
            LabelConfiguration.GetLabels(),
            fileLicenceMapping,
            "Output/",
            "Cache/"),
        [pdfFolder + fileName]);
}

internal class CsvLine
{
    public string? Filename { get; set; }
    public string? LicenceNumber { get; set; }    
    public string? Data { get; set; }
}