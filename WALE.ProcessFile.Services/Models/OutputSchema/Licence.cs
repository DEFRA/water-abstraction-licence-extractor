namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class Licence
{
    public string? LicenceNumber { get; init; }
    
    public string? Filename { get; init; }

    public LicenceVersion LicenceVersion { get; init; } = new();
    
    public AbstractionLimits AbstractionLimits { get; init; } = new();

    public TimePeriod[] PeriodsOfAbstraction { get; set; } = [];
    
    public TimePeriod? DefinitionOfYear { get; set; }

    public Purpose[] Purposes { get; init; } = [];

    public Point[] Points { get; init; } = [];
}