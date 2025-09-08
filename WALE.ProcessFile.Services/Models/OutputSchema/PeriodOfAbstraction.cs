using System.Text.Json;
using WALE.ProcessFile.Services.Enums.OutputSchema;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class PeriodOfAbstraction : TimePeriod
{
    public string? Id { get; set; }
    
    public string? Description { get; set; }
    
    public string? NaldId { get; set; }
    
    public string[] PointIds { get; set; } = [];
    
    public string[] PurposeIds { get; set; } = [];
    
    public static string GetSchemaForPrompt()
    {
        return JsonSerializer.Serialize(Template, JsonHelper.GetSerializer());
    }

    public static PeriodOfAbstraction Template => new()
    {
        Description = string.Empty,
        EndDate = string.Empty,
        Id = string.Empty,
        Inclusive = false,
        NaldId = null,
        PeriodType = AbstractionPeriodType.PerYear,
        PointIds =
        [
            string.Empty
        ],
        PurposeIds =
        [
            string.Empty
        ],
        StartDate = string.Empty
    };
}