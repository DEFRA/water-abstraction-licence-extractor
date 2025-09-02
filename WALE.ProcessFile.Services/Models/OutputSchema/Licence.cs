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
        LicenceVersion = new()
        {
            NaldStartDate = null,
            NaldEndDate = null,
            NaldVersionNumber = null,
            Issuer = string.Empty,
            EffectiveDate = DateTime.MinValue,
            ExpiryDate = DateTime.MinValue,
            IssueDate = DateTime.MinValue,
            OriginalIssueDate = DateTime.MinValue
        },
        Points = [
            new()
            {
                Description = string.Empty,
                Id = string.Empty,
                NaldId = null,
                PurposeIds = [
                    "4.1"
                ]
            }
        ],
        Purposes = [
            new()
            {
                Id = string.Empty,
                NaldId = null,
                Description = string.Empty,
                PointIds = [
                    "2.1"
                ]
            }
        ],
        PeriodsOfAbstraction = [
            new PeriodOfAbstraction
            {
                Description = string.Empty,
                EndDate = string.Empty,
                Id = 0,
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
                Limit = new()
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
        AbstractionLimits = new()
        {
            Aggregates = [
                new()
                {
                    AggregateSetId = string.Empty,
                    NaldType = null,
                    PrimaryType = PrimaryType.NotSet,
                    SubType = SubType.NotSet,
                    TimeCutoff = new TimeLimited
                    {
                        Date = null,
                        LimitationType = LimitationType.Unknown
                    },
                    TimePeriod = new TimePeriod
                    {
                        StartDate = null,
                        EndDate = null
                    },
                    LicenceNumber = null,
                    LicenceVersionId = null,
                    Limits = [],
                    LinkedLicences = []
                }
            ],
            Individual = [
                new()
                {
                    Value = 0,
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
                    Units = string.Empty
                }
            ]
        },
        DefinitionOfYear = new()
        {
            StartDate = string.Empty,
            EndDate = string.Empty,
            Inclusive = false
        }
    };
}