using WALE.Tools;

//const string workflow = "TestsForAiPrompts";
//const string workflow = "GenerateAggregatesCsvForTesting";
const string workflow = "GenerateLinkedLicencesCsv";

switch (workflow)
{
    case "GenerateLinkedLicencesCsv":
        const int processRunId = 718;
        await GenerateLinkedLicencesCsv.GenerateCsvAsync(processRunId);
        break;
    case "GenerateAggregatesCsvForTesting":
        await GenerateAggregatesCsvForTesting.GenerateCsvForTestingAsync();
        break;
    case "TestsForAiPrompts":
        await TestsForAiPrompts.TestsForAiPromptsAsync();
        break;
}