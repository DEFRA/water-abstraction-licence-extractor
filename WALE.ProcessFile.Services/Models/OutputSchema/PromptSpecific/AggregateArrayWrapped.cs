using System.Text.Json;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema.PromptSpecific;

public class AggregateArrayWrapped
{
    public Aggregate[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new AggregateArrayWrapped { Data = [Aggregate.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializer());
    }
}