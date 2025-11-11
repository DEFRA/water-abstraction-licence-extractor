namespace WALE.ProcessFile.Models;

public class NaldData
{
    public string? LicenceNumber { get; set; }
    public string? ExpiryDate { get; init; }
    public string? VersionStartDate { get; init; }
    public List<string> AggregateConditions { get; init; } = [];
    public List<double> Points { get; init; } = [];
}