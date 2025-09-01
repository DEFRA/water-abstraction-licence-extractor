using WALE.ProcessFile.Services.Enums.OutputSchema;

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
    
    public static Licence Empty => new()
    {
        LicenceNumber = string.Empty,
        Filename = string.Empty,
        LicenceVersion = new()
        {
            NaldStartDate = DateTime.MinValue,
            NaldEndDate = DateTime.MinValue,
            NaldVersionNumber = string.Empty,
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
                NaldId = string.Empty,
                PurposeIds = [
                    0.0
                ]
            }
        ],
        Purposes = [
            new()
            {
                NaldId = string.Empty,
                Description = string.Empty,
                Id = string.Empty,
                PointIds = [
                    0.0
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
                NaldId = string.Empty,
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
                    NaldType = string.Empty,
                    PrimaryType = PrimaryType.InLicence
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