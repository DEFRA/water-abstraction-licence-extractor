using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Models.PromptSpecific;

public class AbstractionLimitArrayWrapped
{
    public AbstractionLimit[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new AbstractionLimitArrayWrapped { Data = [AbstractionLimit.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializerOptions());
    }
}