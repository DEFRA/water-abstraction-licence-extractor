using WALE.Tools;

//const string workflow = "TestsForAiPrompts";
const string workflow = "PdfContentReaderExtract";

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
    case "PdfContentReaderExtract":
        await PdfContentReaderExtract.GeneratePdfContentReaderExtractAsync();
        break;
}