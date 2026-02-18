using WALE.Tools;
using WALE.Tools.Config;

//const string workflow = "TestsForAiPrompts";
//const string workflow = "GenerateAggregatesCsvForTesting";
//const string workflow = "GenerateLinkedLicencesCsv";
//const string workflow = "GenerateUnknownSectionLinkedLicencesCsv";
const string workflow = "DuplicateLicenceIdentificationExtractBySize";

const int processRunId = 631;
var pdfFolder = KeyConfig.PdfFolder5;
const int regionCode = 3;

switch (workflow)
{
    // 1st half of process tools
    case "GenerateLicenceReaderExtract": // Scrapes the DOI that will be uses in Live Licence Identification
        await GenerateLicenceReaderExtract.GenerateLicenceReaderExtractAsync(pdfFolder, regionCode);
        break;
    case "DuplicateLicenceIdentificationExtract": // We don't run anymore
        await DuplicateLicenceIdentificationExtract.GenerateDuplicateLicenceIdentificationExtractAsync();
        break;
    case "DuplicateLicenceIdentificationExtractBySize": // Identify duplicates by file size
        await DuplicateLicenceIdentificationExtract.GenerateDuplicateLicenceIdentificationExtractAsync(false);
        break;
    case "FileTypeIdentificationExtract": // Version File Type Identification
        await FileTypeIdentificationExtract.GenerateFileTypeIdentificationAsync();
        break;
    case "FilesAvailableForLicenceIdentificationExtract": // Identify files available for licence identification
        TemplateIdentificationExtract.GenerateWaterPdfsFolderInventory();
        break;
    case "TemplateFinderExtract":
        await TemplateIdentificationExtract.GenerateTemplateFinderResult("NW");
        break;
    
    // 2nd half tools
    case "GenerateLinkedLicencesCsv": // Generates a linked licence file for Mitin and Shaun
        await GenerateLinkedLicencesCsv.GenerateCsvAsync(processRunId);
        break;
    case "GenerateUnknownSectionLinkedLicencesCsv": // A one-off file for debugging
        await GenerateUnknownSectionLinkedLicencesCsv.GenerateCsvAsync(processRunId);
        break;
    case "GenerateAggregatesCsvForTesting": // A file to give to James and team
        await GenerateAggregatesCsvForTesting.GenerateCsvForTestingAsync();
        break;
    case "TestsForAiPrompts": // An old POC in AI prompts to read files
        await TestsForAiPrompts.TestsForAiPromptsAsync();
        break;
    case "GenerateEALicenceFeaturesCsv": // Pull licence features out a file (Ryan)
        await GenerateEaLicenceFeaturesCsv.GenerateCsvAsync(processRunId);
        break;
    case "PopulateCachedImageWidthAndHeights": // Populate image widths and heights for cached images (one off)
        await PopulateCachedImageWidthAndHeights.PopulateWidthAndHeightsAsync();
        break;
    case "UpdateCachedImageWidthAndHeightsFilenames": // Populate image widths and heights for cached images (one off)
        await UpdateCachedImageWidthAndHeightsFilenames.PopulateWidthAndHeightsAsync();
        break;
    case "ImportNaldData": // A (monthly?) import needed to import data from CSVs
        await ImportNaldData.ImportAsync();
        break;
}