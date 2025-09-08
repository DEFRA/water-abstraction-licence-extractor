using System.Text.Json;
using WALE.ProcessFile.Services.Enums.OutputSchema;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class MeanOfAbstraction
{
    public string? Id { get; set; }
    
    public string? Description { get; set; }
    
    public AbstractionLimit? AbstractionLimit { get; set; }
    
    public static string GetSchemaForPrompt()
    {
        return JsonSerializer.Serialize(Template, JsonHelper.GetSerializer());
    }
    
    public static MeanOfAbstraction Template => new()
    {
        Description = string.Empty,
        Id = "(1)",
        AbstractionLimit = new()
        {
            ImplicitLimit = false,
            PeriodType = LimitPeriodType.PerYear,
            Points = [
                new()
                {
                    Description = string.Empty,
                    Id = string.Empty
                }
            ],
            Purposes = [
                new()
                {
                    Description = string.Empty,
                    Id = string.Empty
                }
            ],
            Units = string.Empty,
            Value = 0
        }
    };
}