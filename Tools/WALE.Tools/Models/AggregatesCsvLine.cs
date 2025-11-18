namespace WALE.Tools.Models;

public class AggregatesCsvLine
{
    public string? Filename { get; set; }
    public string? LicenceNumber { get; set; }
    public bool HasAggregate { get; set; }
    public string? AggregateData { get; set; }
    public string? IndividualLimits { get; set; }
    public string? Data { get; set; }
    public string? LicenceSets { get; set; }
}