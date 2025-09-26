using System.Text.Json;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema.PromptSpecific;

public class AbstractionLimitGroupArrayWrapped
{
    public AbstractionLimitGroup[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new AbstractionLimitGroupArrayWrapped { Data = [AbstractionLimitGroup.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializerOptions());
    }
}