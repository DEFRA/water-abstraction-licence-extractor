namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class Licence
{
    public string? LicenceNumber { get; init; }
    
    public string? Filename { get; init; }

    public LicenceVersion LicenceVersion { get; init; } = new();
    
    public AbstractionLimits AbstractionLimits { get; init; } = new();
    
    public TimePeriod? PeriodOfAbstraction { get; set; }
    
    public TimePeriod? DefinitionOfYear { get; set; }

    public Purpose[] Purposes { get; set; } = [];

    public Point[] Points { get; set; } = [];
}