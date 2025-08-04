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
    
    public string? Filename { get; init; }

    public LicenceVersion LicenceVersion { get; init; } = new();
    
    public PointOfAbstraction[] Points { get; init; } = [];
    
    public PurposeOfAbstraction[] Purposes { get; init; } = [];

    public PeriodOfAbstraction[] PeriodsOfAbstraction { get; set; } = [];
    
    public MeanOfAbstraction[] MeansOfAbstraction { get; set; } = [];

    public AbstractionLimits AbstractionLimits { get; init; } = new();    
    
    public TimePeriod? DefinitionOfYear { get; set; }
}