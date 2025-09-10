using System.Text.Json;
using WALE.ProcessFile.Services.Enums.OutputSchema;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class AbstractionLimits
{
    public AbstractionLimitGroup[] Individual { get; init; } = [];
    
    public Aggregate[] Aggregates { get; init; } = [];

    public static AbstractionLimits Template = new()
    {
        Aggregates =
        [
            new()
            {
                AggregateSetId = string.Empty,
                NaldType = null,
                PrimaryType = PrimaryType.NotSet,
                SubType = SubType.NotSet,
                TimeCutoff = new TimeCutoff
                {
                    Date = null,
                    CutoffType = CutoffType.Unknown
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
        Individual = [AbstractionLimitGroup.Template]
    };
}