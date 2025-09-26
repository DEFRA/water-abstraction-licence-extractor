using System.Text.Json;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema.PromptSpecific;

public class PeriodOfAbstractionArrayWrapped
{
    public PeriodOfAbstraction[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new PeriodOfAbstractionArrayWrapped { Data = [PeriodOfAbstraction.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializerOptions());
    }
}