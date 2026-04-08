using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class Licence
{
    public string Id
    {
        get
        {
            var licenceNumber = FormattingHelper.RemoveSeperators(LicenceNumber?.Value);
            return $"{licenceNumber}-{LicenceVersion.LicenceVersionId}";
        }
    }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenceStatus Status { get; init; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NaldLicenceStatus NaldStatus { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LicenceType LicenceType { get; set; }
    
    public ValueWithConfidence<string>? LicenceNumber { get; init; }
    
    public string? DmsPermitNumber { get; set; }
    
    public string? DmsPath { get; set; }
    
    public Guid? DmsFileId { get; set; }
    
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
}