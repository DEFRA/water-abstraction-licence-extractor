using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema.PromptSpecific;

public class PointOfAbstractionArrayWrapped
{
    public PointOfAbstraction[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new PointOfAbstractionArrayWrapped { Data = [PointOfAbstraction.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializerOptions());
    }
}