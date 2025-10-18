using WALE.Tools;

//const string workflow = "TestsForAiPrompts";
const string workflow = "DuplicateLicenceIdentificationExtract";

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
}