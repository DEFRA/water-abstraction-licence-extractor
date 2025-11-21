using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema.PromptSpecific;

public class AbstractionLimitArrayWrapped
{
    public AbstractionLimit[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new AbstractionLimitArrayWrapped { Data = [AbstractionLimit.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializerOptions());
    }
}