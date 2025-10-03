using WALE.Tools;

//const string workflow = "TestsForAiPrompts";
const string workflow = "GenerateLicenceReaderExtract";

switch (workflow)
{
    case "GenerateCsvForTesting":
        await GenerateCsvForTesting.GenerateCsvForTestingAsync();
        break;
    case "GenerateLicenceReaderExtract":
        await GenerateLicenceReaderExtract.GenerateLicenceReaderExtractAsync();
        break;
}