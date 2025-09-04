using System.Text.Json;
using WALE.ProcessFile.Services.Enums.OutputSchema;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class Licence
{
    public string Id
    {
        get
        {
            var licenceNumber = LicenceNumber?
                .Replace(" ", string.Empty)
                .Replace("/", string.Empty);
            
            return $"{licenceNumber}-{LicenceVersion.LicenceVersionId}";
        }
    }
    
    public string? LicenceNumber { get; init; }
    
    public string? Filename { get; set; }

    public LicenceVersion LicenceVersion { get; init; } = new();
    
    public PointOfAbstraction[] Points { get; init; } = [];
    
    public PurposeOfAbstraction[] Purposes { get; init; } = [];

    public PeriodOfAbstraction[] PeriodsOfAbstraction { get; init; } = [];
    
    public MeanOfAbstraction[] MeansOfAbstraction { get; init; } = [];

    public AbstractionLimits AbstractionLimits { get; init; } = new();    
    
    public TimePeriod? DefinitionOfYear { get; init; }

    public static string GetSchemaForPrompt()
    {
        return JsonSerializer.Serialize(Empty, JsonHelper.GetSerializer());
    }
    
    public static Licence Empty => new()
    {
        LicenceNumber = string.Empty,
        Filename = null,
        LicenceVersion = LicenceVersion.Template,
        Points = [PointOfAbstraction.Template],
        Purposes = [PurposeOfAbstraction.Template],
        PeriodsOfAbstraction = [
            new PeriodOfAbstraction
            {
                Description = string.Empty,
                EndDate = string.Empty,
                Id = string.Empty,
                Inclusive = false,
                NaldId = null,
                PeriodType = AbstractionPeriodType.Unknown,
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
                    PeriodType = LimitPeriodType.NotApplicable,
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
        AbstractionLimits = AbstractionLimits.Template,
        DefinitionOfYear = new()
        {
            StartDate = string.Empty,
            EndDate = string.Empty,
            Inclusive = false
        }
    };
}