using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Models.PromptSpecific;

public class AbstractionLimitGroupArrayWrapped
{
    public AbstractionLimitGroup[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new AbstractionLimitGroupArrayWrapped { Data = [AbstractionLimitGroup.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializerOptions());
    }
}