using WALE.ProcessFile.Models.Enums.OutputSchema;

namespace WALE.ProcessFile.Models.OutputSchema;

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
    
    public LicenceStatus Status { get; init; }
    
    public string? LicenceNumber { get; set; }
    
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
    
    public Dictionary<string, object> NoneSchemaData { get; set; } = [];
}