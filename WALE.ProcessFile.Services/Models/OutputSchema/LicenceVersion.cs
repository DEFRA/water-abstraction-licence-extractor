using System.Text.Json;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class LicenceVersion
{
    public string LicenceVersionId
    {
        get
        {
            if (EffectiveDate == null && ExpiryDate == null)
            {
                const string unknownVersion = "LVUNKNOWN";
                return unknownVersion;
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

    public static LicenceVersion Empty => new()
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
        return JsonSerializer.Serialize(Empty, JsonHelper.GetSerializer());
    }
}