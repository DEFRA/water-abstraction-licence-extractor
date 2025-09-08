using WALE.Tools;

const string workflow = "TestsForAiPrompts";

switch (workflow)
{
    case "GenerateCsvForTesting":
        await GenerateCsvForTesting.GenerateCsvForTestingAsync();
        break;
    case "TestsForAiPrompts":
        await TestsForAiPrompts.TestsForAiPromptsAsync();
        break;
}