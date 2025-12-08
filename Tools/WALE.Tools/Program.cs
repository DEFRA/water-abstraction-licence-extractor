using WALE.Tools;

//const string workflow = "TestsForAiPrompts";
//const string workflow = "GenerateAggregatesCsvForTesting";
const string workflow = "GenerateLinkedLicencesCsv";
//const string workflow = "GenerateUnknownSectionLinkedLicencesCsv";

switch (workflow)
{
    case "GenerateLinkedLicencesCsv":
        const int processRunId1 = 842;
        await GenerateLinkedLicencesCsv.GenerateCsvAsync(processRunId1);

        break;
    case "GenerateUnknownSectionLinkedLicencesCsv":
        const int processRunId2 = 804;
        await GenerateUnknownSectionLinkedLicencesCsv.GenerateCsvAsync(processRunId2);
        
        break;
    case "GenerateAggregatesCsvForTesting":
        await GenerateAggregatesCsvForTesting.GenerateCsvForTestingAsync();
        break;
    case "TestsForAiPrompts":
        await TestsForAiPrompts.TestsForAiPromptsAsync();
        break;
}