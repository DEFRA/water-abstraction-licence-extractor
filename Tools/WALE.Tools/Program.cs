using WALE.Tools._1stHalf;
using WALE.Tools._2ndHalf;
using WALE.Tools._2ndHalf.ImportNaldData;
using WALE.Tools.Config;

var workflow = "ImportNaldData";//"GenerateAggregatesCsvForTesting";//"GenerateLinkedLicencesCsv";//" "OverrideAddIncrements";//""GenerateLicenceReaderExtract";
workflow = "FilesAvailableForLicenceIdentificationExtract";

const int processRunId = 1707;
const int regionCode = 3; // Anglia=1, NE=3
var pdfFolder = KeyConfig.PdfFolder5; //KeyConfig.PdfFolderForDuplicates; //KeyConfig.PdfFolder5;
var duplicateResultsFilePath = Path.Combine(KeyConfig.PdfFolderForDuplicates, "Download_Info_20260218-2.xlsx"); // File comes from JP
var username = "xxx";
var overrideRootPath = $"/Users/{username}/Documents/GitHub/water-abstraction-licence-finder/WA.DMS.LicenceFinder.Services/Resources";

switch (workflow)
{
    // 1st half of process tools
    case "GenerateLicenceReaderExtract": // Scrapes the DOI that will be used in Live Licence Identification
        await GenerateLicenceReaderExtract.GenerateLicenceReaderExtractAsync(pdfFolder, regionCode);
        break;
    case "DuplicateLicenceIdentificationExtract": // Identify duplicates by name (NOTE We don't run anymore)
        await DuplicateLicenceIdentificationExtract.GenerateDuplicateLicenceIdentificationExtractAsync(
            duplicateResultsFilePath,
            KeyConfig.PdfFolderForDuplicates,
            true);
        break;
    case "DuplicateLicenceIdentificationExtractBySize": // Identify duplicates by file size
        await DuplicateLicenceIdentificationExtract.GenerateDuplicateLicenceIdentificationExtractAsync(
            duplicateResultsFilePath,
            pdfFolder,
            false);
        break;
    case "FileTypeIdentificationExtract": // Version File Type Identification
        await FileTypeIdentificationExtract.GenerateFileTypeIdentificationAsync();
        break;
    case "FilesAvailableForLicenceIdentificationExtract": // Identify local files available for licence
        // identification (feeds into other process)
        
        TemplateIdentificationExtract.GenerateWaterPdfsFolderInventory(username);
        break;
    case "TemplateFinderExtract":
        await TemplateIdentificationExtract.GenerateTemplateFinderResult("NW");
        break;
    case "OverrideAddIncrements":
        await OverrideAddIncrements.GenerateOverrideFileAsync(overrideRootPath);
        break;    
    
    // 2nd half tools
    case "GenerateLinkedLicencesCsv": // Generates a linked licence file for Mitin and Shaun
        await GenerateLinkedLicencesCsv.GenerateCsvAsync(processRunId);
        break;
    case "GenerateUnknownSectionLinkedLicencesCsv": // A one-off file for debugging
        await GenerateUnknownSectionLinkedLicencesCsv.GenerateCsvAsync(processRunId);
        break;
    case "GenerateAggregatesCsvForTesting": // A file to give to James and team
        await GenerateAggregatesCsvForTesting.GenerateCsvForTestingAsync(processRunId);
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