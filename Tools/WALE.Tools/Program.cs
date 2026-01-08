using WALE.Tools;

//const string workflow = "TestsForAiPrompts";
//const string workflow = "GenerateAggregatesCsvForTesting";
//const string workflow = "GenerateLinkedLicencesCsv";
//const string workflow = "GenerateUnknownSectionLinkedLicencesCsv";
//const string workflow = "UpdateCachedImageWidthAndHeightsFilenames";
const string workflow = "ImportNaldData";

const int processRunId = 45;

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
    case "GenerateEALicenceFeaturesCsv":
        await GenerateEaLicenceFeaturesCsv.GenerateCsvAsync(processRunId);
        break;
    case "PopulateCachedImageWidthAndHeights":
        await PopulateCachedImageWidthAndHeights.PopulateWidthAndHeightsAsync();
        break;
    case "UpdateCachedImageWidthAndHeightsFilenames":
        await UpdateCachedImageWidthAndHeightsFilenames.PopulateWidthAndHeightsAsync();
        break;    
    case "ImportNaldData":
        await ImportNaldData.ImportAsync();
        break;
}