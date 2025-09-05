using System.Text.Json;
using WALE.ProcessFile.Services.Enums.OutputSchema;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema.PromptSpecific;

public class BaseLicence
{
    public string? LicenceNumber { get; init; }
    
    public PeriodOfAbstraction[] PeriodsOfAbstraction { get; init; } = [];
    
    public MeanOfAbstraction[] MeansOfAbstraction { get; init; } = [];
    
    public TimePeriod? DefinitionOfYear { get; init; }
    
    public static string GetSchemaForPrompt()
    {
        return JsonSerializer.Serialize(Template, JsonHelper.GetSerializer());
    }
    
    public static BaseLicence Template => new()
    {
        LicenceNumber = "01/02/05/S*",
        PeriodsOfAbstraction = [
            new PeriodOfAbstraction
            {
                Description = string.Empty,
                EndDate = string.Empty,
                Id = string.Empty,
                Inclusive = false,
                NaldId = null,
                PeriodType = AbstractionPeriodType.PerYear,
                PointIds = [
                    string.Empty
                ],
                PurposeIds = [
                    string.Empty
                ],
                StartDate = string.Empty
            }
        ],
        MeansOfAbstraction = [
            new()
            {
                Description = string.Empty,
                Id = 0,
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
            }
        ],
        DefinitionOfYear = new()
        {
            StartDate = "1st January",
            EndDate = "31st December",
            Inclusive = true
        }
    };
}