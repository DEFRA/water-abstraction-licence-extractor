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
    case "GenerateLicenceReaderExtract":
        await GenerateLicenceReaderExtract.GenerateLicenceReaderExtractAsync();
        break;
    case "DuplicateLicenceIdentificationExtract":
        await DuplicateLicenceIdentificationExtract.GenerateDuplicateLicenceIdentificationExtractAsync();
        break;
    case "DuplicateLicenceIdentificationExtractBySize":
        await DuplicateLicenceIdentificationExtract.GenerateDuplicateLicenceIdentificationExtractAsync(false);
        break;
    case "FileTypeIdentificationExtract":
        await FileTypeIdentificationExtract.GenerateFileTypeIdentificationAsync();
        break;
}