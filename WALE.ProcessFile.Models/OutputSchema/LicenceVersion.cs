using System.Text.Json;
using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Models.OutputSchema;

public class LicenceVersion
{
    public static string UnknownVersion = "LVUNKNOWN";
    
    public string LicenceVersionId
    {
        get
        {
            if (EffectiveDate == null && ExpiryDate == null)
            {
                return UnknownVersion;
            }

            return $"LV{EffectiveDate:yyyyMMdd}{ExpiryDate:yyyyMMdd}";
        }
    }
    
    public DateTime? EffectiveDate { get; set; }
    
    public DateTime? ExpiryDate { get; set; }

    public DateTime? IssueDate { get; set; }
    
    public string? Issuer { get; set; }
    
    public DateTime? OriginalIssueDate { get; set; }
    
    public DateTime? NaldStartDate { get; set; }
    
    public DateTime? NaldEndDate { get; set; }
    
    public string? NaldVersionNumber { get; set; }

    public static LicenceVersion Template => new()
    {
        NaldStartDate = null,
        NaldEndDate = null,
        NaldVersionNumber = null,
        Issuer = string.Empty,
        EffectiveDate = DateTime.MinValue,
        ExpiryDate = DateTime.MinValue,
        IssueDate = DateTime.MinValue,
        OriginalIssueDate = DateTime.MinValue
    };
    
    public static string GetSchemaForPrompt()
    {
        // TODO this should happen elsewhere
        return JsonSerializer.Serialize(Template, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        });
    }
}