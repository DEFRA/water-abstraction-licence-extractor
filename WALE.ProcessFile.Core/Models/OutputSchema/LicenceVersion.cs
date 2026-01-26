using System.Text.Json;
using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class LicenceVersion
{
    public static string UnknownVersion = "LVUNKNOWN";

    private string? _explicitLicenceVersionId;

    public void SetExplicitLicenceVersionId(string licenceVersionId)
    {
        _explicitLicenceVersionId = licenceVersionId;
    }

    public string LicenceVersionId
    {
        get
        {
            if (!string.IsNullOrEmpty(_explicitLicenceVersionId))
            {
                return _explicitLicenceVersionId;
            }
            
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
    
    public DateTime? NaldRevisionDate { get; set; }
    
    public DateTime? NaldExpiryDate { get; set; }
    
    public DateTime? NaldOrigEffectiveDate { get; set; }
    
    public DateTime? NaldOrigSignatureDate { get; set; }
    public DateTime? NaldSignatureDate { get; set; }
    public DateTime? NaldEffectiveStartDate { get; set; }
    public DateTime? NaldEffectiveEndDate { get; set; }
    public int? NaldIssueNumber { get; set; }
    public int? NaldIncrementNumber { get; set; }
    public string? NaldUpdateReason { get; set; }

    public static LicenceVersion Template => new()
    {
        NaldRevisionDate = null,
        NaldExpiryDate = null,
        NaldOrigEffectiveDate = null,
        NaldOrigSignatureDate = null,        
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