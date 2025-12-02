using WALE.Tools;

//const string workflow = "TestsForAiPrompts";
//const string workflow = "GenerateAggregatesCsvForTesting";
//const string workflow = "GenerateLinkedLicencesCsv";
const string workflow = "GenerateUnknownSectionLinkedLicencesCsv";

const int processRunId = 741;

switch (workflow)
{
    case "GenerateLinkedLicencesCsv":
        await GenerateLinkedLicencesCsv.GenerateCsvAsync(processRunId);
        break;
    case "GenerateUnknownSectionLinkedLicencesCsv":
        await GenerateUnknownSectionLinkedLicencesCsv.GenerateCsvAsync(processRunId);
        break;
    case "GenerateAggregatesCsvForTesting":
        await GenerateAggregatesCsvForTesting.GenerateCsvForTestingAsync();
        break;
    case "TestsForAiPrompts":
        await TestsForAiPrompts.TestsForAiPromptsAsync();
        break;
}