using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Methods;

public static class BaseMethod
{
    public static async Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
        FunctionInputModel request,
        LabelGroupResult labelGroupResult)
    {
        var results = new List<LabelGroupResult>
        {
            labelGroupResult
        };

        return await ProcessSubLabelsAsync(request, results);
    }
    
    public static async Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
        FunctionInputModel request,
        List<LabelGroupResult> results)
    {
        foreach (var result in results)
        {
            var subResults = await request.pdfDataExtractorService!.ProcessSubLabelsAsync(
                request.label!,
                result.Text!,
                request.isOcr,
                request.serviceName,
                request.labelGroupName!,
                request.licenceMapping!,
                request.previouslyParsedPaths!,
                request.outputFolder!,
                request.useCache);
        
            if (request.label!.MinimumSubMatches.HasValue
                && request.label.MinimumSubMatches.Value > subResults.Count)
            {
                return [];
            }

            result.SubResults = subResults;
        }

        return results;
    }
}