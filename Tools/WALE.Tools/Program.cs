using WALE.Tools;

//const string workflow = "TestsForAiPrompts";
//const string workflow = "GenerateAggregatesCsvForTesting";
const string workflow = "GenerateLinkedLicencesCsv";

switch (workflow)
{
    case "GenerateLinkedLicencesCsv":
        await GenerateLinkedLicencesCsv.GenerateCsvAsync();
        break;
    case "GenerateAggregatesCsvForTesting":
        await GenerateAggregatesCsvForTesting.GenerateCsvForTestingAsync();
        break;
    case "TestsForAiPrompts":
        await TestsForAiPrompts.TestsForAiPromptsAsync();
        break;
}