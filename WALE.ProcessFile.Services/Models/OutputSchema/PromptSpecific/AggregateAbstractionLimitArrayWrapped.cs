using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema.PromptSpecific;

public class AggregateAbstractionLimitArrayWrapped
{
    public AggregateAbstractionLimit[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new AggregateAbstractionLimitArrayWrapped { Data = [AggregateAbstractionLimit.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializerOptions());
    }
}