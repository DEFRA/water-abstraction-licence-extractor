using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema.PromptSpecific;

public class BaseLicence
{
    public string? LicenceNumber { get; init; }
    
    public TimePeriod? DefinitionOfYear { get; init; }
    
    public static string GetSchemaForPrompt()
    {
        return JsonSerializer.Serialize(Template, JsonHelper.GetSerializerOptions());
    }
    
    public static BaseLicence Template => new()
    {
        LicenceNumber = "01/02/05/S*",
        DefinitionOfYear = new()
        {
            StartDate = "1st January",
            EndDate = "31st December",
            Inclusive = true
        }
    };
}