using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Models.PromptSpecific;

public class PointOfAbstractionArrayWrapped
{
    public PointOfAbstraction[] Data { get; init; } = [];
    
    public static string GetSchemaForPrompt()
    {
        var template = new PointOfAbstractionArrayWrapped { Data = [PointOfAbstraction.Template] };
        return JsonSerializer.Serialize(template, JsonHelper.GetSerializerOptions());
    }
}