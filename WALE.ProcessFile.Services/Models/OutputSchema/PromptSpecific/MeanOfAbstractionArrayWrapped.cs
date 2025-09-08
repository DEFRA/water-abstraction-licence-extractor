using System.Text.Json;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema.PromptSpecific;

public class MeanOfAbstractionArrayWrapped
{
    public MeanOfAbstraction[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new MeanOfAbstractionArrayWrapped { Data = [MeanOfAbstraction.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializer());
    }
}