using WALE.Tools;

//const string workflow = "TestsForAiPrompts";
const string workflow = "FileTypeIdentificationExtract";

switch (workflow)
{
    case "GenerateLinkedLicencesCsv":
        await GenerateLinkedLicencesCsv.GenerateCsvAsync();
        break;
    case "GenerateAggregatesCsvForTesting":
        await GenerateAggregatesCsvForTesting.GenerateCsvForTestingAsync();
        break;
    case "GenerateLicenceReaderExtract": // Scrapes the DOI that will be uses in Live Licence Identification
        await GenerateLicenceReaderExtract.GenerateLicenceReaderExtractAsync();
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
    case "TemplateFinderExtract":
        await TemplateIdentificationExtract.GenerateTemplateFinderResult();
        break;
}