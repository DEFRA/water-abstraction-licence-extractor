using WALE.Tools;

//const string workflow = "TestsForAiPrompts";
const string workflow = "DuplicateLicenceIdentificationExtractBySize";

switch (workflow)
{
    case "GenerateCsvForTesting":
        await GenerateCsvForTesting.GenerateCsvForTestingAsync();
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