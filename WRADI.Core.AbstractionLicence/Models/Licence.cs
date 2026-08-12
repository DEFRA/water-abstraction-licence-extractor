using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Enums;
using LicenceType = WRADI.Core.AbstractionLicence.Enums.LicenceType;

namespace WRADI.Core.AbstractionLicence.Models;

public class Licence
{
    public int? ProcessRunId { get; set; }
    
    public string Id
    {
        get
        {
            var licenceNumber = FormattingHelper.RemoveSeperators(LicenceNumber?.Value);
            return $"{licenceNumber}-{LicenceVersion.LicenceVersionId}";
        }
    }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ScrapeStatus Status { get; init; }
    
    public ValueWithConfidence<string>? LicenceNumber { get; init; }
    
    public int? RegionId { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenceType LicenceType { get; set; }
    
    public string? DmsPermitNumber { get; set; }
    
    public string? DmsPath { get; set; }
    
    public Guid? DmsFileId { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NaldLicenceStatus NaldStatus { get; set; }
        
    public bool? NaldHasAggregateCondition { get; set; }
    
    public string? Filename { get; set; }

    public LicenceVersion LicenceVersion { get; init; } = new();
    
    public PointOfAbstraction[] Points { get; init; } = [];
    
    public PurposeOfAbstraction[] Purposes { get; init; } = [];

    public PeriodOfAbstraction[] PeriodsOfAbstraction { get; init; } = [];
    
    public MeanOfAbstraction[] MeansOfAbstraction { get; init; } = [];

    public AbstractionLimits AbstractionLimits { get; init; } = new();    
    
    public TimePeriod? DefinitionOfYear { get; init; }
    
    public LinkedLicence[] LinkedLicences { get; set; } = [];
    
    public LicenceSetReference[] LicenceSets { get; set; } = [];

    public Dictionary<string, object?> NoneSchemaData { get; set; } = [];

    public Licence CloneWithAbstractionLimits(AbstractionLimits abstractionLimits)
    {
        return new Licence
        {
            ProcessRunId = ProcessRunId,
            Status = Status,
            LicenceNumber = LicenceNumber,
            RegionId = RegionId,
            LicenceType = LicenceType,
            DmsPermitNumber = DmsPermitNumber,
            DmsPath = DmsPath,
            DmsFileId = DmsFileId,
            NaldStatus = NaldStatus,
            NaldHasAggregateCondition = NaldHasAggregateCondition,
            Filename = Filename,
            LicenceVersion = LicenceVersion,
            Points = Points,
            Purposes = Purposes,
            PeriodsOfAbstraction = PeriodsOfAbstraction,
            MeansOfAbstraction = MeansOfAbstraction,
            AbstractionLimits = abstractionLimits,
            DefinitionOfYear = DefinitionOfYear,
            LinkedLicences = LinkedLicences,
            LicenceSets = LicenceSets,
            NoneSchemaData = NoneSchemaData
        };
    }
}