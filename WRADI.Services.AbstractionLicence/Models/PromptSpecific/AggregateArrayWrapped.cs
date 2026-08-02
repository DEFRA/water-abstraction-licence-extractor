using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Models.PromptSpecific;

public class AggregateArrayWrapped
{
    public Aggregate[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new AggregateArrayWrapped { Data = [Aggregate.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializerOptions());
    }
}